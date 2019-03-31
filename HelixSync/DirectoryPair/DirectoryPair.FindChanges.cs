using System;
using System.Collections.Generic;
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
        public List<PreSyncDetails> FindChanges(bool reset = true, ConsoleEx console = null)
        {//todo: disable default for reset
            console?.WriteLine(VerbosityLevel.Diagnostic, 0, "Finding Changes...");
            if (reset)
                Reset();

            var rng = RandomNumberGenerator.Create();

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
            List<PreSyncBuilder> changes = FindChanges_St4_ThreeWayJoin(encrDirectoryFiles, decrDirectoryFiles, SyncLog, console).ToList();


            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Refreshing EncrHeaders...");
            FindChanges_St5_RefreshEncrHeaders(console, changes);


            //todo: detect conflicts

            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Adding Relationships...");
            FindChanges_St6_AddRelationships(changes);


            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Calculating Operation...");
            FindChanges_St7_CalculateOperation(changes);

            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Calculating Dependencies...");
            FindChanges_St8_CalculateDependencies(changes);

            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Sorting...");
            changes = FindChanges_St9_Sort(rng, changes);


            return changes.Select(m => m.ToSyncEntry()).ToList();
        }



        /// <summary>
        /// Matches files from Encr, Decr and Log Entry
        /// </summary>
        private List<PreSyncBuilder> FindChanges_St4_ThreeWayJoin(List<FSEntry> encrDirectoryFiles, List<FSEntry> decrDirectoryFiles, SyncLog syncLog, ConsoleEx console)
        {
            List<PreSyncBuilder> preSyncDetails = new List<PreSyncBuilder>();

            //Adds Logs
            console?.WriteLine(VerbosityLevel.Diagnostic, 2, "Merging in log...");
            preSyncDetails.AddRange(syncLog.Select(entry => new PreSyncBuilder { LogEntry = entry }));
            console?.WriteLine(VerbosityLevel.Diagnostic, 3, $"{preSyncDetails.Count} added");

            //Updates/Adds Decrypted File Information
            console?.WriteLine(VerbosityLevel.Diagnostic, 2, "Merging in decrypted information...");
            int decrStatAdd = 0;
            int decrStatMerge = 0;
            var decrJoin = decrDirectoryFiles
                .GroupJoin(preSyncDetails,
                    o => o.RelativePath,
                    i => i?.LogEntry?.DecrFileName,
                    (o, i) => new Tuple<FSEntry, PreSyncBuilder>(o, i.FirstOrDefault()));
            foreach (var entry in decrJoin.ToList())
            {
                if (entry.Item2 == null)
                {
                    //New Entry (not in log)
                    preSyncDetails.Add(new PreSyncBuilder { DecrInfo = entry.Item1 });
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
                    (o, i) => new Tuple<FSEntry, PreSyncBuilder>(o, i.FirstOrDefault()));
            foreach (var entry in encrJoin.ToList())
            {
                if (entry.Item2 == null)
                {
                    //New Entry (not in log or decrypted file)
                    preSyncDetails.Add(new PreSyncBuilder { EncrInfo = entry.Item1, EncrFileName = entry.Item1.RelativePath });
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
        private void FindChanges_St5_RefreshEncrHeaders(ConsoleEx console, List<PreSyncBuilder> matchesA)
        {
            int statsRefreshHeaderCount = 0;
            foreach (PreSyncBuilder preSyncDetails in matchesA.Where(m => m.EncrInfo != null && m.EncrInfo.LastWriteTimeUtc != m.LogEntry?.EncrModified))
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
        private void FindChanges_St6_AddRelationships(List<PreSyncBuilder> matchesA)
        {
            var indexedByParent = matchesA.GroupBy(m => Path.GetDirectoryName(m.DecrFileName)).ToDictionary(k => k.Key, k => k.ToList());
            var indexedByName = matchesA.GroupBy(m => m.DecrFileName).ToDictionary(k => k.Key, k => k.ToList());
            var indexedByUpperName = matchesA.GroupBy(m => m.DecrFileName.ToUpperInvariant()).ToDictionary(k => k.Key, k => k.ToList());

            foreach (var match in matchesA)
            {
                indexedByName.TryGetValue(Path.GetDirectoryName(match.DecrFileName), out var relationParents);
                match.RelationParents = relationParents;

                indexedByUpperName.TryGetValue(match.DecrFileName.ToUpperInvariant(), out var relationCaseDifference);
                match.RelationCaseDifference = relationCaseDifference;

                indexedByParent.TryGetValue(match.DecrFileName, out var relationChildren);
                match.RelationChildren = relationChildren;
            }
        }


        private void FindChanges_St7_CalculateOperation(List<PreSyncBuilder> changes)
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


                //test if the encr info matches the log (this is the quicker method)
                if ((change.LogEntry == null || change.LogEntry.EntryType == FileEntryType.Removed || change.LogEntry.EntryType == FileEntryType.Purged)
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

        /// <summary>
        /// Adds the dependencies (pre-requisite for sort)
        /// </summary>
        private void FindChanges_St8_CalculateDependencies(List<PreSyncBuilder> changes)
        {

            foreach (var change in changes)
            {
                HashSet<PreSyncBuilder> dependencies = new HashSet<PreSyncBuilder>();
                if (change.DecrChange != PreSyncOperation.None)
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


                if (change.EncrChange != PreSyncOperation.None)
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
        private static List<PreSyncBuilder> FindChanges_St9_Sort(RandomNumberGenerator rng, List<PreSyncBuilder> matchesA)
        {
            List<PreSyncBuilder> NoDependents = new List<PreSyncBuilder>();
            Dictionary<PreSyncBuilder, List<PreSyncBuilder>> DependencyLookup = new Dictionary<PreSyncBuilder, List<PreSyncBuilder>>();
            Dictionary<PreSyncBuilder, List<PreSyncBuilder>> ReverseDependencyLookup = new Dictionary<PreSyncBuilder, List<PreSyncBuilder>>();
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
                        if (!ReverseDependencyLookup.TryAdd(dependency, new List<PreSyncBuilder>() { match }))
                            ReverseDependencyLookup[dependency].Add(match);
                    }
                }
            }

            List<PreSyncBuilder> outputList = new List<PreSyncBuilder>();
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
