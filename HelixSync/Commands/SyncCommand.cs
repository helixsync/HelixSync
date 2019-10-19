// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HelixSync.Commands;
using HelixSync.FileSystem;
using HelixSync.HelixDirectory;

namespace HelixSync
{
    public static class SyncCommand
    {
        public static SyncSummary Sync(SyncOptions options, ConsoleEx consoleEx = null, HelixFileVersion fileVersion = null)
        {
            var sum = new SyncSummary();

            consoleEx ??= new ConsoleEx();
            consoleEx.Verbosity = options.Verbosity;

            consoleEx.WriteLine("Sync");
            if (options.WhatIf)
                consoleEx.WriteLine("..Options: WhatIf");
            consoleEx.WriteLine($"..DecrDir: {options.DecrDirectory}");
            consoleEx.WriteLine($"..EncrDir: {options.EncrDirectory}");
            //consoleEx.WriteLine($"..Direction: {options.Direction}");
            consoleEx.WriteLine($"..Verbosity: {options.Verbosity}");

            consoleEx.WriteLine();

            if (options.WhatIf)
            {
                consoleEx.WriteLine("** WhatIf Mode - No Changes Made **");
                consoleEx.WriteLine("");
            }


            DerivedBytesProvider derivedBytesProvider = DerivedBytesProvider.FromPassword(options.Password, options.KeyFile);
            using DirectoryPair pair = new DirectoryPair(options.DecrDirectory, options.EncrDirectory, derivedBytesProvider, options.WhatIf);
            pair.PreInitializationCheck();

            if (pair.InitializeFullNeeded())
            {
                if (options.Initialize)
                {
                    //continue, unprompted
                    if (pair.InitializeMergeWarning())
                    {
                        consoleEx.WriteLine("WARNING: Decrypted directory is not empty and will be merged");
                    }
                }
                else
                {
                    consoleEx.WriteLine("Directories require initialization...");

                    if (!consoleEx.PromptBool("Initialized encrypted and decrypted directories now? [y/N] ", false))
                    {
                        consoleEx.WriteErrorLine("Operation cancelled");
                        sum.Error = new InitializationCanceledException();
                        return sum;
                    }

                    if (pair.InitializeMergeWarning())
                    {
                        if (!consoleEx.PromptBool("Decrypted directory is not empty and will be merged, continue? [y/N] ", false))
                        {
                            consoleEx.WriteErrorLine("Operation cancelled");
                            sum.Error = new InitializationCanceledException();
                            return sum;
                        }
                    }
                }

                pair.InitializeFull(consoleEx);
            }

            pair.OpenEncr(consoleEx);

            if (pair.InitializeDecrNeeded())
            {
                if (options.Initialize)
                {
                    //continue, unprompted
                    if (pair.InitializeMergeWarning())
                    {
                        consoleEx.WriteLine("WARNING: Decrypted directory is not empty and will be merged");
                    }
                }
                else
                {
                    consoleEx.WriteLine("Decrypted directory require initialization...");

                    if (!consoleEx.PromptBool("Initialized decrypted directories now? [y/N] ", false))
                    {
                        consoleEx.WriteErrorLine("Operation cancelled");
                        sum.Error = new InitializationCanceledException();
                        return sum;
                    }

                    if (pair.InitializeMergeWarning())
                    {
                        if (!consoleEx.PromptBool("Decrypted directory is not empty and will be merged, continue? [y/N] ", false))
                        {
                            consoleEx.WriteErrorLine("Operation cancelled");
                            sum.Error = new InitializationCanceledException();
                            return sum;
                        }
                    }
                }

                pair.InitializeDecr(consoleEx);
            }

            pair.OpenDecr(consoleEx);

            pair.Cleanup(consoleEx);

            List<PreSyncDetails> changes = pair.FindChanges(clearCache: false, console: consoleEx);

            if (changes.Count == 0)
                consoleEx.WriteLine("--No Changes--");




            

            var defaultConflictAction = "";

            consoleEx.WriteLine(VerbosityLevel.Normal, 0, "Performing Sync...");
            foreach (PreSyncDetails change in changes)
            {
                consoleEx.WriteLine(change);

                if (change.SyncMode == PreSyncMode.Conflict)
                {
                    var decrModified = change.DecrInfo == null ? (object)null : change.DecrInfo.LastWriteTimeUtc.ToLocalTime();
                    var decrSize = change.DecrInfo == null ? (object)null : HelixUtil.FormatBytes5(change.DecrInfo.Length);
                    var encrModified = change.EncrInfo == null ? (object)null : change.EncrInfo.LastWriteTimeUtc.ToLocalTime();
                    var encrSize = change.EncrHeader == null ? (object)null : HelixUtil.FormatBytes5(change.EncrHeader.Length);


                    string response;
                    if (defaultConflictAction != "")
                        response = defaultConflictAction;
                    else
                    {
                        consoleEx.WriteLine($"    Decrypted - Modified: {decrModified}, Size: {decrSize}");
                        consoleEx.WriteLine($"    Encrypted - Modified: {encrModified}, Size: {encrSize}");
                        consoleEx.WriteLine($"");
                        consoleEx.WriteLine($"    D - Decrypted, E - Encrypted, S - Skip, + Always perform this action"); //todo: support newer, support always
                        response = consoleEx.PromptChoice("    Select Option [D,E,S,D+,E+,S+]? ", new string[] { "D", "E", "S", "D+", "E+", "S+" }, "S");
                        if (response.EndsWith("+"))
                        {
                            response = response.Substring(0, 1);
                            defaultConflictAction = response;
                        }
                    }
                    if (response == "D")
                        change.SyncMode = PreSyncMode.DecryptedSide;
                    else if (response == "E")
                        change.SyncMode = PreSyncMode.EncryptedSide;

                    if (change.SyncMode != PreSyncMode.Conflict)
                        consoleEx.WriteLine(change);
                }

                if (change.SyncMode == PreSyncMode.EncryptedSide)
                {
                    //todo: make display operation be the actual operation
                    if (change.DisplayOperation == PreSyncOperation.Add)
                        sum.EncrAdd++;
                    else if (change.DisplayOperation == PreSyncOperation.Remove)
                        sum.EncrRemove++;
                    else if (change.DisplayOperation == PreSyncOperation.Change)
                        sum.EncrChange++;
                    else
                        sum.EncrOther++;
                }
                else if (change.SyncMode == PreSyncMode.DecryptedSide)
                {

                    if (change.DisplayOperation == PreSyncOperation.Add)
                        sum.DecrAdd++;
                    else if (change.DisplayOperation == PreSyncOperation.Remove)
                        sum.DecrRemove++;
                    else if (change.DisplayOperation == PreSyncOperation.Change)
                        sum.DecrChange++;
                    else
                        sum.DecrOther++;
                }
                else if (change.SyncMode == PreSyncMode.Conflict)
                {
                    sum.Conflict++;
                }


                var syncResult = pair.TrySync(change, consoleEx);
                //todo: add to error log
                if (syncResult.Exception != null)
                    consoleEx.WriteErrorLine("..." + syncResult.Exception.Message);
            }

            consoleEx.WriteLine("== Summary ==");
            consoleEx.WriteLine(sum);

            //todo: fix unchanged
            consoleEx.WriteLine();


            //consoleEx.WriteLine(VerbosityLevel.Diagnostic, 0, "");
            //consoleEx.WriteLine(VerbosityLevel.Diagnostic, 0, "==Decr Directory==");
            //foreach (var entry in decrDirectory.FSDirectory.GetEntries(SearchOption.AllDirectories))
            //{
            //    if (entry is FSDirectory dirEntry)
            //        consoleEx.WriteLine(VerbosityLevel.Diagnostic, 1, $"<dir> {entry.RelativePath}");
            //    else if (entry is FSFile fileEntry)
            //        consoleEx.WriteLine(VerbosityLevel.Diagnostic, 1, $"{HelixUtil.FormatBytes5(entry.Length)} {entry.RelativePath}");
            //}

            //consoleEx.WriteLine(VerbosityLevel.Diagnostic, 0, "");
            //consoleEx.WriteLine(VerbosityLevel.Diagnostic, 0, "==Encr Directory==");
            //foreach (var entry in encrDirectory.FSDirectory.GetEntries(SearchOption.AllDirectories))
            //{
            //    if (entry is FSDirectory dirEntry)
            //        consoleEx.WriteLine(VerbosityLevel.Diagnostic, 1, $"<dir> {entry.RelativePath}");
            //    else if (entry is FSFile fileEntry)
            //        consoleEx.WriteLine(VerbosityLevel.Diagnostic, 1, $"{HelixUtil.FormatBytes5(entry.Length)} {entry.RelativePath}");
            //}

            return sum;
        }
    }
}

