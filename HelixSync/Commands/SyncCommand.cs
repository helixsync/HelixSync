// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HelixSync.HelixDirectory;

namespace HelixSync
{
    public static class SyncCommand
    {
        public static int Sync(SyncOptions options, ConsoleEx consoleEx = null, HelixFileVersion fileVersion = null)
        {
            consoleEx = consoleEx ?? new ConsoleEx();
            consoleEx.Verbosity = options.Verbosity;

            consoleEx.WriteLine("------------------------");
            consoleEx.WriteLine("-- HelixSync " + typeof(SyncCommand).GetTypeInfo().Assembly.GetName().Version.ToString());
            consoleEx.WriteLine("------------------------");
            consoleEx.WriteLine();

            consoleEx.WriteLine("Sync");
            consoleEx.WriteLine($"..DecrDir: {options.DecrDirectory}");
            consoleEx.WriteLine($"..EncrDir: {options.EncrDirectory}");
            if (options.WhatIf)
                consoleEx.WriteLine("..Options: WhatIf");
            consoleEx.WriteLine($"..Verbosity: {options.Verbosity}");

            consoleEx.WriteLine();

            if (options.WhatIf)
                consoleEx.WriteLine("** WhatIf Mode - No Changes Made **");


            using (HelixEncrDirectory encrDirectory = new HelixEncrDirectory(options.EncrDirectory, options.WhatIf))
            using (HelixDecrDirectory decrDirectory = new HelixDecrDirectory(options.DecrDirectory, whatIf: options.WhatIf))
            {
                DerivedBytesProvider derivedBytesProvider = DerivedBytesProvider.FromPassword(options.Password, options.KeyFile);

                if (!encrDirectory.IsInitialized())
                {
                    bool error;
                    string[] warnings = encrDirectory.PreInitializationWarnings(out error);
                    if (error)
                    {
                        consoleEx.WriteErrorLine("Encrypted Directory: Unable To Initialize");
                        foreach (var warning in warnings)
                            consoleEx.WriteErrorLine(".." + warning);
                        return -1;
                    }
                    consoleEx.WriteLine("Encrypted Directory: Needs Initialization");
                    foreach (string warning in warnings)
                        consoleEx.WriteLine(".." + warning);


                    warnings = new HelixDecrDirectory(options.DecrDirectory, DirectoryHeader.NewDirectoryId()).PreInitializationWarnings(out error);
                    if (error)
                    {
                        consoleEx.WriteErrorLine("Decrypted Directory: Unable to initialize");
                        foreach (var warning in warnings)
                            consoleEx.WriteErrorLine(".." + warning);
                        return -1;
                    }
                    consoleEx.WriteLine("Decrypted Directory: Needs Initialization");
                    foreach (string warning in warnings)
                        consoleEx.WriteLine(".." + warning);


                    if (!options.Initialize && !consoleEx.Interactive)
                    {
                        consoleEx.WriteErrorLine("To initialize the directory use the -Initialize switch or run in an interactive console.");
                        return -1;
                    }
                    else if (!options.Initialize)
                    {
                        bool initializeResponse = consoleEx.PromptBool("Initialized encrypted and decrypted directories now? [y/N] ", false);
                        if (!initializeResponse)
                        {
                            consoleEx.WriteErrorLine("Operation cancelled, must initialize directories before continuing");
                            return -1;
                        }
                    }

                    consoleEx.WriteLine();
                    if (options.WhatIf)
                    {
                        consoleEx.WriteLine("** WhatIf Mode - No Changes Made **");
                        consoleEx.WriteLine("Initialized Encrypted Directory (" + DirectoryHeader.EmptyDirectoryId().Substring(0, 6) + "...)");
                        decrDirectory.EncrDirectoryId = DirectoryHeader.EmptyDirectoryId();
                        consoleEx.WriteLine("Initialized Decrypted Directory");
                    }
                    else
                    {
                        encrDirectory.Initialize(derivedBytesProvider, fileVersion);
                        encrDirectory.Open(derivedBytesProvider);
                        consoleEx.WriteLine("Initialized Encrypted Directory (" + encrDirectory.Header.DirectoryId.Substring(0, 6) + "...)");
                        decrDirectory.EncrDirectoryId = encrDirectory.Header.DirectoryId;
                        decrDirectory.Initialize();
                        consoleEx.WriteLine("Initialized Decrypted Directory");
                        decrDirectory.Open(options.WhatIf);
                    }

                }
                else
                {
                    encrDirectory.Open(derivedBytesProvider);
                    consoleEx.WriteLine("Opened Encrypted Directory (" + encrDirectory.Header.DirectoryId.Substring(0, 6) + "...)");
                    decrDirectory.EncrDirectoryId = encrDirectory.Header.DirectoryId;

                    if (!decrDirectory.IsInitialized())
                    {
                        bool error;
                        string[] warnings = new HelixDecrDirectory(options.DecrDirectory, DirectoryHeader.NewDirectoryId()).PreInitializationWarnings(out error);
                        if (error)
                        {
                            consoleEx.WriteErrorLine("Decrypted Directory: Unable to initialize");
                            foreach (var warning in warnings)
                                consoleEx.WriteErrorLine(".." + warning);
                            return -1;
                        }
                        consoleEx.WriteLine("Decrypted Directory: Needs Initialization");
                        foreach (string warning in warnings)
                            consoleEx.WriteLine(".." + warning);

                        if (!options.Initialize && !consoleEx.Interactive)
                        {
                            consoleEx.WriteErrorLine("To initialize the directory use the -Initialize switch or run in an interactive console.");
                            return -1;
                        }
                        else if (!options.Initialize)
                        {
                            bool initializeResponse = consoleEx.PromptBool("Initialized decrypted directory now? [y/N] ", false);
                            if (!initializeResponse)
                            {
                                consoleEx.WriteErrorLine("Operation cancelled, must initialize directories before continuing");
                                return -1;
                            }
                        }

                        consoleEx.WriteLine();
                        if (options.WhatIf)
                        {
                            consoleEx.WriteLine("** WhatIf Mode - No Changes Made **");
                            consoleEx.WriteLine("Initialized Decrypted Directory");
                        }
                        else
                        {
                            decrDirectory.Initialize();
                            consoleEx.WriteLine("Initialized Decrypted Directory");
                            decrDirectory.Open(options.WhatIf);
                        }
                    }
                    else
                    {
                        decrDirectory.Open(options.WhatIf);
                        consoleEx.WriteLine("Opened Decrypted Directory");
                    }
                }
                consoleEx.WriteLine();



                using (DirectoryPair pair = new DirectoryPair(encrDirectory, decrDirectory, options.WhatIf))
                {
                    consoleEx.WriteLine("Key: [+] Add  [-] Remove  [c] Change  [x] Drop Delete Stub");
                    consoleEx.WriteLine("");

                    encrDirectory.Cleanup(consoleEx);
                    decrDirectory.Cleanup(consoleEx);

                    consoleEx.WriteLine(VerbosityLevel.Detailed, 0, "Finding changes...");
                    List<PreSyncDetails> changes = pair.FindChanges(reset: false, console: consoleEx);

                    if (changes.Count == 0)
                        consoleEx.WriteLine("--No Changes--");



                    int decrAdd = 0;
                    int decrRemove = 0;
                    int decrChange = 0;
                    int decrOther = 0;

                    int encrAdd = 0;
                    int encrRemove = 0;
                    int encrChange = 0;
                    int encrOther = 0;

                    int conflict = 0;

                    consoleEx.WriteLine(VerbosityLevel.Normal, 0, "Performing Sync...");
                    foreach (PreSyncDetails change in changes)
                    {
                        pair.RefreshPreSyncDetails(change);
                        consoleEx.WriteLine(change);

                        //todo: prompt on conflict

                        if (change.SyncMode == PreSyncMode.Conflict)
                        {
                            var decrModified = change.DecrInfo == null ? (object)null : change.DecrInfo.LastWriteTimeUtc.ToLocalTime();
                            var decrSize = change.DecrInfo == null ? (object)null : HelixUtil.FormatBytes5(change.DecrInfo.Length);
                            var encrModified = change.EncrInfo == null ? (object)null : change.EncrInfo.LastWriteTimeUtc.ToLocalTime();
                            var encrSize = change.EncrHeader == null ? (object)null : HelixUtil.FormatBytes5(change.EncrHeader.Length);

                            consoleEx.WriteLine($"    Decrypted - Modified: {decrModified}, Size: {decrSize}");
                            consoleEx.WriteLine($"    Encrypted - Modified: {encrModified}, Size: {encrSize}");
                            consoleEx.WriteLine($"");
                            consoleEx.WriteLine($"    D - Decrypted, E - Encrypted, S - Skip"); //todo: support newer, support always
                            var response = consoleEx.PromptChoice("    Select Option [D,E,S]? ", new string[] { "D", "E", "S" }, "S");
                            if (response == "D")
                                change.SyncMode = PreSyncMode.DecryptedSide;
                            else if (response == "E")
                                change.SyncMode = PreSyncMode.EncryptedSide;

                            if (change.SyncMode != PreSyncMode.Conflict)
                                consoleEx.WriteLine(change);
                        }

                        if (change.SyncMode == PreSyncMode.EncryptedSide)
                        {
                            if (change.DisplayOperation == PreSyncOperation.Add)
                                encrAdd++;
                            else if (change.DisplayOperation == PreSyncOperation.Remove)
                                encrRemove++;
                            else if (change.DisplayOperation == PreSyncOperation.Change)
                                encrChange++;
                            else
                                encrOther++;
                        }
                        else if (change.SyncMode == PreSyncMode.DecryptedSide)
                        {

                            if (change.DisplayOperation == PreSyncOperation.Add)
                                decrAdd++;
                            else if (change.DisplayOperation == PreSyncOperation.Remove)
                                decrRemove++;
                            else if (change.DisplayOperation == PreSyncOperation.Change)
                                decrChange++;
                            else
                                decrOther++;
                        }
                        else if (change.SyncMode == PreSyncMode.Conflict)
                        {
                            conflict++;
                        }


                        if (!options.WhatIf)
                        {
                            var syncResult = pair.TrySync(change, consoleEx);
                            //todo: add to error log
                            if (syncResult.Exception != null)
                                consoleEx.WriteErrorLine("..." + syncResult.Exception.Message);
                        }
                    }

                    //todo: show totals

                    Console.WriteLine("== Summary ==");
                    Console.WriteLine($"           | Add     | Remove  | Change  | Other   |");
                    Console.WriteLine($"ENC->DEC   | {encrAdd,7} | {encrRemove,7} | {encrChange,7} | {encrOther,7} |");
                    Console.WriteLine($"DEC->ENC   | {decrAdd,7} | {decrRemove,7} | {decrChange,7} | {decrOther,7} |");

                    //todo: fix unchanged
                    Console.WriteLine($"Other      |     --- |     --- |     --- | {conflict,7:#,0} |");
                    return 0;
                }
            }
        }
    }
}

