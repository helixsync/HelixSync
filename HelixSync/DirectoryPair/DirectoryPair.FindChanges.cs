using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HelixSync.FileSystem;

namespace HelixSync
{
    public partial class DirectoryPair
    {
        /// <summary>
        /// Returns a list of changes that need to be performed as part of the sync.
        /// </summary>
        public List<PreSyncDetails> FindChanges(bool clearCache = true, ConsoleEx console = null)
        {   
            //todo: disable default for reset
            console?.WriteLine(VerbosityLevel.Diagnostic, 0, "Finding Changes...");
            if (clearCache)
                ClearCache();

            using var rng = RandomNumberGenerator.Create();

            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Enumerating Encr Directory...");
            List<FSEntry> encrDirectoryFiles = EncrDirectory.GetEntries(SearchOption.AllDirectories).Where(EncrFilter).ToList();
            console?.WriteLine(VerbosityLevel.Diagnostic, 2, $"{encrDirectoryFiles.Count} files found");

            console?.WriteLine(VerbosityLevel.Diagnostic, 1, $"Enumerating Decr Directory...");
            console?.WriteLine(VerbosityLevel.Diagnostic, 2, $"Path: {DecrDirectory.FullName}");
            List<FSEntry> decrDirectoryFiles = DecrDirectory.GetEntries(SearchOption.AllDirectories).Where(DecrFilter).ToList();
            console?.WriteLine(VerbosityLevel.Diagnostic, 2, $"{decrDirectoryFiles.Count} files found");

            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Reading sync log (decr side)...");
            console?.WriteLine(VerbosityLevel.Diagnostic, 2, $"{SyncLog.Count()} entries found");
            //todo: filter out log entries where the decr file name and the encr file name does not match


            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Performing 3 way join...");
            List<ChangeBuilder> changes = FindChanges_St04_ThreeWayJoin(encrDirectoryFiles, decrDirectoryFiles, SyncLog, console).ToList();


            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Refreshing EncrHeaders...");
            FindChanges_St05_RefreshEncrHeaders(console, changes);


            //todo: detect conflicts

            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Adding Relationships...");
            FindChanges_St06_AddRelationships(changes);

#if DEBUG
            Debug.Assert(changes.All(c => c.RelationParents != null));
            Debug.Assert(changes.All(c => c.RelationChildren != null));
            Debug.Assert(changes.All(c => c.RelationCaseDifference != null));
#endif


            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Calculating Operation...");
            FindChanges_St07_CalculateOperation(changes);

            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Calculating Sync Mode...");
            FindChanges_St08_CalculateSyncMode(changes);

            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Calculating Dependencies...");
            FindChanges_St09_CalculateDependencies(changes);

            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Sorting...");
            changes = FindChanges_St10_Sort(rng, changes);



            return changes
                .Where(m => m.SyncMode != PreSyncMode.Unchanged)
                .Select(m => m.ToSyncEntry())
                .ToList();
        }



