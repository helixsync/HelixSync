using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Collections.ObjectModel;

namespace HelixSync.FileSystem
{
    ///<summary>
    ///Provides a wrapper for a system directory. Supports -WhatIf (changes are not saved to disk)
    ///<summary>
    public class FSDirectory : FSEntry, IFSDirectoryCore
    {
        public FSDirectory(string path, bool whatIf)
            : base(path, null, whatIf)
        {
            this.IsRoot = true;
            this.PopulateFromInfo(new DirectoryInfo(path));
        }

        public FSDirectory(string path, bool whatIf, bool isRoot)
            : this(new DirectoryInfo(path), null, whatIf, isRoot)
        {

        }

        public FSDirectory(string path, FSDirectory parent)
            : base(path, parent, parent.WhatIf)
        {

        }

        internal FSDirectory(DirectoryInfo directoryInfo, FSDirectory parent, bool whatIf, bool isRoot)
            : base(directoryInfo.FullName, parent, whatIf)
        {
            this.PopulateFromInfo(directoryInfo);
            this.IsRoot = isRoot;
        }



        private FSEntryCollection children = new FSEntryCollection(HelixUtil.FileSystemCaseSensitive);
        public bool IsLoaded { get; private set; }
        bool IsLoadedDeep { get; set; }
        public bool IsRoot { get; }

        public void Load(bool deep = false)
        {
            if (IsLoadedDeep)
                return;
            if (IsLoaded && !deep)
                return;

            Load(new DirectoryInfo(this.FullName), deep);
        }
        protected void Load(DirectoryInfo directoryInfo, bool deep)
        {
            if (IsLoadedDeep)
                return;
            if (IsLoaded)
            {
                if (deep)
                {
                    foreach (var child in children.OfType<FSDirectory>())
                        child.Load(true);
                }
                IsLoadedDeep = true;
                return;
            }

            if (!Exists)
            {
                IsLoadedDeep = true;
                return;
            }

            PopulateFromInfo(directoryInfo);
            var IOChildren = (directoryInfo).EnumerateFileSystemInfos().ToArray();
            foreach (var entry in IOChildren)
            {
                if (entry is FileInfo childFileInfo)
                    children.Add(new FSFile(childFileInfo, this, WhatIf));
                else if (entry is DirectoryInfo childDirectoryInfo)
                {
                    var newChild = new FSDirectory(childDirectoryInfo, this, WhatIf, isRoot: false);
                    children.Add(newChild);
                    if (deep)
                        newChild.Load(childDirectoryInfo, true);
                }
            }
            IsLoaded = true;
            if (deep)
                IsLoadedDeep = true;
        }

        public IEnumerable<FSEntry> GetEntries(SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (searchOption == SearchOption.AllDirectories)
                Load(true);
            else
                Load(false);

            foreach (var entry in children)
            {
                yield return entry;
                if (searchOption == SearchOption.AllDirectories && entry is FSDirectory entryDirectory)
                {
                    foreach (var grandchildEntry in entryDirectory.GetEntries(SearchOption.AllDirectories))
                        yield return grandchildEntry;
                }
            }
        }

        /// <summary>
        /// Returns the FSEntry for the path. Returns null if not found.
        /// </summary>
        /// <param name="path">Path can be relative to the directory or absolute</param>
        public FSEntry TryGetEntry(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            path = PathRelative(path);

            if (path == "")
                return this;

            Load();

            var split = path.Split(HelixUtil.UniversalDirectorySeparatorChar);
            if (!children.Contains(split[0]))
                return null;
            else if (split.Length == 1)
                return children[split[0]];
            else
                return (children[split[0]] as FSDirectory)?
                    .TryGetEntry(string.Join(HelixUtil.UniversalDirectorySeparatorChar.ToString(), split.Skip(1).ToArray()));
        }

        internal void WhatIfAddFile(string relativeName, long fileSize)
        {
            if (!WhatIf)
                throw new InvalidOperationException("FSDirectory not in WhatIf mode");

            if (!Exists)
                throw new DirectoryNotFoundException();

            FSFile entry = new FSFile(PathFull(relativeName), GetDirectory(PathDirectory(relativeName)), this.WhatIf);
            //entry.Length = fileSize;
            entry.Parent.children.Add(entry);
        }

        public FSDirectory GetDirectory(string path)
        {
            var entry = this.TryGetEntry(path);
            if (entry is FSDirectory entryDirectory)
                return entryDirectory;
            else
                throw new DirectoryNotFoundException();
        }

        internal void WhatIfAddDirectory(string relativeName)
        {
            if (!WhatIf)
                throw new InvalidOperationException("FSDirectory not in WhatIf mode");

            var entry = new FSDirectory(PathFull(relativeName), this.GetDirectory(PathDirectory(relativeName)));
            entry.Parent.children.Add(entry);
        }

