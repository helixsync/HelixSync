using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HelixSync.FileSystem;
using HelixSync.HelixDirectory;

namespace HelixSync
{
    [DebuggerDisplay("{DecrDirectory.Name} <=> {EncrDirectory.Name}")]
    public partial class DirectoryPair : IDisposable
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
            
            if (!EncrDirectory.Exists
                && !Directory.Exists(Path.GetDirectoryName(EncrDirectory.FullName)))
            {
                throw new Exception("Encrypted directory (and parent) does not exist");
            }

            if (Directory.Exists(EncrDirectory.FullName)
                && !EncrDirectory.ChildExists(HelixConsts.HeaderFileName)
                && EncrDirectory.GetEntries().Any())
            {
                throw new Exception($"Unable to initialize, encrypted directory ({EncrDirectory.Name}) is not empty");
            }


            if (!DecrDirectory.Exists
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
            consoleEx?.WriteLine(VerbosityLevel.Detailed, 0, "Initializing Encrypted Directory...");
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
            consoleEx?.WriteLine(VerbosityLevel.Detailed, 1, "Encrypted Directory Initialized (" + Header.DirectoryId.Substring(0, 6) + "...)");


            InitializeDecr(consoleEx);
        }

        public void OpenEncr(ConsoleEx consoleEx)
        {
            if (Header == null)
            {
                consoleEx?.WriteLine(VerbosityLevel.Detailed, 0, "Opening Encrypted Directory...");
                Header = DirectoryHeader.Load(EncrDirectory.PathFull(HelixConsts.HeaderFileName), DerivedBytesProvider);
                consoleEx?.WriteLine(VerbosityLevel.Detailed, 0, "Opened Encrypted Directory (" + Header.DirectoryId.Substring(0, 6) + "...)");
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
            consoleEx?.WriteLine(VerbosityLevel.Detailed, 0, "Initializing Decrypted Directory...");

            //Initialize Decr Directory
            DecrDirectory.Create();
            DecrDirectory.CreateDirectory(HelixConsts.SyncLogDirectory);
            if (WhatIf)
            {
                DecrDirectory.WhatIfAddFile(SyncLogPath, 10);
            }
            else
            {
                using var stream = File.CreateText(DecrDirectory.PathFull(SyncLogPath));
                stream.WriteLine(HelixConsts.SyncLogHeader);
            }

            consoleEx?.WriteLine(VerbosityLevel.Detailed, 1, "Decrypted Directory Initialized");
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

        public void ClearCache()
        {
            EncrDirectory.Reset();
            SyncLog.Reload();
            DecrDirectory.Reset();
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
                    EncrDirectory.WhatIfReplaceFile(encrPath, entry.DisplayFileLength);
                    if (entry.DecrInfo == null)
                    {
                        header = new FileEntry()
                        {
                            EntryType = FileEntryType.Removed,
                            FileName = entry.DecrFileName,
                        };
                    }
                    else
                    {
                        header = entry.DecrInfo.ToFileEntry();
                    }
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
                        if (entry.EncrHeader.EntryType == FileEntryType.File)
                            DecrDirectory.WhatIfReplaceFile(decrPath, entry.EncrHeader.Length, entry.EncrHeader.LastWriteTimeUtc);
                        else if (entry.EncrHeader.EntryType == FileEntryType.Removed)
                            DecrDirectory.WhatIfDeleteFile(decrPath);
                        else if (entry.EncrHeader.EntryType == FileEntryType.Directory)
                            DecrDirectory.WhatIfAddDirectory(decrPath);
                        else
                            throw new NotSupportedException();
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
            else if (entry.SyncMode == PreSyncMode.Match)
            {
                //Add to Log file (changed to be equal on both sides)
                SyncLogEntry fileSystemEntry = CreateNewLogEntryFromDecrPath(entry.DecrFileName);
                SyncLog.Add(fileSystemEntry);
                return SyncResults.Success();
            }
            else if (entry.SyncMode == PreSyncMode.Unchanged)
            {
                //do nothing
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


        public static DirectoryPair Open(string decrDirectoryPath, string encrDirectoryPath, DerivedBytesProvider derivedBytesProvider, bool initialize = false, HelixFileVersion fileVersion = null)
        {
            if (derivedBytesProvider == null)
                throw new ArgumentNullException(nameof(derivedBytesProvider));

            DirectoryPair pair = new DirectoryPair(decrDirectoryPath, encrDirectoryPath, derivedBytesProvider, false);
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
