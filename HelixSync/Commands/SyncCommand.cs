// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Reflection;

namespace HelixSync
{
    public static class SyncCommand
    {
        public static void Sync(SyncOptions options, ConsoleEx consoleEx = null, HelixFileVersion fileVersion = null)
        {
            consoleEx = consoleEx ?? new ConsoleEx();
            consoleEx.WriteLine("------------------------");
            consoleEx.WriteLine("-- HelixSync " + Assembly.GetExecutingAssembly().GetName().Version.ToString());
            consoleEx.WriteLine("------------------------");
            consoleEx.WriteLine();

            consoleEx.WriteLine("Sync");
            consoleEx.WriteLine("..DecrDir: " + options.DecrDirectory);
            consoleEx.WriteLine("..EncrDir: " + options.EncrDirectory);
            if (options.WhatIf)
                consoleEx.WriteLine("..Options: WhatIf");
            consoleEx.WriteLine();

            if (options.WhatIf)
                consoleEx.WriteLine("** WhatIf Mode - No Changes Made **");


            using (HelixEncrDirectory encrDirectory = new HelixEncrDirectory(options.EncrDirectory))
            using (HelixDecrDirectory decrDirectory = new HelixDecrDirectory(options.DecrDirectory))
            {
                DerivedBytesProvider derivedBytesProvider = DerivedBytesProvider.FromPassword(options.Password, options.KeyFile);

                if (!encrDirectory.IsInitialized())
                {
                    bool error;
                    string[] warnings = encrDirectory.PreInitializationWarnings(out error);
                    if (error)
                    {
                        consoleEx.WriteLine("Encrypted Directory: Unable To Initialize");
                        foreach (var warning in warnings)
                            consoleEx.WriteLine(".." + warning);
                        throw new OperationCanceledException();
                    }
                    consoleEx.WriteLine("Encrypted Directory: Needs Initialization");
                    foreach (string warning in warnings)
                        consoleEx.WriteLine(".." + warning);


                    warnings = new HelixDecrDirectory(options.DecrDirectory, DirectoryHeader.NewDirectoryId()).PreInitializationWarnings(out error);
                    if (error)
                    {
                        consoleEx.WriteLine("Decrypted Directory: Unable to initialize");
                        foreach (var warning in warnings)
                            consoleEx.WriteLine(".." + warning);
                        throw new OperationCanceledException();
                    }
                    consoleEx.WriteLine("Decrypted Directory: Needs Initialization");
                    foreach (string warning in warnings)
                        consoleEx.WriteLine(".." + warning);



                    bool initialize = options.Initialize || consoleEx.PromptBool("Initialized encrypted and decripted directories now? [y/N] ", false);
                    if (!initialize)
                        throw new OperationCanceledException();

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
                            consoleEx.WriteLine("Decrypted Directory: Unable to initialize");
                            foreach (var warning in warnings)
                                consoleEx.WriteLine(".." + warning);
                            throw new OperationCanceledException();
                        }
                        consoleEx.WriteLine("Decrypted Directory: Needs Initialization");
                        foreach (string warning in warnings)
                            consoleEx.WriteLine(".." + warning);

                        bool initialize = options.Initialize || consoleEx.PromptBool("Initialized decripted directory now? [y/N] ", false);
                        if (!initialize)
                            throw new OperationCanceledException();

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

                    List<PreSyncDetails> changes = pair.FindChanges();

                    //List<DirectoryChange> changes = pair.FindChanges();
                    if (changes.Count == 0)
                        consoleEx.WriteLine("--No Changes--");

                    foreach (PreSyncDetails change in changes)
                    {
                        pair.RefreshPreSyncDetails(change);
                        consoleEx.WriteLine(change);
                        string message;
                        bool retry;
                        if (!options.WhatIf)
                        {
                            pair.TrySync(change, out retry, out message);
                            if (!string.IsNullOrEmpty(message))
                                consoleEx.WriteLine("..." + message);
                        }
                    }
                }
            }
        }
    }
}

