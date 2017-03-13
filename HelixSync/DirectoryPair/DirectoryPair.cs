// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
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
        public List<PreSyncDetails> MatchFiles()
        {
            if (!WhatIf && (!EncrDirectory.IsOpen || !DecrDirectory.IsOpen))
                throw new InvalidOperationException("DecrDirectory and EncrDirectory needs to be opened before performing operation");

            List<FileEntry> encrDirectoryFiles = !EncrDirectory.IsOpen ? new List<FileEntry>() : EncrDirectory.GetAllEntries().ToList();
            List<FileEntry> decrDirectoryFiles = DecrDirectory.GetAllEntries().ToList();
            IEnumerable<SyncLogEntry> syncLog = !DecrDirectory.IsOpen ? (IEnumerable<SyncLogEntry>)new List<SyncLogEntry>() : DecrDirectory.SyncLog;
            List<PreSyncDetails> preSyncDetails = new List<PreSyncDetails>();
            
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
                    preSyncDetails.Add(new HelixSync.PreSyncDetails { DecrInfo = entry.Item1 });
                else
                    entry.Item2.DecrInfo = entry.Item1;
            }

            //find encrypted file names
            foreach(var entry in preSyncDetails.Where(d => d.DecrInfo != null))
                entry.ShouldBeEncrName = EncrDirectory.FileNameEncoder.EncodeName(entry.DecrInfo.FileName);

            //Updates/adds encrypted File Information
            var encrJoin = encrDirectoryFiles
                .GroupJoin(preSyncDetails, 
                    o => o.FileName, 
                    i => i?.LogEntry?.EncrFileName ?? i.ShouldBeEncrName, 
                    (o, i) => new Tuple<FileEntry, PreSyncDetails>(o, i.FirstOrDefault()));
            foreach (var entry in encrJoin.ToList())
            {
                if (entry.Item2 == null)
                    preSyncDetails.Add(new HelixSync.PreSyncDetails { EncrInfo = entry.Item1 });
                else
                    entry.Item2.EncrInfo = entry.Item1;
            }

            
            return preSyncDetails;
        }

        public List<PreSyncDetails> FindChanges2()
        {
            var rng = RandomNumberGenerator.Create();

            List<PreSyncDetails> matches = MatchFiles()
                .Where(m => m.DecrChanged || m.EncrChanged)
                .OrderBy(m =>
                {
                    byte[] rno = new byte[5];
                    rng.GetBytes(rno);
                    int randomvalue = BitConverter.ToInt32(rno, 0);
                    return randomvalue;
                })
                .ToList();
            //var str = matches[0].ToString();
            foreach(var match in matches.Where(m=> m.EncrInfo != null))
            {
                string encryFullPath = Path.Combine(EncrDirectoryPath, HelixUtil.PathNative(match.EncrInfo.FileName));
                match.EncrHeader = HelixFile.DecryptHeader(encryFullPath, EncrDirectory.DerivedBytesProvider);
            }

            return matches;
        }

        internal PreSyncMode CalculateMode(PreSyncDetails preSyncDetails)
        {

            bool decrChanged = preSyncDetails.DecrChanged;
            bool encrChanged = preSyncDetails.EncrChanged;

            if (encrChanged == false && decrChanged == false)
                return PreSyncMode.Unchanged;

            //todo: detect orphans
            //todo: detect case-only conflicts

            if (encrChanged == true && decrChanged == true)
            {
                if (preSyncDetails.DecrInfo.EntryType == FileEntryType.Removed && preSyncDetails.EncrHeader == null)
                {
                    return PreSyncMode.Match;
                }
                else if (preSyncDetails.DecrInfo.LastWriteTimeUtc == preSyncDetails.EncrHeader.LastWriteTimeUtc
                    && preSyncDetails.DecrInfo.EntryType == preSyncDetails.EncrHeader.EntryType)
                {
                    return PreSyncMode.Match; //Both changed however still match
                }

                return PreSyncMode.Conflict; //Both changed however one is in conflict
            }
            else if (encrChanged)
            {
                return PreSyncMode.EncryptedSide;
            }
            else if (decrChanged)
            {
                return PreSyncMode.DecryptedSide;
            }
            else
            {
                return PreSyncMode.Unknown;
            }
        }


        [Obsolete]
        public List<DirectoryChange> FindChanges()
        {
            List<DirectoryChange> result = new List<DirectoryChange>();

            //todo: instead of doing this return the PreSyncDetails directly
            //todo: detect conflicts
            foreach (PreSyncDetails preSyncDetail in MatchFiles())
            {
                if ((preSyncDetail?.LogEntry?.DecrModified ?? DateTime.MinValue) != (preSyncDetail?.DecrInfo?.LastWriteTimeUtc ?? DateTime.MinValue) ||
                    (preSyncDetail?.LogEntry?.EntryType ?? FileEntryType.Removed) != (preSyncDetail?.DecrInfo?.EntryType ?? FileEntryType.Removed))
                {
                    result.Add(new DirectoryChange(PairSide.Decrypted, preSyncDetail?.DecrInfo?.FileName ?? preSyncDetail?.LogEntry?.DecrFileName));
                }
                else if (preSyncDetail?.LogEntry?.EncrModified != preSyncDetail?.EncrInfo?.LastWriteTimeUtc)
                    result.Add(new DirectoryChange(PairSide.Encrypted, preSyncDetail?.EncrInfo?.FileName ?? preSyncDetail?.LogEntry?.EncrFileName));
            }

            return result;
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



        public PreSyncDetails GetPreSyncDetails(DirectoryChange entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            PreSyncDetails details = new PreSyncDetails();
            var SyncLog = DecrDirectory.SyncLog;
            if (entry.Side == PairSide.Decrypted)
            {
                details.Side = PairSide.Decrypted;
                details.LogEntry = DecrDirectory.SyncLog.FindByDecrFileName(entry.FileName);
                details.DecrInfo = FileEntry.FromFile(Path.Combine(DecrDirectory.DirectoryPath, entry.FileName), DecrDirectory.DirectoryPath);

                string encrName;
                if (WhatIf && EncrDirectory.FileNameEncoder == null)
                    encrName = new string('#', 32);
                else
                    encrName = EncrDirectory.FileNameEncoder.EncodeName(entry.FileName);
                details.ShouldBeEncrName = encrName;
                details.EncrInfo = FileEntry.FromFile(Path.Combine(EncrDirectory.DirectoryPath, encrName), EncrDirectory.DirectoryPath);
                if (details.EncrInfo.EntryType != FileEntryType.Removed)
                {
                    details.EncrHeader = HelixFile.DecryptHeader(Path.Combine(EncrDirectory.DirectoryPath, encrName),
                                                                 EncrDirectory.DerivedBytesProvider);
                }
                return details;
            }
            else //if (entry.Side == PairSide.Encrypted)
            {
                details.Side = PairSide.Encrypted;
                details.LogEntry = DecrDirectory.SyncLog.FindByEncrFileName(entry.FileName);
                details.EncrHeader = HelixFile.DecryptHeader(Path.Combine(EncrDirectory.DirectoryPath, entry.FileName),
                                                             EncrDirectory.DerivedBytesProvider);
                details.EncrInfo = FileEntry.FromFile(Path.Combine(EncrDirectory.DirectoryPath, entry.FileName), 
                                                         EncrDirectory.DirectoryPath);
                details.DecrInfo = FileEntry.FromFile(Path.Combine(DecrDirectory.DirectoryPath, details.EncrHeader.FileName),
                                                         DecrDirectory.DirectoryPath);
                details.ShouldBeEncrName = EncrDirectory.FileNameEncoder.EncodeName(details.DecrInfo.FileName);

                return details;
            }
        }

        public SyncStatus TrySync(DirectoryChange entry)
        {
            string message;
            bool retry;
            return TrySync(entry, out retry, out message);
        }

        public const int encrTimespanPrecisionMS = 1000;

        public SyncStatus TrySync(DirectoryChange entry, out bool retry, out string message)
        {
            if (WhatIf)
                throw new NotSupportedException("Unable to perform sync when WhatIf mode is set to true");
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            message = null;
            retry = false;

            var SyncLog = DecrDirectory.SyncLog;
            if (entry.Side == PairSide.Decrypted)
            {
                SyncLogEntry logEntry = SyncLog.FindByDecrFileName(entry.FileName);
                SyncLogEntry fileSystemEntry = CreateNewLogEntryFromDecrPath(entry.FileName);
                if (logEntry?.ToString() == fileSystemEntry?.ToString())
                    return SyncStatus.Skipped; //Unchanged

                string encrName = EncrDirectory.FileNameEncoder.EncodeName(entry.FileName);
                string encrPath = Path.Combine(EncrDirectory.DirectoryPath, HelixUtil.PathNative(encrName));
                string decrPath = Path.Combine(DecrDirectory.DirectoryPath, HelixUtil.PathNative(entry.FileName));
                FileEncryptOptions options = new FileEncryptOptions();
                FileEntry header = null;
                options.BeforeWriteHeader = (h) => header = h;
                options.StoredFileName = entry.FileName;
                options.FileVersion = EncrDirectory.Header.FileVersion;
                HelixFile.Encrypt(decrPath, encrPath, EncrDirectory.DerivedBytesProvider, options);
                if (logEntry != null && (File.GetLastWriteTimeUtc(encrPath) - logEntry.EncrModified).TotalMilliseconds < encrTimespanPrecisionMS)
                {
                    File.SetLastWriteTimeUtc(encrPath, logEntry.EncrModified + TimeSpan.FromMilliseconds(encrTimespanPrecisionMS));
                }

                SyncLog.Add(CreateEntryFromHeader(header, FileEntry.FromFile(encrPath, EncrDirectory.DirectoryPath)));
                return SyncStatus.Success;
            }
            else //if (entry.Side == PairSide.Encrypted)
            {
                SyncLogEntry logEntry = SyncLog.FindByEncrFileName(entry.FileName);
                SyncLogEntry fileSystemEntry = CreateNewLogEntryFromEncrPath(entry.FileName);
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
        }

        public void Dispose()
        {
            DecrDirectory.Dispose();
            EncrDirectory.Dispose();
        }
    }
}