        /// <summary>
        /// Matches files from Encr, Decr and Log Entry
        /// </summary>
        private List<ChangeBuilder> FindChanges_St04_ThreeWayJoin(List<FSEntry> encrDirectoryFiles, List<FSEntry> decrDirectoryFiles, SyncLog syncLog, ConsoleEx console)
        {
            List<ChangeBuilder> preSyncDetails = new List<ChangeBuilder>();

            //Adds Logs
            console?.WriteLine(VerbosityLevel.Diagnostic, 2, "Merging in log...");
            preSyncDetails.AddRange(syncLog.Select(entry => new ChangeBuilder { LogEntry = entry }));
            console?.WriteLine(VerbosityLevel.Diagnostic, 3, $"{preSyncDetails.Count} added");

            //Updates/Adds Decrypted File Information
            console?.WriteLine(VerbosityLevel.Diagnostic, 2, "Merging in decrypted information...");
            int decrStatAdd = 0;
            int decrStatMerge = 0;
            var decrJoin = decrDirectoryFiles
                .GroupJoin(preSyncDetails,
                    o => o.RelativePath,
                    i => i?.LogEntry?.DecrFileName,
                    (o, i) => new Tuple<FSEntry, ChangeBuilder>(o, i.FirstOrDefault()));
            foreach (var entry in decrJoin.ToList())
            {
                if (entry.Item2 == null)
                {
                    //New Entry (not in log)
                    preSyncDetails.Add(new ChangeBuilder { DecrInfo = entry.Item1 });
                    decrStatAdd++;
                }
                else
                {
                    //Existing Entry (update only)
                    entry.Item2.DecrInfo = entry.Item1;
                    if (entry.Item1 != null)
                        decrStatMerge++;
                }
            }
            console?.WriteLine(VerbosityLevel.Diagnostic, 3, $"{decrStatAdd} added, {decrStatMerge} merged");

            //find encrypted file names
            foreach (var entry in preSyncDetails)
            {
                entry.DecrFileName = entry.LogEntry?.DecrFileName ?? entry.DecrInfo.RelativePath;
                entry.EncrFileName = FileNameEncoder.EncodeName(entry.DecrFileName);
            }


            //Updates/adds encrypted File Information
            console?.WriteLine(VerbosityLevel.Diagnostic, 2, "Merging in encrypted information...");
            int encrStatAdd = 0;
            int encrStatMerge = 0;
            var encrJoin = encrDirectoryFiles
                .GroupJoin(preSyncDetails,
                    o => o.RelativePath,
                    i => i.EncrFileName,
                    (o, i) => new Tuple<FSEntry, ChangeBuilder>(o, i.FirstOrDefault()));
            foreach (var entry in encrJoin.ToList())
            {
                if (entry.Item2 == null)
                {
                    //New Entry (not in log or decrypted file)
                    preSyncDetails.Add(new ChangeBuilder { EncrInfo = entry.Item1, EncrFileName = entry.Item1.RelativePath });
                    encrStatAdd++;
                }
                else
                {
                    //Existing Entry
                    entry.Item2.EncrInfo = entry.Item1;
                    if (entry.Item1 != null)
                        encrStatMerge++;
                }
            }
            console?.WriteLine(VerbosityLevel.Diagnostic, 3, $"{encrStatAdd} added, {encrStatMerge} merged");

            return preSyncDetails;
        }

        /// <summary>
        /// Loads the EncrHeaders for new and updated encripted files
        /// </summary>
        private void FindChanges_St05_RefreshEncrHeaders(ConsoleEx console, List<ChangeBuilder> matchesA)
        {
            int statsRefreshHeaderCount = 0;
            foreach (ChangeBuilder preSyncDetails in matchesA.Where(m => m.EncrInfo != null && m.EncrInfo.LastWriteTimeUtc != m.LogEntry?.EncrModified))
            {
                string encrFullPath = Path.Combine(EncrDirectory.FullName, HelixUtil.PathNative(preSyncDetails.EncrFileName));
                if (File.Exists(encrFullPath))
                {
                    preSyncDetails.EncrHeader = HelixFile.DecryptHeader(encrFullPath, this.DerivedBytesProvider);

                    //Updates the DecrFileName (if necessary)
                    if (string.IsNullOrEmpty(preSyncDetails.DecrFileName)
                        && FileNameEncoder.EncodeName(preSyncDetails.EncrHeader.FileName) == preSyncDetails.EncrInfo.RelativePath)
                    {
                        preSyncDetails.DecrFileName = preSyncDetails.EncrHeader.FileName;
                    }
                }

                statsRefreshHeaderCount++;
            }
            console?.WriteLine(VerbosityLevel.Diagnostic, 2, $"Updated {statsRefreshHeaderCount} headers");
        }

        /// <summary>
        /// Adds parents, children and files with case only differences
        /// </summary>
        private void FindChanges_St06_AddRelationships(List<ChangeBuilder> matchesA)
        {
            var indexedByParent = matchesA.GroupBy(m => Path.GetDirectoryName(m.DecrFileName)).ToDictionary(k => k.Key, k => k.ToList());
            var indexedByName = matchesA.GroupBy(m => m.DecrFileName).ToDictionary(k => k.Key, k => k.ToList());
            var indexedByUpperName = matchesA.GroupBy(m => m.DecrFileName.ToUpperInvariant()).ToDictionary(k => k.Key, k => k.ToList());

            foreach (var match in matchesA)
            {
                indexedByName.TryGetValue(Path.GetDirectoryName(match.DecrFileName), out var relationParents);
                match.RelationParents = relationParents ?? new List<ChangeBuilder>();

                indexedByUpperName.TryGetValue(match.DecrFileName.ToUpperInvariant(), out var relationCaseDifference);
                match.RelationCaseDifference = relationCaseDifference ?? new List<ChangeBuilder>();

                indexedByParent.TryGetValue(match.DecrFileName, out var relationChildren);
                match.RelationChildren = relationChildren ?? new List<ChangeBuilder>();
            }
        }


