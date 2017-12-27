// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    public class HelixDecrDirectory : IDisposable
    {
        public HelixDecrDirectory(string directoryPath, string encrDirectoryId = null)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentNullException(nameof(directoryPath));

            this.DirectoryPath = HelixUtil.PathNative(directoryPath);
            this.EncrDirectoryId = encrDirectoryId;
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

        public string DirectoryPath { get; private set; }
        public SyncLog SyncLog { get; private set; }

        private string GetSyncLogPath()
        {
            return Path.Combine(DirectoryPath, HelixConsts.SyncLogDirectory, EncrDirectoryId + HelixConsts.SyncLogExtention);
        }

        public bool IsInitialized()
        {
            return File.Exists(GetSyncLogPath());
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

            Directory.CreateDirectory(DirectoryPath);
            Directory.CreateDirectory(Path.GetDirectoryName(GetSyncLogPath()));
            using (var stream = File.CreateText(GetSyncLogPath()))
            {
                stream.WriteLine(HelixConsts.SyncLogHeader);
            }
        }

        public bool IsOpen { get; private set; }
        public void Open(bool whatIf = false)
        {
            if (string.IsNullOrEmpty(EncrDirectoryId))
                throw new InvalidOperationException("EncrDirectoryID must be set prior to initialization");
            if (!IsInitialized())
                throw new InvalidOperationException("Directory has not been initialized");
            if (IsOpen)
                throw new InvalidOperationException("Directory is already opened");
                
            SyncLog = SyncLog.GetLog(GetSyncLogPath(), whatIf);
            IsOpen = true;
        }

        public IEnumerable<FileEntry> GetAllEntries()
        {
            foreach (FileEntry fileEntry in FileEntry.GetChildren(DirectoryPath, DirectoryPath))
            {
                string fileName = fileEntry.FileName;

                if (string.Equals(fileName, HelixConsts.SyncLogDirectory))
                    continue;
                if (fileName.StartsWith(HelixConsts.SyncLogDirectory + HelixUtil.UniversalDirectorySeparatorChar, StringComparison.Ordinal))
                    continue;

                yield return fileEntry;
            }
        }

        public FileEntry GetFileEntry(string fileName)
        {
            return FileEntry.FromFile(Path.Combine(DirectoryPath, fileName), DirectoryPath);
        }

        /// <summary>
        /// Removes extra and temporary files
        /// </summary>
        public void Clean()
        {
            foreach (string file in Directory.GetFiles(DirectoryPath, Path.ChangeExtension("*", HelixConsts.StagedHxExtention)))
                HelixFile.CleanupFile(file);
            foreach (string file in Directory.GetFiles(DirectoryPath, Path.ChangeExtension("*", HelixConsts.BackupExtention)))
                HelixFile.CleanupFile(file);
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
