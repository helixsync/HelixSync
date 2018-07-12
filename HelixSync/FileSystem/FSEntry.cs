using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HelixSync.FileSystem
{
    public abstract class FSEntry
    {
        protected FSEntry(string fullPath, FSDirectory parent, bool whatIf)
        {
            FullName = HelixUtil.PathUniversal(fullPath);
            Parent = parent;
            Root = parent?.Root ?? (this as FSDirectory);
            WhatIf = whatIf;
        }

        public string FullName { get; }
        public FSDirectory Parent { get; }
        public FSDirectory Root { get; }
        public bool WhatIf { get; }

        private DateTime m_LastWriteTimeUtc;
        public DateTime LastWriteTimeUtc
        {
            get => m_LastWriteTimeUtc;
            set
            {
                if (this is FSDirectory)
                    return;

                if (!WhatIf)
                {
                    if (this is FSFile)
                        File.SetLastWriteTimeUtc(HelixUtil.PathNative(FullName), value);
                }
                
                m_LastWriteTimeUtc = HelixUtil.TruncateTicks(value); 
            }
        }

        private long m_Length;
        public long Length
        {
            get { return m_Length; }
        }

        protected virtual void PopulateFromInfo(FileSystemInfo info)
        {
            if (this is FSDirectory)
            {
                this.m_LastWriteTimeUtc = DateTime.MinValue;
                this.m_Length = 0;
            }
            else 
            {
                this.m_LastWriteTimeUtc = HelixUtil.TruncateTicks(info.LastWriteTimeUtc);
                this.m_Length = (info as FileInfo)?.Length ?? ((long)0);
            }
        }

        internal virtual void PopulateFromInfo(DateTime lastWriteTimeUtc, long length)
        {
            if (this is FSDirectory)
            {
                this.m_LastWriteTimeUtc = DateTime.MinValue;
                this.m_Length = 0;
            }
            else 
            {
                this.m_LastWriteTimeUtc = HelixUtil.TruncateTicks(lastWriteTimeUtc);
                this.m_Length = length;
            }
        }

        /// <summary>
        /// The path relative to the root
        /// </summary>
        public string RelativePath => RemoveRootFromPath(FullName, Root.FullName);

        /// <summary>
        /// The name of the file or directory excludes any path
        /// </summary>
        public string Name => Path.GetFileName(FullName);

        /// <summary>
        /// Removes the root from the begining of the string (returns a relative path)
        /// For cross platform compatibility is case sensitive
        /// </summary>
        public static string RemoveRootFromPath(string path, string root)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            if (string.IsNullOrEmpty(root))
                return path;

            path = HelixUtil.PathUniversal(path);
            root = HelixUtil.PathUniversal(root);

            if (path == root)
                return "";

            if (!root.EndsWith(HelixUtil.UniversalDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                root = root + HelixUtil.UniversalDirectorySeparatorChar;

            if (!path.StartsWith(root, StringComparison.Ordinal))
                throw new ArgumentOutOfRangeException(nameof(path), "path must start with the root directory (path:" + path + ", root: " + root + ")");

            return path.Substring(root.Length);
        }

        public FileEntryType EntryType
        {
            get
            {
                if (this is FSFile)
                    return FileEntryType.File;
                else if (this is FSDirectory)
                    return FileEntryType.Directory;
                else
                    throw new InvalidDataException("FSEntry is not a known type");
            }
        }

        public override string ToString()
        {
            return RelativePath;
        }
    }
}
