// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace HelixSync
{
    public class DirectoryPair : IDisposable
    {
        bool WhatIf;
        private string EncrDirectoryPath;

        public DirectoryPair(HelixEncrDirectory encrDirectory, HelixDecrDirectory decrDirectory, bool whatIf = false)
        {
            if (encrDirectory == null)
                throw new ArgumentNullException(nameof(encrDirectory));
            if (!whatIf && !encrDirectory.IsOpen)
                throw new ArgumentException("EncrDirectory must be open first", nameof(encrDirectory));
            if (decrDirectory == null)
                throw new ArgumentNullException(nameof(decrDirectory));
            if (!whatIf && !decrDirectory.IsOpen)
                throw new ArgumentException("DecrDirectory must be open first", nameof(decrDirectory));
            if (encrDirectory.IsOpen && encrDirectory.Header.DirectoryId != decrDirectory.EncrDirectoryId)
                throw new InvalidOperationException("DecrDirectory and EncrDirectory's Directory Id do not match");
            
            WhatIf = whatIf;
            EncrDirectory = encrDirectory;
            EncrDirectoryPath = encrDirectory.DirectoryPath;

            DecrDirectory = decrDirectory;
        }

        public static DirectoryPair Open(string encrDirectoryPath, string decrDirectoryPath, DerivedBytesProvider derivedBytesProvider, bool initialize=false, HelixFileVersion fileVersion = null)
        {
            if (derivedBytesProvider == null)
                throw new ArgumentNullException(nameof(derivedBytesProvider));


            HelixEncrDirectory encrDirectory = new HelixEncrDirectory(encrDirectoryPath);
            if (!encrDirectory.IsInitialized())
            {
                if (initialize)
                    encrDirectory.Initialize(derivedBytesProvider, fileVersion);
                else
                    throw new HelixException("Encrypted Directory Not Initialized");
            }

            encrDirectory.Open(derivedBytesProvider);

            HelixDecrDirectory decrDirectory = new HelixDecrDirectory(decrDirectoryPath, encrDirectory.Header.DirectoryId);
            if (!decrDirectory.IsInitialized())
            {
                if (initialize)
                    decrDirectory.Initialize();
                else
                    throw new HelixException("Decrypted Directory Not Initialized");
            }

            decrDirectory.Open();

            return new DirectoryPair(encrDirectory, decrDirectory);
        }

        public HelixEncrDirectory EncrDirectory { get; private set; }
        public HelixDecrDirectory DecrDirectory { get; private set; }
        
        /// <summary>
        /// Matches files from Encr, Decr and Log Entry
        /// </summary>
        private List<PreSyncDetails> MatchFiles()
        {
            if (!WhatIf && (!EncrDirectory.IsOpen || !DecrDirectory.IsOpen))
                throw new InvalidOperationException("DecrDirectory and EncrDirectory needs to be opened before performing operation");

            List<FileEntry> encrDirectoryFiles = !EncrDirectory.IsOpen ? new List<FileEntry>() : EncrDirectory.GetAllEntries().ToList();
            List<FileEntry> decrDirectoryFiles = DecrDirectory.GetAllEntries().ToList();

            //todo: filter out log entries where the decr file name and the encr file name does not match
            IEnumerable<SyncLogEntry> syncLog = !DecrDirectory.IsOpen ? (IEnumerable<SyncLogEntry>)new List<SyncLogEntry>() : DecrDirectory.SyncLog;
            List<PreSyncDetails> preSyncDetails = new List<PreSyncDetails>();
            
            //Adds Logs
            preSyncDetails.AddRange(syncLog.Select(entry => new PreSyncDetails { LogEntry = entry }));
            
            //Updates/Adds Decrypted File Information
            var decrJoin = decrDirectoryFiles
                .GroupJoin(preSyncDetails,
                    o => o.FileName,
                    i => i?.LogEntry?.DecrFileName,
                    (o, i) => new Tuple<FileEntry, PreSyncDetails>(o, i.FirstOrDefault()));
            foreach (var entry in decrJoin.ToList())
            {
                if (entry.Item2 == null)
                {
                    //New Entry (not in log)
                    preSyncDetails.Add(new HelixSync.PreSyncDetails { DecrInfo = entry.Item1 });
                }
                else
                {
                    //Existing Entry (update only)
                    entry.Item2.DecrInfo = entry.Item1;
                }
            }

            //find encrypted file names
            foreach (var entry in preSyncDetails)
            {
                entry.DecrFileName = entry.LogEntry?.DecrFileName ?? entry.DecrInfo.FileName;
                entry.EncrFileName = EncrDirectory.FileNameEncoder.EncodeName(entry.DecrFileName);
            }


            //Updates/adds encrypted File Information
            var encrJoin = encrDirectoryFiles
                .GroupJoin(preSyncDetails, 
                    o => o.FileName, 
                    i => i.EncrFileName, 
                    (o, i) => new Tuple<FileEntry, PreSyncDetails>(o, i.FirstOrDefault()));
            foreach (var entry in encrJoin.ToList())
            {
                if (entry.Item2 == null)
                {
                    //New Entry (not in log or decrypted file)
                    preSyncDetails.Add(new HelixSync.PreSyncDetails { EncrInfo = entry.Item1, EncrFileName = entry.Item1.FileName });
                }
                else
                {
                    //Existing Entry
                    entry.Item2.EncrInfo = entry.Item1;
                }
            }

            foreach (PreSyncDetails entry in preSyncDetails)
                RefreshPreSyncMode(entry);

            return preSyncDetails;
        }

        public List<PreSyncDetails> FindChanges()
        {
            var rng = RandomNumberGenerator.Create();

            List<PreSyncDetails> matches = MatchFiles()
                .Where(m => m.SyncMode != PreSyncMode.Unchanged)
                .ToList();

            //Fills in the EncrHeader
            foreach (PreSyncDetails match in matches.Where(m => m.EncrInfo != null))
            {
                RefreshPreSyncEncrHeader(match);
                RefreshPreSyncMode(match);
            }

            //todo: reorder based on dependencies
            //todo: detect conflicts
            return matches.OrderBy(m =>
                {
                    byte[] rno = new byte[5];
                    rng.GetBytes(rno);
                    int randomvalue = BitConverter.ToInt32(rno, 0);
                    return randomvalue;
                })
                .ToList();
        }

        private void RefreshPreSyncEncrHeader(PreSyncDetails match)
        {
            string encryFullPath = Path.Combine(EncrDirectoryPath, HelixUtil.PathNative(match.EncrInfo.FileName));
            if (File.Exists(encryFullPath))
            {
                match.EncrHeader = HelixFile.DecryptHeader(encryFullPath, EncrDirectory.DerivedBytesProvider);

                //Updates the DecrFileName (if neccessary)
                if (string.IsNullOrEmpty(match.DecrFileName)
                    && EncrDirectory.FileNameEncoder.EncodeName(match.EncrHeader.FileName) == match.EncrInfo.FileName)
                {
                    match.DecrFileName = match.EncrHeader.FileName;
                }
            }
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

            bool decrChanged = true;
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
                && LogEntry.DecrFileName == DecrInfo.FileName
                && LogEntry.DecrModified == DecrInfo.LastWriteTimeUtc)
            {
                decrChanged = false;
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
            else if (LogEntry.EncrFileName == EncrInfo.FileName
                && LogEntry.EncrModified == EncrInfo.LastWriteTimeUtc)
            {
                encrChanged = false;
            }

            //todo: detect orphans
            //todo: detect case-only conflicts

            if (encrChanged == false && decrChanged == false)
            {
                
                preSyncDetails.SyncMode = PreSyncMode.Unchanged;
            }
            else if (encrChanged == true && decrChanged == true)
            {
                if (preSyncDetails.DecrInfo.EntryType == FileEntryType.Removed && preSyncDetails.EncrHeader == null)
                {
                    preSyncDetails.SyncMode = PreSyncMode.Match;
                }
                else if (preSyncDetails.DecrInfo.LastWriteTimeUtc == preSyncDetails.EncrHeader.LastWriteTimeUtc
                    && preSyncDetails.DecrInfo.EntryType == preSyncDetails.EncrHeader.EntryType)
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
                else if(preSyncDetails.EncrHeader?.EntryType == FileEntryType.Removed)
                    preSyncDetails.DisplayOperation = PreSyncOperation.Add;
                else
                    preSyncDetails.DisplayOperation = PreSyncOperation.Change;
            }
            else if (preSyncDetails.SyncMode == PreSyncMode.EncryptedSide)
            {
                preSyncDetails.DisplayEntryType = preSyncDetails.EncrHeader?.EntryType ?? FileEntryType.Removed;
                preSyncDetails.DisplayFileLength = preSyncDetails.EncrHeader?.Length ?? preSyncDetails.DecrInfo?.Length ?? 0;
                
                if (preSyncDetails.EncrInfo == null || preSyncDetails.EncrInfo.EntryType == FileEntryType.Removed) 
                    preSyncDetails.DisplayOperation = PreSyncOperation.Remove;
                else if(preSyncDetails.EncrHeader?.EntryType == FileEntryType.Removed)
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
                preSyncDetails.DisplayEntryType = preSyncDetails.DecrInfo.EntryType;
                preSyncDetails.DisplayFileLength = preSyncDetails.DecrInfo?.Length ?? preSyncDetails.EncrHeader?.Length ?? 0;

                preSyncDetails.DisplayOperation = PreSyncOperation.Error;
            }

        }
        
        
        /// <summary>
        /// Refreshes the contents of the preSyncDetails using the file system (in case the file system has changed since last retrieved)
        /// </summary>
        internal void RefreshPreSyncDetails(PreSyncDetails preSyncDetails)
        {
            if (preSyncDetails == null)
                throw new ArgumentNullException(nameof(preSyncDetails));
            if (string.IsNullOrEmpty(preSyncDetails.EncrFileName) && string.IsNullOrEmpty(preSyncDetails.DecrFileName))
                throw new ArgumentException("EncrFileName or DecrFileName must be included with preSyncDetails", nameof(preSyncDetails));

            if (string.IsNullOrEmpty(preSyncDetails.EncrFileName))
                preSyncDetails.EncrFileName = EncrDirectory.FileNameEncoder.EncodeName(preSyncDetails.DecrFileName);
            
            preSyncDetails.EncrInfo = FileEntry.FromFile(Path.Combine(EncrDirectory.DirectoryPath, preSyncDetails.EncrFileName),
                                                        EncrDirectory.DirectoryPath);
            RefreshPreSyncEncrHeader(preSyncDetails);

            if (!string.IsNullOrEmpty(preSyncDetails.DecrFileName))
            {
                preSyncDetails.DecrInfo = FileEntry.FromFile(Path.Combine(DecrDirectory.DirectoryPath, preSyncDetails.DecrFileName),
                                                     DecrDirectory.DirectoryPath);
            }

            preSyncDetails.LogEntry = DecrDirectory.SyncLog.FindByDecrFileName(preSyncDetails.DecrFileName);

            RefreshPreSyncMode(preSyncDetails);
        }

        public SyncLogEntry CreateNewLogEntryFromDecrPath(string decrFileName)
        {
            FileEntry decrEntry = DecrDirectory.GetFileEntry(decrFileName);

            string encrFileName = EncrDirectory.FileNameEncoder.EncodeName(decrFileName);
            FileEntry encrEntry = EncrDirectory.GetFileEntry(encrFileName);

            return new SyncLogEntry(decrEntry.EntryType, decrEntry.FileName, decrEntry.LastWriteTimeUtc, encrEntry.FileName, encrEntry.LastWriteTimeUtc);
        }


        public SyncLogEntry CreateNewLogEntryFromEncrPath(string encrFileName)
        {
            string encrFilePath = Path.Combine(EncrDirectoryPath, encrFileName);

            FileEntry encrEntry = EncrDirectory.GetFileEntry(encrFileName);

            FileEntry header = HelixFile.DecryptHeader(encrFilePath, EncrDirectory.DerivedBytesProvider);
            var decrFileName = header.FileName;
            if (encrFileName != EncrDirectory.FileNameEncoder.EncodeName(decrFileName))
                throw new HelixException("Encrypted file name does not match"); //todo: prompt for action

            return new SyncLogEntry(header.EntryType, header.FileName, header.LastWriteTimeUtc, encrEntry.FileName, encrEntry.LastWriteTimeUtc);
        }

        public SyncLogEntry CreateEntryFromHeader(FileEntry decrFileInfo, FileEntry encrFileInfo)
        {
            if (decrFileInfo == null)
                throw new ArgumentNullException(nameof(decrFileInfo));
            if (encrFileInfo == null)
                throw new ArgumentNullException(nameof(encrFileInfo));

            SyncLogEntry entry = new SyncLogEntry(decrFileInfo.EntryType, decrFileInfo.FileName, decrFileInfo.LastWriteTimeUtc, encrFileInfo.FileName, encrFileInfo.LastWriteTimeUtc);
            return entry;
        }


        public const int encrTimespanPrecisionMS = 1000;

        public SyncStatus TrySync(PreSyncDetails entry)
        {
            string message;
            bool retry;
            return TrySync(entry, out retry, out message);
        }
        public SyncStatus TrySync(PreSyncDetails entry, out bool retry, out string message)
        {
            if (WhatIf)
                throw new NotSupportedException("Unable to perform sync when WhatIf mode is set to true");
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            message = null;
            retry = false;

            var SyncLog = DecrDirectory.SyncLog;
            if (entry.SyncMode == PreSyncMode.DecryptedSide)
            {
                SyncLogEntry logEntry = entry.LogEntry;
                SyncLogEntry fileSystemEntry = CreateNewLogEntryFromDecrPath(entry.DecrFileName);
                if (logEntry?.ToString() == fileSystemEntry?.ToString())
                    return SyncStatus.Skipped; //Unchanged

                
                string encrPath = Path.Combine(EncrDirectory.DirectoryPath, HelixUtil.PathNative(entry.EncrFileName));
                string decrPath = Path.Combine(DecrDirectory.DirectoryPath, HelixUtil.PathNative(entry.DecrFileName));
                FileEncryptOptions options = new FileEncryptOptions();
                FileEntry header = null;
                options.BeforeWriteHeader = (h) => header = h;
                options.StoredFileName = entry.DecrFileName;
                options.FileVersion = EncrDirectory.Header.FileVersion;
                HelixFile.Encrypt(decrPath, encrPath, EncrDirectory.DerivedBytesProvider, options);
                if (logEntry != null && (File.GetLastWriteTimeUtc(encrPath) - logEntry.EncrModified).TotalMilliseconds < encrTimespanPrecisionMS)
                {
                    File.SetLastWriteTimeUtc(encrPath, logEntry.EncrModified + TimeSpan.FromMilliseconds(encrTimespanPrecisionMS));
                }

                SyncLog.Add(CreateEntryFromHeader(header, FileEntry.FromFile(encrPath, EncrDirectory.DirectoryPath)));
                return SyncStatus.Success;
            }
            else if (entry.SyncMode == PreSyncMode.EncryptedSide)
            {
                SyncLogEntry logEntry = entry.LogEntry;
                SyncLogEntry fileSystemEntry = CreateNewLogEntryFromEncrPath(entry.EncrFileName);
                if (logEntry?.ToString() == fileSystemEntry?.ToString())
                    return SyncStatus.Skipped; //Unchanged

                //todo: test to see if there are illegal characters
                //todo: check if the name matches

                string encrPath = Path.Combine(EncrDirectory.DirectoryPath, HelixUtil.PathNative(fileSystemEntry.EncrFileName));
                string decrPath = Path.Combine(DecrDirectory.DirectoryPath, HelixUtil.PathNative(fileSystemEntry.DecrFileName));

                //todo: if file exists with different case - skip file
                HelixFile.Decrypt(encrPath, decrPath, EncrDirectory.DerivedBytesProvider);
                //todo: get the date on the file system (needed if the filesystem has less percission

                SyncLog.Add(fileSystemEntry);
                return SyncStatus.Success;
            }

            return SyncStatus.Failure;
        }


        public void Dispose()
        {
            DecrDirectory.Dispose();
            EncrDirectory.Dispose();
        }
    }
}
