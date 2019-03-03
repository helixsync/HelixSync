// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HelixSync.FileSystem;

namespace HelixSync.HelixDirectory
{
    public class HelixDecrDirectory : IDisposable
    {
        public HelixDecrDirectory(string directoryPath, string encrDirectoryId = null, bool whatIf = false)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentNullException(nameof(directoryPath));

            this.DirectoryPath = HelixUtil.PathNative(directoryPath);
            this.EncrDirectoryId = encrDirectoryId;
            this.FSDirectory = new FSDirectory(directoryPath, whatIf, isRoot: true);
        }

        string m_EncrDirectoryId = null;
        public string EncrDirectoryId
        {
            get { return m_EncrDirectoryId; }
            set
            {
                if (IsOpen)
                    throw new InvalidOperationException("EncrDirectoryID cannot be changed after Opened");
                m_EncrDirectoryId = value;
            }
        }

        public FSDirectory FSDirectory { get; }
        public string DirectoryPath { get; }
        public SyncLog SyncLog { get; private set; }

        private string GetSyncLogPath()
        {
            return Path.Combine(HelixConsts.SyncLogDirectory, EncrDirectoryId + HelixConsts.SyncLogExtention);
        }

        public bool IsInitialized()
        {
            return FSDirectory.ChildExists(GetSyncLogPath());
        }

        public string[] PreInitializationWarnings(out bool error)
        {
            List<string> warnings = new List<string>();
            error = false;
            if (IsInitialized())
            {
                error = true;
                warnings.Add("ERROR: Decrypted directory already initialized");
            }
            else if (!Directory.Exists(DirectoryPath)
                && !Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(DirectoryPath))))
            {
                error = true;
                warnings.Add("ERROR: Decrypted directory (and parent) does not exist");
            }

            else if (Directory.Exists(DirectoryPath)
                && Directory.GetFileSystemEntries(DirectoryPath).Any())
            {
                warnings.Add("WARNING: Decrypted directory is not empty will be merged on initialized");
            }

            return warnings.ToArray();
        }

        public void GetStatus(out bool isInitialized, out bool canInitialize, out bool warning, out string message)
        {
            if (IsInitialized())
            {
                isInitialized = true;
                canInitialize = false;
                warning = false;
                message = "Initialized";
            }
            else if (!Directory.Exists(DirectoryPath) && !Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(DirectoryPath))))
            {
                isInitialized = false;
                canInitialize = false;
                warning = false;
                message = "Parent directory '" + Path.GetDirectoryName(DirectoryPath) + "' does not exist";
            }
            else if (Directory.Exists(DirectoryPath) && Directory.GetFileSystemEntries(DirectoryPath).Any())
            {
                isInitialized = false;
                canInitialize = true;
                warning = true;
                message = "Directory is not empty may loose files if conflicts exist";
            }
            else
            {
                isInitialized = false;
                canInitialize = true;
                warning = false;
                message = "Directory needs initializing";
            }
        }


        /// <summary>
        /// One time task to initialize the directory for syncing
        /// </summary>
        public void Initialize()
        {
            if (string.IsNullOrEmpty(EncrDirectoryId))
                throw new InvalidOperationException("EncrDirectoryID must be set prior to initialization");
            if (IsInitialized())
                throw new InvalidOperationException("Directory has already been initialized");


            FSDirectory.Create();
            FSDirectory.CreateDirectory(HelixConsts.SyncLogDirectory);

            if (FSDirectory.WhatIf)
            {
                FSDirectory.WhatIfAddFile(GetSyncLogPath(), 10);
            }
            else
            {
                using (var stream = File.CreateText(FSDirectory.PathFull(GetSyncLogPath())))
                {
                    stream.WriteLine(HelixConsts.SyncLogHeader);
                }
            }
        }

        public bool IsOpen { get; private set; }
        public void Open()
        {
            if (string.IsNullOrEmpty(EncrDirectoryId))
                throw new InvalidOperationException("EncrDirectoryID must be set prior to initialization");
            if (!IsInitialized())
                throw new InvalidOperationException("Directory has not been initialized");
            if (IsOpen)
                throw new InvalidOperationException("Directory is already opened");
                
            SyncLog = SyncLog.GetLog(FSDirectory.PathFull(GetSyncLogPath()), FSDirectory.WhatIf);
            IsOpen = true;
        }

        public IEnumerable<FSEntry> GetAllEntries()
        {
            foreach (FSEntry fileEntry in FSDirectory.GetEntries(SearchOption.AllDirectories))
            {
                string fileName = fileEntry.RelativePath;

                if (string.Equals(fileName, HelixConsts.SyncLogDirectory))
                    continue;
                if (fileName.StartsWith(HelixConsts.SyncLogDirectory + HelixUtil.UniversalDirectorySeparatorChar, StringComparison.Ordinal))
                    continue;

                yield return fileEntry;
            }
        }

        public FSEntry GetFileEntry(string fileName)
        {
            return FSDirectory.TryGetEntry(fileName);
        }

        /// <summary>
        /// Removes extra and temporary files, restores backups for partial files
        /// </summary>
        public void Cleanup(ConsoleEx console)
        {
            FSDirectory.Cleanup(console);
        }

        public void Dispose()
        {
            if (SyncLog != null)
            {
                SyncLog.Dispose();
                SyncLog = null;
                IsOpen = false;
            }
        }
    }
    
}