        private void FindChanges_St07_CalculateOperation(List<ChangeBuilder> changes)
        {
            foreach (var change in changes)
            {
                PreSyncOperation decrChange;
                PreSyncOperation encrChange;
                if (change.LogEntry == null || change.LogEntry.EntryType == FileEntryType.Removed || change.LogEntry.EntryType == FileEntryType.Purged)
                {
                    if (change.DecrInfo == null || change.DecrInfo.EntryType == FileEntryType.Removed)
                        decrChange = PreSyncOperation.None;
                    else
                        decrChange = PreSyncOperation.Add;
                }
                else
                {
                    if (change.DecrInfo == null || change.DecrInfo.EntryType == FileEntryType.Removed)
                    {
                        decrChange = PreSyncOperation.Remove;
                    }
                    else if (change.LogEntry.EntryType == change.DecrInfo.EntryType
                        && change.LogEntry.DecrFileName == change.DecrInfo.RelativePath
                        && change.LogEntry.DecrModified == change.DecrInfo.LastWriteTimeUtc)
                    {
                        decrChange = PreSyncOperation.None;
                    }
                    else
                    {
                        decrChange = PreSyncOperation.Change;
                    }
                }


                if (change.LogEntry?.EntryType == FileEntryType.Removed 
                    && change.EncrInfo == null)
                {
                    encrChange = PreSyncOperation.Purge;
                }

                //test if the encr info matches the log (this is the quicker method)
                else if ((change.LogEntry == null || change.LogEntry.EntryType == FileEntryType.Removed || change.LogEntry.EntryType == FileEntryType.Purged)
                    && (change.EncrInfo == null || change.EncrInfo.EntryType == FileEntryType.Removed || change.EncrInfo.EntryType == FileEntryType.Purged))
                {
                    encrChange = PreSyncOperation.None;
                }
                else if ((change.LogEntry != null && change.EncrInfo != null)
                 && (change.LogEntry.EncrModified == change.EncrInfo.LastWriteTimeUtc))
                { 
                        encrChange = PreSyncOperation.None;
                }

                //test if the header matches the log
                else if (change.LogEntry == null || change.LogEntry.EntryType == FileEntryType.Removed || change.LogEntry.EntryType == FileEntryType.Purged)
                {
                    if (change.EncrHeader.EntryType == FileEntryType.Removed || change.EncrHeader.EntryType == FileEntryType.Purged)
                        encrChange = PreSyncOperation.None;
                    else
                        encrChange = PreSyncOperation.Add;
                }

                //
                else if (change.EncrInfo == null)
                {
                    encrChange = PreSyncOperation.Purge;
                }

                else
                {
                    if (change.LogEntry.EntryType == change.EncrHeader.EntryType
                        && change.LogEntry.DecrFileName == change.EncrHeader.FileName
                        && change.LogEntry.DecrModified == change.EncrHeader.LastWriteTimeUtc)
                    {
                        encrChange = PreSyncOperation.None;
                    }
                    else if (change.EncrHeader.EntryType == FileEntryType.Removed)
                        encrChange= PreSyncOperation.Remove;
                    else
                        encrChange = PreSyncOperation.Change;
                }

                change.DecrChange = decrChange;
                change.EncrChange = encrChange;

            }
        }


