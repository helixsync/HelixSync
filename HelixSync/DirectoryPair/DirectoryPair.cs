using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HelixSync.FileSystem;
using HelixSync.HelixDirectory;

namespace HelixSync
{
    public class DirectoryPair : IDisposable
    {
        public FSDirectory DecrDirectory { get; }
        public FSDirectory EncrDirectory { get; }
        public DerivedBytesProvider DerivedBytesProvider { get; }
        public bool WhatIf { get; }

        private DirectoryHeader Header;
        private string SyncLogPath => Path.Combine(HelixConsts.SyncLogDirectory, Header.DirectoryId + HelixConsts.SyncLogExtention);

        public SyncLog SyncLog { get; private set; }
        public FileNameEncoder FileNameEncoder { get; private set; }

        public DirectoryPair(string decrDirectory, string encrDirectory, DerivedBytesProvider derivedBytesProvider, bool whatIf)
        {
            this.DecrDirectory = new FSDirectory(new DirectoryInfo(decrDirectory).FullName, whatIf);
            this.EncrDirectory = new FSDirectory(new DirectoryInfo(encrDirectory).FullName, whatIf);
            this.DerivedBytesProvider = derivedBytesProvider;
            this.WhatIf = whatIf;
        }

        public void PreInitializationCheck()
        {
            if (!Directory.Exists(EncrDirectory.FullName)
                && !Directory.Exists(Path.GetDirectoryName(EncrDirectory.FullName)))
            {
                throw new Exception("Encrypted directory (and parent) does not exist");
            }

            if (Directory.Exists(EncrDirectory.FullName)
                && !EncrDirectory.ChildExists(HelixConsts.HeaderFileName)
                && EncrDirectory.GetEntries().Any())
            {
                throw new Exception("Encrypted directory is not a valid HelixSync directory");
            }


            if (!Directory.Exists(DecrDirectory.FullName)
                && !Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(DecrDirectory.FullName))))
            {
                throw new Exception("Decrypted directory (and parent) does not exist");
            }
        }

        public bool InitializeFullNeeded()
        {
            return !EncrDirectory.ChildExists(HelixConsts.HeaderFileName);
        }

        public bool InitializeMergeWarning()
        {
            return DecrDirectory.Exists && DecrDirectory.GetEntries(SearchOption.TopDirectoryOnly).Any();
        }

        public void InitializeFull(ConsoleEx consoleEx, HelixFileVersion fileVersion = null)
        {

            PreInitializationCheck();

            //Initialize Encr Directory
            consoleEx.WriteLine(VerbosityLevel.Detailed, 0, "Initializing Encrypted Directory...");
            Header = DirectoryHeader.New();
            EncrDirectory.Create();
            if (WhatIf)
            {
                EncrDirectory.WhatIfAddFile(HelixConsts.HeaderFileName, 10);
            }
            else
            {
                Header.Save(EncrDirectory.PathFull(HelixConsts.HeaderFileName), DerivedBytesProvider, fileVersion);
                EncrDirectory.RefreshEntry(HelixConsts.HeaderFileName);
            }

            this.FileNameEncoder = new FileNameEncoder(Header.FileNameKey);
            consoleEx.WriteLine(VerbosityLevel.Detailed, 1, "Encrypted Directory Initialized (" + Header.DirectoryId.Substring(0, 6) + "...)");


            InitializeDecr(consoleEx);
        }

        public void OpenEncr(ConsoleEx consoleEx)
        {
            if (Header == null)
            {
                consoleEx.WriteLine(VerbosityLevel.Detailed, 0, "Opening Encrypted Directory...");
                Header = DirectoryHeader.Load(EncrDirectory.PathFull(HelixConsts.HeaderFileName), DerivedBytesProvider);
                consoleEx.WriteLine(VerbosityLevel.Detailed, 0, "Opened Encrypted Directory (" + Header.DirectoryId.Substring(0, 6) + "...)");
            }
            this.FileNameEncoder = new FileNameEncoder(Header.FileNameKey);
        }

        public bool InitializeDecrNeeded()
        {
            if (Header == null)
                throw new InvalidOperationException("OpenEncr() must be called first");

            return !DecrDirectory.ChildExists(SyncLogPath);
        }

        public void InitializeDecr(ConsoleEx consoleEx)
        {
            consoleEx.WriteLine(VerbosityLevel.Detailed, 0, "Initializing Decrypted Directory...");
            //Initialize Decr Directory
            DecrDirectory.Create();
            DecrDirectory.CreateDirectory(HelixConsts.SyncLogDirectory);
            if (WhatIf)
            {
                DecrDirectory.WhatIfAddFile(SyncLogPath, 10);
            }
            else
            {
                using (var stream = File.CreateText(DecrDirectory.PathFull(SyncLogPath)))
                {
                    stream.WriteLine(HelixConsts.SyncLogHeader);
                }
            }
            consoleEx.WriteLine(VerbosityLevel.Detailed, 1, "Decrypted Directory Initialized");
        }

        public void OpenDecr(ConsoleEx consoleEx)
        {
            SyncLog = SyncLog.GetLog(DecrDirectory.PathFull(SyncLogPath), WhatIf);
        }

        public void Cleanup(ConsoleEx consoleEx)
        {
            EncrDirectory.Cleanup(consoleEx);
            DecrDirectory.Cleanup(consoleEx);
        }

        public void Reset()
        {
            EncrDirectory.Reset();
            SyncLog.Reload();
            DecrDirectory.Reset();
        }


        /// <summary>
        /// Returns a list of changes that need to be performed as part of the sync.
        /// </summary>
        public List<PreSyncDetails> FindChanges(bool reset = true, ConsoleEx console = null)
        {//todo: disable default for reset
            if (reset)
                Reset();

            var rng = RandomNumberGenerator.Create();

            List<PreSyncDetails> matches = MatchFiles(console)
                .Where(m => m.SyncMode != PreSyncMode.Unchanged)
                .ToList();

            //Fills in the EncrHeader
            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Refreshing EncrHeaders...");
            int statsRefreshHeaderCount = 0;
            foreach (PreSyncDetails match in matches.Where(m => m.EncrInfo != null))
            {
                RefreshPreSyncEncrHeader(match);
                RefreshPreSyncMode(match);
                statsRefreshHeaderCount++;
            }
            console?.WriteLine(VerbosityLevel.Diagnostic, 2, $"Updated {statsRefreshHeaderCount} headers");

            //todo: reorder based on dependencies
            //todo: detect conflicts

            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Sorting...");
            return Sort(matches);

            //return matches.OrderBy(m =>
            //    {
            //        byte[] rno = new byte[5];
            //        rng.GetBytes(rno);
            //        int randomvalue = BitConverter.ToInt32(rno, 0);
            //        return randomvalue;
            //    })
            //    .ToList();
        }


        /// <summary>
        /// Matches files from Encr, Decr and Log Entry
        /// </summary>
        private List<PreSyncDetails> MatchFiles(ConsoleEx console)
        {
            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Enumerating Encr Directory...");
            List<FSEntry> encrDirectoryFiles = EncrDirectory.GetEntries(SearchOption.AllDirectories).Where(EncrFilter).ToList();
            console?.WriteLine(VerbosityLevel.Diagnostic, 2, $"{encrDirectoryFiles.Count} files found");

            console?.WriteLine(VerbosityLevel.Diagnostic, 1, $"Enumerating Decr Directory...");
            console?.WriteLine(VerbosityLevel.Diagnostic, 2, $"Path: {DecrDirectory.FullName}");
            List<FSEntry> decrDirectoryFiles = DecrDirectory.GetEntries(SearchOption.AllDirectories).Where(DecrFilter).ToList();
            console?.WriteLine(VerbosityLevel.Diagnostic, 2, $"{decrDirectoryFiles.Count} files found");

            //todo: filter out log entries where the decr file name and the encr file name does not match
            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Reading sync log (decr side)...");
            console?.WriteLine(VerbosityLevel.Diagnostic, 2, $"{SyncLog.Count()} entries found");
            List<PreSyncDetails> preSyncDetails = new List<PreSyncDetails>();

            console?.WriteLine(VerbosityLevel.Diagnostic, 1, "Performing 3 way compare...");

            //Adds Logs
            console?.WriteLine(VerbosityLevel.Diagnostic, 2, "Merging in log...");
            preSyncDetails.AddRange(SyncLog.Select(entry => new PreSyncDetails { LogEntry = entry }));
            console?.WriteLine(VerbosityLevel.Diagnostic, 3, $"{preSyncDetails.Count} added");

            //Updates/Adds Decrypted File Information
            console?.WriteLine(VerbosityLevel.Diagnostic, 2, "Merging in decrypted information...");
            int decrStatAdd = 0;
            int decrStatMerge = 0;
            var decrJoin = decrDirectoryFiles
                .GroupJoin(preSyncDetails,
                    o => o.RelativePath,
                    i => i?.LogEntry?.DecrFileName,
                    (o, i) => new Tuple<FSEntry, PreSyncDetails>(o, i.FirstOrDefault()));
            foreach (var entry in decrJoin.ToList())
            {
                if (entry.Item2 == null)
                {
                    //New Entry (not in log)
                    preSyncDetails.Add(new HelixSync.PreSyncDetails { DecrInfo = entry.Item1 });
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
                    (o, i) => new Tuple<FSEntry, PreSyncDetails>(o, i.FirstOrDefault()));
            foreach (var entry in encrJoin.ToList())
            {
                if (entry.Item2 == null)
                {
                    //New Entry (not in log or decrypted file)
                    preSyncDetails.Add(new PreSyncDetails { EncrInfo = entry.Item1, EncrFileName = entry.Item1.RelativePath });
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

            foreach (PreSyncDetails entry in preSyncDetails)
                RefreshPreSyncMode(entry);

            return preSyncDetails;
        }

        private void RefreshPreSyncMode(PreSyncDetails preSyncDetails)
        {
            if (preSyncDetails == null)
                throw new ArgumentNullException(nameof(preSyncDetails));

            var LogEntry = preSyncDetails.LogEntry;
            var DecrInfo = preSyncDetails.DecrInfo;
            var EncrInfo = preSyncDetails.EncrInfo;
            var decrType = preSyncDetails.DecrInfo?.EntryType ?? FileEntryType.Removed;
            var encrType = preSyncDetails.EncrInfo?.EntryType ?? FileEntryType.Removed;

            bool decrChanged;
            if (LogEntry == null && DecrInfo == null)
            {
                decrChanged = false;
            }
            else if (LogEntry == null && decrType == FileEntryType.Removed)
            {
                decrChanged = false;
            }
            else if (LogEntry == null && decrType != FileEntryType.Removed)
            {
                decrChanged = true;
            }
            else if (LogEntry.EntryType == FileEntryType.Removed && decrType == FileEntryType.Removed)
            {
                decrChanged = false;
            }
            else if (LogEntry.EntryType == decrType
                && LogEntry.DecrFileName == DecrInfo.RelativePath
                && LogEntry.DecrModified == DecrInfo.LastWriteTimeUtc)
            {
                decrChanged = false;
            }
            else
            {
                decrChanged = true;
            }


            bool encrChanged = true;
            if (LogEntry == null && EncrInfo == null)
            {
                encrChanged = false;
            }
            else if (LogEntry == null && encrType == FileEntryType.Removed)
            {
                encrChanged = false;
            }
            else if (LogEntry == null && encrType != FileEntryType.Removed)
            {
                encrChanged = true;
            }
            else if (EncrInfo == null && LogEntry.EntryType == FileEntryType.Removed)
            {
                //encrChanged to purge
                encrChanged = true;
            }
            else if (EncrInfo == null)
            {
                throw new NotImplementedException($"EncrInfo is null. {preSyncDetails.DiagnosticString()}\ndecrChanged: {decrChanged}");
            }
            else if (LogEntry.EncrFileName == EncrInfo.RelativePath
                && LogEntry.EncrModified == EncrInfo.LastWriteTimeUtc)
            {
                encrChanged = false;
            }
            else
            {
                encrChanged = true;
            }

            //todo: detect orphans
            //todo: detect case-only conflicts

            if (encrChanged == false && decrChanged == false)
            {
                preSyncDetails.SyncMode = PreSyncMode.Unchanged;
            }
            else if (encrChanged == true && decrChanged == true)
            {
                if ((preSyncDetails.DecrInfo?.EntryType ?? FileEntryType.Removed) == FileEntryType.Removed
                    && preSyncDetails.EncrHeader == null)
                {
                    preSyncDetails.SyncMode = PreSyncMode.Match;
                }
                else if (preSyncDetails.DecrInfo?.LastWriteTimeUtc == preSyncDetails.EncrHeader?.LastWriteTimeUtc
                    && (preSyncDetails.DecrInfo?.EntryType ?? FileEntryType.Removed) == preSyncDetails.EncrHeader?.EntryType)
                {
                    preSyncDetails.SyncMode = PreSyncMode.Match; //Both changed however still match
                }
                else
                {
                    preSyncDetails.SyncMode = PreSyncMode.Conflict; //Both changed however one is in conflict
                }
            }
            else if (encrChanged)
            {
                preSyncDetails.SyncMode = PreSyncMode.EncryptedSide;

            }
            else if (decrChanged)
            {
                preSyncDetails.SyncMode = PreSyncMode.DecryptedSide;
            }
            else
            {
                preSyncDetails.SyncMode = PreSyncMode.Unknown;
            }



            if (preSyncDetails.SyncMode == PreSyncMode.DecryptedSide)
            {
                preSyncDetails.DisplayEntryType = preSyncDetails?.DecrInfo?.EntryType ?? FileEntryType.Removed;
                preSyncDetails.DisplayFileLength = preSyncDetails.DecrInfo?.Length ?? preSyncDetails.EncrHeader?.Length ?? 0;

                if (preSyncDetails.DecrInfo == null || preSyncDetails.DecrInfo.EntryType == FileEntryType.Removed)
                    preSyncDetails.DisplayOperation = PreSyncOperation.Remove;
                else if (preSyncDetails.EncrInfo == null || preSyncDetails.EncrInfo.EntryType == FileEntryType.Removed)
                    preSyncDetails.DisplayOperation = PreSyncOperation.Add;
                else if (preSyncDetails.EncrHeader?.EntryType == FileEntryType.Removed)
                    preSyncDetails.DisplayOperation = PreSyncOperation.Add;
                else
                    preSyncDetails.DisplayOperation = PreSyncOperation.Change;
            }
            else if (preSyncDetails.SyncMode == PreSyncMode.EncryptedSide)
            {
                preSyncDetails.DisplayEntryType = preSyncDetails.EncrHeader?.EntryType ?? FileEntryType.Removed;
                preSyncDetails.DisplayFileLength = preSyncDetails.EncrHeader?.Length ?? preSyncDetails.DecrInfo?.Length ?? 0;

                if ((preSyncDetails.EncrInfo?.EntryType == FileEntryType.Removed || preSyncDetails.EncrInfo?.EntryType == null)
                     && preSyncDetails.LogEntry.EntryType == FileEntryType.Removed)
                {
                    preSyncDetails.DisplayEntryType = FileEntryType.Purged;
                    preSyncDetails.DisplayFileLength = 0;
                    preSyncDetails.DisplayOperation = PreSyncOperation.Purge;
                }
                else if (preSyncDetails.EncrInfo == null || preSyncDetails.EncrInfo.EntryType == FileEntryType.Removed)
                    preSyncDetails.DisplayOperation = PreSyncOperation.Remove;
                else if (preSyncDetails.EncrHeader?.EntryType == FileEntryType.Removed)
                    preSyncDetails.DisplayOperation = PreSyncOperation.Remove;
                else if (preSyncDetails.DecrInfo == null || preSyncDetails.DecrInfo.EntryType == FileEntryType.Removed)
                    preSyncDetails.DisplayOperation = PreSyncOperation.Add;
                else
                    preSyncDetails.DisplayOperation = PreSyncOperation.Change;
            }
            else if (preSyncDetails.SyncMode == PreSyncMode.Match || preSyncDetails.SyncMode == PreSyncMode.Unchanged)
            {
                preSyncDetails.DisplayEntryType = preSyncDetails.DecrInfo?.EntryType ?? FileEntryType.Removed;
                preSyncDetails.DisplayFileLength = preSyncDetails.DecrInfo?.Length ?? preSyncDetails.EncrHeader?.Length ?? 0;

                preSyncDetails.DisplayOperation = PreSyncOperation.None;
            }
            else
            {
                preSyncDetails.DisplayEntryType = preSyncDetails.DecrInfo?.EntryType ?? FileEntryType.Removed;
                preSyncDetails.DisplayFileLength = preSyncDetails.DecrInfo?.Length ?? preSyncDetails.EncrHeader?.Length ?? 0;

                preSyncDetails.DisplayOperation = PreSyncOperation.Error;
            }

        }

        private void RefreshPreSyncEncrHeader(PreSyncDetails preSyncDetails)
        {
            if (preSyncDetails == null)
                throw new ArgumentNullException(nameof(preSyncDetails));

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
        }

        private List<PreSyncDetails> Sort(List<PreSyncDetails> list)
        {
            Dictionary<string, List<PreSyncDetails>> fileNameIndex = new Dictionary<string, List<PreSyncDetails>>();
            Dictionary<string, List<PreSyncDetails>> fileNameUpperIndex = new Dictionary<string, List<PreSyncDetails>>();
            Dictionary<string, List<PreSyncDetails>> fileNameParentIndex = new Dictionary<string, List<PreSyncDetails>>();
            foreach (var item in list)
            {
                var fileName = item.DecrFileName;
                var fileNameUpper = item.DecrFileName?.ToUpperInvariant();
                var parent = Path.GetDirectoryName(item.DecrFileName);

                if (string.IsNullOrEmpty(item.DecrFileName))
                    throw new Exception("DecrFileName null, " + item.DiagnosticString());

                if (fileNameIndex.ContainsKey(fileName))
                    fileNameIndex[fileName].Add(item);
                else
                    fileNameIndex.Add(fileName, new List<PreSyncDetails>() { item });

                if (fileNameUpperIndex.ContainsKey(fileNameUpper))
                    fileNameUpperIndex[fileNameUpper].Add(item);
                else
                    fileNameUpperIndex.Add(fileNameUpper, new List<PreSyncDetails>() { item });

                if (!string.IsNullOrEmpty(parent))
                {
                    if (fileNameParentIndex.ContainsKey(parent))
                        fileNameParentIndex[parent].Add(item);
                    else
                        fileNameParentIndex.Add(parent, new List<PreSyncDetails>() { item });
                }
            }

            Dictionary<PreSyncDetails, List<PreSyncDetails>> DependChildToParent = new Dictionary<PreSyncDetails, List<PreSyncDetails>>();
            Dictionary<PreSyncDetails, List<PreSyncDetails>> DependParentToChild = new Dictionary<PreSyncDetails, List<PreSyncDetails>>();
            List<PreSyncDetails> NoDependents = new List<PreSyncDetails>();

            void AddDependent(PreSyncDetails child, PreSyncDetails parent)
            {
                if (!DependChildToParent.ContainsKey(child))
                    DependChildToParent.Add(child, new List<PreSyncDetails>());
                if (!DependParentToChild.ContainsKey(parent))
                    DependParentToChild.Add(parent, new List<PreSyncDetails>());

                DependParentToChild[parent].Add(child);
                DependChildToParent[child].Add(parent);
            }

            foreach (var item in list)
            {
                if (item.DisplayOperation == PreSyncOperation.Add)
                {
                    { //adds parent directory (adds) if exist
                        string parentDirectory = Path.GetDirectoryName(item.DecrFileName);
                        if (!string.IsNullOrEmpty(parentDirectory))
                        {
                            if (fileNameIndex.TryGetValue(parentDirectory, out var parents))
                            {
                                foreach (var parent in parents)
                                {
                                    if (parent.DisplayOperation == PreSyncOperation.Add)
                                        AddDependent(item, parent);
                                }
                            }
                        }
                    }


                    {   //adds same case deletes (if exists)
                        if (fileNameUpperIndex.TryGetValue(item.DecrFileName.ToUpperInvariant(), out var parents))
                        {
                            foreach (var parent in parents)
                            {
                                if (parent.DisplayOperation == PreSyncOperation.Remove)
                                    AddDependent(item, parent);
                            }
                        }
                    }

                    if (!DependChildToParent.ContainsKey(item))
                        NoDependents.Add(item);
                }
                else if (item.DisplayOperation == PreSyncOperation.Remove)
                {
                    if (fileNameParentIndex.TryGetValue(item.DecrFileName, out var childFiles))
                    {
                        foreach (var childFile in childFiles)
                        {
                            if (childFile.DisplayOperation == PreSyncOperation.Remove)
                            {
                                //removal of this directory is depended on the removal of the child directory
                                AddDependent(item, childFile);
                            }
                        }
                    }

                    if (!DependChildToParent.ContainsKey(item))
                        NoDependents.Add(item);
                }
                else
                {
                    NoDependents.Add(item);
                }
            }

            var rng = RandomNumberGenerator.Create();

            List<PreSyncDetails> outputList = new List<PreSyncDetails>();
            while (NoDependents.Count > 0)
            {

                byte[] rno = new byte[5];
                rng.GetBytes(rno);
                int randomvalue = (int)(BitConverter.ToUInt32(rno, 0) % (uint)NoDependents.Count);

                var next = NoDependents[randomvalue];
                NoDependents.RemoveAt(randomvalue);
                outputList.Add(next);

                //clear dependents, adds to the NoDependents list if possible
                if (DependParentToChild.TryGetValue(next, out var children))
                {
                    foreach (var child in children)
                    {
                        DependChildToParent[child].Remove(next);
                        if (DependChildToParent[child].Count == 0)
                        {
                            DependChildToParent.Remove(child);
                            NoDependents.Add(child);
                        }
                    }
                    DependParentToChild.Remove(next);
                }

            }

            if (DependChildToParent.Count > 0)
                throw new Exception("Unexpected circular reference found when sorting presyncdetails");

            return outputList;
        }

        /// <summary>
        /// Refreshes the contents of the preSyncDetails using the file system (in case the file system has changed since last retrieved)
        /// </summary>
        public void RefreshPreSyncDetails(PreSyncDetails preSyncDetails)
        {
            if (preSyncDetails == null)
                throw new ArgumentNullException(nameof(preSyncDetails));
            if (string.IsNullOrEmpty(preSyncDetails.EncrFileName) && string.IsNullOrEmpty(preSyncDetails.DecrFileName))
                throw new ArgumentException("EncrFileName or DecrFileName must be included with preSyncDetails", nameof(preSyncDetails));

            if (string.IsNullOrEmpty(preSyncDetails.EncrFileName))
                preSyncDetails.EncrFileName = FileNameEncoder.EncodeName(preSyncDetails.DecrFileName);

            preSyncDetails.EncrInfo = EncrDirectory.TryGetEntry(preSyncDetails.EncrFileName);
            RefreshPreSyncEncrHeader(preSyncDetails);

            if (!string.IsNullOrEmpty(preSyncDetails.DecrFileName))
                preSyncDetails.DecrInfo = DecrDirectory.TryGetEntry(preSyncDetails.DecrFileName);

            preSyncDetails.LogEntry = SyncLog.FindByDecrFileName(preSyncDetails.DecrFileName);

            RefreshPreSyncMode(preSyncDetails);
        }


        public SyncResults TrySync(PreSyncDetails entry, ConsoleEx console = null)
        {
            const int encrTimespanPrecisionMS = 1000;

            //todo: ensure the entry direction and operation end up being the same
            //todo: ensure all the calling methods do something with the results

            //if (WhatIf)
            //    throw new InvalidOperationException("Unable to perform sync when WhatIf mode is set to true");
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (entry.SyncMode == PreSyncMode.DecryptedSide)
            {
                SyncLogEntry logEntry = entry.LogEntry;


                string encrPath = Path.Combine(EncrDirectory.FullName, HelixUtil.PathNative(entry.EncrFileName));
                string decrPath = Path.Combine(DecrDirectory.FullName, HelixUtil.PathNative(entry.DecrFileName));
                FileEncryptOptions options = new FileEncryptOptions();
                FileEntry header = null;
                options.BeforeWriteHeader = (h) => header = h;
                options.StoredFileName = entry.DecrFileName;
                options.FileVersion = Header.FileVersion;
                options.Log = (s) => console?.WriteLine(VerbosityLevel.Diagnostic, 1, s);

                if (WhatIf)
                {
                    EncrDirectory.WhatIfAddFile(encrPath, entry.DisplayFileLength);
                    header = entry.DecrInfo.ToFileEntry();
                }
                else
                {
                    HelixFile.Encrypt(decrPath, encrPath, DerivedBytesProvider, options);


                    //forces a change if the file was modified to quickly
                    if (logEntry != null && (File.GetLastWriteTimeUtc(encrPath) - logEntry.EncrModified).TotalMilliseconds < encrTimespanPrecisionMS)
                    {
                        File.SetLastWriteTimeUtc(encrPath, logEntry.EncrModified + TimeSpan.FromMilliseconds(encrTimespanPrecisionMS));
                    }

                    EncrDirectory.RefreshEntry(encrPath);
                }
                var newLogEntry = CreateEntryFromHeader(header, EncrDirectory.TryGetEntry(entry.EncrFileName));
                SyncLog.Add(newLogEntry);


                return SyncResults.Success();
            }
            else if (entry.SyncMode == PreSyncMode.EncryptedSide)
            {
                if (entry.DisplayOperation == PreSyncOperation.Purge)
                {
                    SyncLog.Add(entry.GetUpdatedLogEntry());
                    return SyncResults.Success();
                }
                else
                {
                    //todo: use the DisplayOperation to determine what to do (ensures the counts stay consistant)

                    SyncLogEntry logEntry = entry.LogEntry;
                    SyncLogEntry fileSystemEntry = CreateNewLogEntryFromEncrPath(entry.EncrFileName);
                    if (logEntry?.ToString() == fileSystemEntry?.ToString())
                        return SyncResults.Success(); //Unchanged

                    //todo: test to see if there are illegal characters
                    //todo: check if the name matches

                    string encrPath = HelixUtil.JoinNative(EncrDirectory.FullName, fileSystemEntry.EncrFileName);
                    string decrPath = HelixUtil.JoinNative(DecrDirectory.FullName, fileSystemEntry.DecrFileName);

                    //todo: if file exists with different case - skip file
                    var exactPath = HelixUtil.GetExactPathName(decrPath);
                    FSEntry decrEntry = DecrDirectory.TryGetEntry(fileSystemEntry.DecrFileName);

                    if (decrEntry != null && decrEntry.RelativePath != fileSystemEntry.DecrFileName)
                    {
                        //todo: throw more specific exception
                        return SyncResults.Failure(new HelixException($"Case only conflict file \"{decrPath}\" exists as \"{exactPath}\"."));
                    }

                    if (WhatIf)
                    {
                        throw new NotImplementedException(); //todo: implment
                    }
                    else
                    {
                        HelixFile.Decrypt(encrPath, decrPath, DerivedBytesProvider);
                        //todo: get the date on the file system (needed if the filesystem has less precision 
                        DecrDirectory.RefreshEntry(decrPath);
                    }

                    SyncLog.Add(fileSystemEntry);

                    return SyncResults.Success();
                }
            }
            else if (entry.SyncMode == PreSyncMode.Match || entry.SyncMode == PreSyncMode.Unchanged)
            {
                //Add to Log file (changed to be equal on both sides)
                SyncLogEntry fileSystemEntry = CreateNewLogEntryFromDecrPath(entry.DecrFileName);
                SyncLog.Add(fileSystemEntry);
                return SyncResults.Success();
            }

            return SyncResults.Failure(new HelixException($"Invalid sync mode {entry.SyncMode}"));
        }


        public SyncLogEntry CreateEntryFromHeader(FileEntry decrFileInfo, FSEntry encrFileInfo)
        {
            if (decrFileInfo == null)
                throw new ArgumentNullException(nameof(decrFileInfo));
            if (encrFileInfo == null)
                throw new ArgumentNullException(nameof(encrFileInfo));

            SyncLogEntry entry = new SyncLogEntry(decrFileInfo.EntryType, decrFileInfo.FileName, decrFileInfo.LastWriteTimeUtc, encrFileInfo.RelativePath, encrFileInfo.LastWriteTimeUtc);
            return entry;
        }
        public SyncLogEntry CreateNewLogEntryFromDecrPath(string decrFileName)
        {
            FSEntry decrEntry = DecrDirectory.TryGetEntry(decrFileName);

            string encrFileName = FileNameEncoder.EncodeName(decrFileName);
            FSEntry encrEntry = EncrDirectory.TryGetEntry(encrFileName);

            return new SyncLogEntry(decrEntry.EntryType, decrEntry.RelativePath, decrEntry.LastWriteTimeUtc, encrEntry.RelativePath, encrEntry.LastWriteTimeUtc);
        }
        public SyncLogEntry CreateNewLogEntryFromEncrPath(string encrFileName)
        {
            string encrFilePath = Path.Combine(EncrDirectory.FullName, encrFileName);

            FSEntry encrEntry = EncrDirectory.TryGetEntry(encrFileName);

            FileEntry header = HelixFile.DecryptHeader(encrFilePath, DerivedBytesProvider);
            var decrFileName = header.FileName;
            if (encrFileName != FileNameEncoder.EncodeName(decrFileName))
                throw new HelixException("Encrypted file name does not match"); //todo: prompt for action

            return new SyncLogEntry(header.EntryType, header.FileName, header.LastWriteTimeUtc, encrEntry.RelativePath, encrEntry.LastWriteTimeUtc);
        }


        public bool EncrFilter(FSEntry entry)
        {
            string fileName = entry.RelativePath;
            if (string.Equals(fileName, HelixConsts.HeaderFileName))
                return false;
            if (string.Equals(fileName, HelixConsts.SyncLogDirectory))
                return false;
            if (fileName.StartsWith(HelixConsts.SyncLogDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                return false;
            if (Path.GetExtension(fileName) == HelixConsts.StagedHxExtention)
                return false;
            if (Path.GetExtension(fileName) == HelixConsts.BackupExtention)
                return false;
            if (entry.EntryType != FileEntryType.File)
                return false;

            return true;
        }

        public bool DecrFilter(FSEntry entry)
        {
            string fileName = entry.RelativePath;

            if (string.Equals(fileName, HelixConsts.SyncLogDirectory))
                return false;
            if (fileName.StartsWith(HelixConsts.SyncLogDirectory + HelixUtil.UniversalDirectorySeparatorChar, StringComparison.Ordinal))
                return false;

            return true;
        }


        public static DirectoryPair Open(string encrDirectoryPath, string decrDirectoryPath, DerivedBytesProvider derivedBytesProvider, bool initialize = false, HelixFileVersion fileVersion = null)
        {
            if (derivedBytesProvider == null)
                throw new ArgumentNullException(nameof(derivedBytesProvider));

            DirectoryPair pair = new DirectoryPair(encrDirectoryPath, decrDirectoryPath, derivedBytesProvider, false);
            if (initialize && pair.InitializeFullNeeded()) pair.InitializeFull(null, fileVersion);
            pair.OpenEncr(null);
            if (initialize && pair.InitializeDecrNeeded()) pair.InitializeDecr(null);
            pair.OpenDecr(null);

            return pair;
        }

        public void Dispose()
        {
            SyncLog?.Dispose();
            SyncLog = null;
        }
    }
}