        internal void CreateDirectory(string path)
        {
            if (!WhatIf)
            {
                Directory.CreateDirectory(PathFull(path));
                RefreshEntry(PathFull(path));
            }
            else
            {
                WhatIfAddDirectory(path);
            }

        }

        public string PathFull(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            path = HelixUtil.PathUniversal(path);

            if (Path.IsPathRooted(path))
                path = RemoveRootFromPath(path, FullName);

            return HelixUtil.PathNative(Path.Combine(this.FullName, path));
        }

        /// <summary>
        /// Returns a path relative to the root, in native format
        /// </summary>
        public string PathRelative(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            path = HelixUtil.PathUniversal(path);

            if (Path.IsPathRooted(path))
                path = RemoveRootFromPath(path, FullName);
            return path;
        }

        public string PathDirectory(string path)
        {
            var parts = PathRelative(path).Split(HelixUtil.UniversalDirectorySeparatorChar).ToList();
            if (parts.Count == 1)
                return "";
            else
            {
                parts.RemoveAt(parts.Count - 1);
                return String.Join(HelixUtil.UniversalDirectorySeparatorChar, parts);
            }
        }


        /// <summary>
        /// Creates the directory (does in virtualy in whatif mode)
        /// </summary>
        public void Create()
        {
            if (!WhatIf)
                Directory.CreateDirectory(this.FullName);

            this.Exists = true;
            this.IsLoaded = true;
        }


        /// <summary>
        /// Forces an entry to be refreshed based of the Filesystem
        /// </summary>
        public void RefreshEntry(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            path = HelixUtil.PathUniversal(path);

            if (Path.IsPathRooted(path))
                path = RemoveRootFromPath(path, FullName);

            var split = path.Split(HelixUtil.UniversalDirectorySeparatorChar);
            FSEntry newEntry;

            FileSystemInfo info;
            string fullPath = HelixUtil.JoinUniversal(this.FullName, split[0]);
            if (System.IO.Directory.Exists(fullPath))
            {
                info = new DirectoryInfo(fullPath);
            }
            else if (System.IO.File.Exists(fullPath))
            {
                info = new FileInfo(fullPath);
            }
            else
            {
                info = null;
            }

            if (info is DirectoryInfo dirInfo)
            {
                newEntry = new FSDirectory(dirInfo, this, this.WhatIf, isRoot: false);
            }
            else if (info is FileInfo fileInfo)
            {
                newEntry = new FSFile(fileInfo, this, this.WhatIf);
            }
            else
            {
                newEntry = null; //not found
            }

            children.TryGetValue(split[0], out FSEntry oldEntry);
            if (newEntry?.EntryType != oldEntry?.EntryType)
            {
                if (oldEntry != null)
                    children.Remove(oldEntry);
                if (newEntry != null)
                    children.Add(newEntry);
            }
            else if (newEntry != null && oldEntry != null)
            {
                oldEntry.PopulateFromInfo(newEntry.LastWriteTimeUtc, newEntry.Length);
                newEntry = oldEntry;
            }
            else if (newEntry != null)
            {
                children.Add(newEntry);
                if (split.Length > 1)
                    (newEntry as FSDirectory)?.RefreshEntry(HelixUtil.JoinUniversal(split.Skip(1).ToArray()));
            }
        }

        /// <summary>
        /// Returns if the file or directory exists.
        /// </summary>
        /// <param name="path">Path can be relative to the directory or absolute</param>
        public bool ChildExists(string path)
        {
            path = HelixUtil.PathUniversal(path);
            return TryGetEntry(path) != null;
        }

        /// <summary>
        /// Removes the directory. If recursive is set to true also remove children, if false throws an exception if children exist
        /// </summary>
        /// <param name="recursive"></param>
        public void Delete(bool recursive = false)
        {
            if (!recursive && this.children.Any())
                throw new IOException("Directory is not empty");

            if (!WhatIf)
                Directory.Delete(FullName, recursive);
            ((IFSDirectoryCore)Parent).Remove(this);
            this.Exists = false;
        }



        void IFSDirectoryCore.Remove(FSEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            children.Remove(entry);
        }

        void IFSDirectoryCore.Add(FSEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (entry.Parent != this)
                throw new ArgumentException(nameof(entry), "Unable to add entry, must have parent set to self");

            children.Add(entry);
        }

        /// <summary>
        /// Clears all of the cached entries
        /// </summary>
        public void Reset()
        {
            if (WhatIf)
                throw new InvalidOperationException($"Unable to perform {nameof(Reset)}() when {nameof(WhatIf)}=True");
            children.Clear();
            IsLoaded = false;
            IsLoadedDeep = false;
        }
    }




}