        private static void FindChanges_St08_CalculateSyncMode(List<ChangeBuilder> changes)
        {
            foreach (var change in changes)
            {
                (bool changed, FileEntryType entryType, DateTime lastWriteTime, long lenght) encr = (change.EncrChange != PreSyncOperation.None, change.EncrHeader?.EntryType ?? FileEntryType.Purged, change.EncrHeader?.LastWriteTimeUtc ?? DateTime.MinValue, change.EncrHeader?.Length ?? 0);
                (bool changed, FileEntryType entryType, DateTime lastWriteTime, long lenght) decr = (change.DecrChange != PreSyncOperation.None, change.DecrInfo?.EntryType ?? FileEntryType.Removed, change.DecrInfo?.LastWriteTimeUtc ?? DateTime.MinValue, change.DecrInfo?.Length ?? 0);

                if (!encr.changed && !decr.changed)
                {
                    change.SyncMode = PreSyncMode.Unchanged;
                    continue;
                }

                if (encr == decr)
                {
                    change.SyncMode = PreSyncMode.Match;
                    continue;
                }

                if (decr.changed)
                {
                    if (decr.entryType == FileEntryType.File)
                    {
                        if (encr.changed)
                        {
                            change.SyncMode = PreSyncMode.Conflict;
                            change.Conflicts.Add(ConflictType.BothSidesChanged);
                            continue;
                        }
                        else
                        {
                            change.SyncMode = PreSyncMode.DecryptedSide;
                            continue;
                        }
                    }
                    else if (decr.entryType == FileEntryType.Directory)
                    {
                        if (encr.changed)
                        {
                            change.SyncMode = PreSyncMode.Conflict;
                            change.Conflicts.Add(ConflictType.BothSidesChanged);
                            continue;
                        }
                        else
                        {
                            change.SyncMode = PreSyncMode.DecryptedSide;
                            continue;
                        }
                    }
                    else if (decr.entryType == FileEntryType.Removed)
                    {
                        if (change.RelationChildren.Any(c =>
                                c.DecrChange == PreSyncOperation.Add
                                || c.DecrChange == PreSyncOperation.Change
                                || c.EncrChange == PreSyncOperation.Add
                                || c.EncrChange == PreSyncOperation.Change
                                ))
                        {
                            //forces a decript change so we can sync added files
                            change.SyncMode = PreSyncMode.DecryptedSide;
                            change.DecrChange = PreSyncOperation.Add;

                            change.Conflicts.Add(ConflictType.NonEmptyFolder);
                            continue;
                        }

                        change.SyncMode = PreSyncMode.DecryptedSide;
                        continue;
                    }

                    throw new NotImplementedException();
                }
                else if (encr.changed)
                {
                    if (encr.entryType == FileEntryType.File)
                    {
                        change.SyncMode = PreSyncMode.EncryptedSide;
                        continue;
                    }
                    else if (encr.entryType == FileEntryType.Directory)
                    {
                        change.SyncMode = PreSyncMode.EncryptedSide;
                        continue;
                    }
                    else if (encr.entryType == FileEntryType.Removed)
                    {
                        if (change.RelationChildren.Any(c =>
                                c.DecrChange == PreSyncOperation.Add
                                || c.DecrChange == PreSyncOperation.Change
                                || c.EncrChange == PreSyncOperation.Add
                                || c.EncrChange == PreSyncOperation.Change
                                ))
                        {
                            change.SyncMode = PreSyncMode.DecryptedSide;
                            change.DecrChange = PreSyncOperation.Add;
                            change.Conflicts.Add(ConflictType.NonEmptyFolder);
                            continue;
                        }

                        change.SyncMode = PreSyncMode.EncryptedSide;
                        continue;
                    }
                    else if (encr.entryType == FileEntryType.Purged)
                    {
                        //todo: add unit test for unexpected purged file
                        if (decr.entryType == FileEntryType.Removed)
                        {
                            change.SyncMode = PreSyncMode.EncryptedSide;
                            continue;
                        }

                        change.SyncMode = PreSyncMode.Conflict;
                        change.Conflicts.Add(ConflictType.UnexpectedPurge);
                        continue;
                    }


                    throw new NotImplementedException();
                }

            }
        }

