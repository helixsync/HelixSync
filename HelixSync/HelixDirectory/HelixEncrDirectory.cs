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
    public class HelixEncrDirectory : IDisposable
    {
        public HelixEncrDirectory(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentNullException(nameof(directoryPath));

            this.DirectoryPath = HelixUtil.PathNative(directoryPath);
        }

        public FileNameEncoder FileNameEncoder { get; private set; }
        public DerivedBytesProvider DerivedBytesProvider { get; private set; }
        public string DirectoryPath { get; private set; }

        public Action<string> DebugLog { get; set; }

        public DirectoryHeader Header { get; private set; }
        
        public bool IsInitialized()
        {
            if (!File.Exists(Path.Combine(DirectoryPath, HelixConsts.HeaderFileName)))
                return false;

            return true;
        }

        public string[] PreInitializationWarnings(out bool error)
        {
            List<string> warnings = new List<string>();
            error = false;
            if (IsInitialized())
            {
                error = true;
                warnings.Add("ERROR: Encrypted directory already initialized");
            }
            else if (!Directory.Exists(DirectoryPath) 
                && !Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(DirectoryPath))))
            {
                error = true;
                warnings.Add("ERROR: Encrypted directory (and parent) does not exist");
            }

            else if (Directory.Exists(DirectoryPath) 
                && Directory.GetFileSystemEntries(DirectoryPath).Any())
            {
                error = true;
                warnings.Add("ERROR: Encrypted directory is not empty");
            }

            return warnings.ToArray();
        }

        public void GetStatus(out bool isInitialized, out bool canInitialize, out string message)
        {
            if (IsInitialized())
            {
                isInitialized = true;
                canInitialize = false;
                message = "Encr directory Initialized";
            }
            else if (!Directory.Exists(DirectoryPath) && !Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(DirectoryPath))))
            {
                isInitialized = false;
                canInitialize = false;
                message = "Encr parent directory '" + Path.GetDirectoryName(DirectoryPath) + "' does not exist";
            }
            else if (Directory.Exists(DirectoryPath) && Directory.GetFileSystemEntries(DirectoryPath).Any())
            {
                isInitialized = false;
                canInitialize = false;
                message = "Encr directory is not empty";
            }
            else
            {
                isInitialized = false;
                canInitialize = true;
                message = "Encr directory needs initializing";
            }
        }

        /// <summary>
        /// Prepairs the directory and creates a file to later use it. This should only be done once.
        /// </summary>
        public DirectoryHeader Initialize(DerivedBytesProvider derivedBytesProvider, HelixFileVersion fileVersion = null)
        {
            if (derivedBytesProvider == null)
                throw new ArgumentNullException(nameof(derivedBytesProvider));
            if (IsInitialized())
                throw new InvalidOperationException("Directory is already initialized");

            if (System.IO.Directory.Exists(DirectoryPath) && System.IO.Directory.GetFileSystemEntries(DirectoryPath).Any())
                throw new IOException("Unable to initialize new Helix Directory, directory is not empty");

            System.IO.Directory.CreateDirectory(DirectoryPath);
            DirectoryHeader header = DirectoryHeader.New();
            header.Save(Path.Combine(DirectoryPath, HelixConsts.HeaderFileName), derivedBytesProvider, fileVersion);
            return header;
        }

        public bool IsOpen { get; private set; }

        /// <summary>
        /// Open to allow access to files
        /// </summary>
        public void Open(DerivedBytesProvider derivedBytesProvider)
        {
            if (derivedBytesProvider == null)
                throw new ArgumentOutOfRangeException(nameof(derivedBytesProvider));

            if (!IsInitialized())
                throw new InvalidOperationException("Encr directory must be initialized first");
            if (IsOpen)
                throw new InvalidOperationException("Directory is already opened");

            //Existing
            Header = DirectoryHeader.Load(Path.Combine(DirectoryPath, HelixConsts.HeaderFileName), derivedBytesProvider);

            this.DerivedBytesProvider = derivedBytesProvider;
            this.FileNameEncoder = new FileNameEncoder(Header.FileNameKey);
            
            IsOpen = true;
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

        public IEnumerable<FileEntry> GetAllEntries()
        {
            foreach (FileEntry fileEntry in FileEntry.GetChildren(DirectoryPath, DirectoryPath))
            {
                string fileName = fileEntry.FileName;

                if (string.Equals(fileName, HelixConsts.HeaderFileName))
                    continue;
                if (string.Equals(fileName, HelixConsts.SyncLogDirectory))
                    continue;
                if (fileName.StartsWith(HelixConsts.SyncLogDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    continue;
                if (Path.GetExtension(fileName) == HelixConsts.StagedHxExtention)
                    continue;
                if (Path.GetExtension(fileName) == HelixConsts.BackupExtention)
                    continue;
                if (fileEntry.EntryType != FileEntryType.File)
                    continue;

                yield return fileEntry;
            }
        }


        public FileEntry GetFileEntry(string encrFileName)
        {
            return FileEntry.FromFile(Path.Combine(DirectoryPath, encrFileName), DirectoryPath);
        }

        public void Dispose()
        {
            
        }
    }
}
