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
            FullName = fullPath;
            Parent = parent;
            Root = parent?.Root ?? (this as FSDirectory);
            WhatIf = whatIf;
        }

        public string FullName { get; }
        public FSDirectory Parent { get; }
        public FSDirectory Root { get; }
        public bool WhatIf { get; }

        internal DateTime m_LastWriteTimeUtc;
        public DateTime LastWriteTimeUtc
        {
            get => m_LastWriteTimeUtc;
            set
            {
                if (!WhatIf)
                {
                    if (this is FSDirectory)
                        Directory.SetLastWriteTimeUtc(FullName, value);
                    else
                        File.SetLastWriteTimeUtc(FullName, value);
                }
                m_LastWriteTimeUtc = value;
            }
        }

        protected virtual void PopulateFromInfo(FileSystemInfo info)
        {
            this.m_LastWriteTimeUtc = info.LastWriteTimeUtc;
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

            if (path == root)
                return "";

            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                root = root + Path.DirectorySeparatorChar;

            if (!path.StartsWith(root, StringComparison.Ordinal))
                throw new ArgumentOutOfRangeException(nameof(path), "path must start with the root directory (path:" + path + ", root: " + root + ")");

            return path.Substring(root.Length);
        }

        public override string ToString()
        {
            return RelativePath;
        }
    }
}