        /// <summary>
        /// Adds the dependencies (pre-requisite for sort)
        /// </summary>
        private void FindChanges_St09_CalculateDependencies(List<ChangeBuilder> changes)
        {

            foreach (var change in changes)
            {
                HashSet<ChangeBuilder> dependencies = new HashSet<ChangeBuilder>();
                if (change.SyncMode == PreSyncMode.DecryptedSide || change.SyncMode == PreSyncMode.Conflict)
                {
                    if (change.DecrInfo == null || change.DecrInfo.EntryType == FileEntryType.Removed || change.DecrInfo.EntryType == FileEntryType.Purged)
                    {
                        change.RelationChildren?.ForEach(r => dependencies.Add(r));
                    }
                    else
                    {
                        change.RelationParents?.ForEach(r => dependencies.Add(r));
                        change.RelationCaseDifference?.Where(r => r != change && r.DecrChange == PreSyncOperation.Remove)
                            ?.ToList()?.ForEach(r => dependencies.Add(r));
                        change.RelationCaseDifference
                            ?.Where(r => r.DecrFileName.CompareTo(change.DecrFileName) < 0)
                            ?.ToList()?.ForEach(r => dependencies.Add(r));
                    }
                }


                if (change.SyncMode == PreSyncMode.EncryptedSide || change.SyncMode == PreSyncMode.Conflict)
                {
                    if (change.EncrHeader == null || change.EncrHeader.EntryType == FileEntryType.Removed || change.EncrHeader.EntryType == FileEntryType.Purged)
                    {
                        change.RelationChildren?.ForEach(r => dependencies.Add(r));
                    }
                    else
                    {
                        change.RelationParents?.ForEach(r => dependencies.Add(r));

                        change.RelationCaseDifference?.Where(r => r != change && r.EncrChange == PreSyncOperation.Remove)
                            ?.ToList()?.ForEach(r => dependencies.Add(r));
                        change.RelationCaseDifference
                            ?.Where(r => r.DecrFileName.CompareTo(change.DecrFileName) < 0)
                            ?.ToList()?.ForEach(r => dependencies.Add(r));
                    }
                }

                change.Dependencies = dependencies.ToList(); 
            }
        }

        /// <summary>
        /// Sorts the changes, readonly, ensuring proper dependency order
        /// </summary>
        private static List<ChangeBuilder> FindChanges_St10_Sort(RandomNumberGenerator rng, List<ChangeBuilder> matchesA)
        {
            List<ChangeBuilder> NoDependents = new List<ChangeBuilder>();
            Dictionary<ChangeBuilder, List<ChangeBuilder>> DependencyLookup = new Dictionary<ChangeBuilder, List<ChangeBuilder>>();
            Dictionary<ChangeBuilder, List<ChangeBuilder>> ReverseDependencyLookup = new Dictionary<ChangeBuilder, List<ChangeBuilder>>();
            foreach (var match in matchesA)
            {
                var dependencies = match.Dependencies;
                if (dependencies.Count == 0)
                {
                    NoDependents.Add(match);
                }
                else
                {
                    DependencyLookup.Add(match, dependencies.ToList());
                    foreach (var dependency in dependencies)
                    {
                        if (!ReverseDependencyLookup.TryAdd(dependency, new List<ChangeBuilder>() { match }))
                            ReverseDependencyLookup[dependency].Add(match);
                    }
                }
            }

            List<ChangeBuilder> outputList = new List<ChangeBuilder>();
            while (NoDependents.Count > 0)
            {

                byte[] rno = new byte[5];
                rng.GetBytes(rno);
                int randomvalue = (int)(BitConverter.ToUInt32(rno, 0) % (uint)NoDependents.Count);

                var next = NoDependents[randomvalue];
                NoDependents.RemoveAt(randomvalue);
                outputList.Add(next);

                //clear dependents, adds to the NoDependents list if possible
                if (ReverseDependencyLookup.TryGetValue(next, out var children))
                {
                    foreach (var child in children)
                    {
                        DependencyLookup[child].Remove(next);
                        if (DependencyLookup[child].Count == 0)
                        {
                            DependencyLookup.Remove(child);
                            NoDependents.Add(child);
                        }
                    }
                    ReverseDependencyLookup.Remove(next);
                }

            }

            if (ReverseDependencyLookup.Count > 0)
                throw new Exception("Unexpected circular reference found when sorting presyncdetails");

            return outputList;
        }


        
    }
}
