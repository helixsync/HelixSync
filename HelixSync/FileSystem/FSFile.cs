using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HelixSync.FileSystem
{
    public class FSFile : FSEntry
    {
        internal FSFile(FileInfo fileInfo, FSDirectory parent, bool whatIf)
            : base(fileInfo.FullName, parent, whatIf)
        {
            this.PopulateFromInfo(fileInfo);
        }


        internal FSFile(string fullName, FSDirectory parent, bool whatIf)
            : base(fullName, parent, whatIf)
        {

        }

        /// <summary>
        /// Moves or renames the file. 
        /// </summary>
        /// <param name="destinationPath">Should be an absolute path or relitive to the root</param>
        public FSFile MoveTo(string destinationPath)
        {
            if (string.IsNullOrEmpty(destinationPath))
                throw new ArgumentNullException(nameof(destinationPath));

            destinationPath = HelixUtil.PathUniversal(destinationPath);

            if (Path.IsPathRooted(destinationPath))
                destinationPath = RemoveRootFromPath(destinationPath, Root.FullName);

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (Root.TryGetEntry(destinationDirectory) as FSDirectory == null)
                throw new System.IO.DirectoryNotFoundException("Could not find a part of the path");

            if ((Root.TryGetEntry(destinationDirectory) as FSDirectory).TryGetEntry(Path.GetFileName(destinationPath)) != null)
                throw new System.IO.DirectoryNotFoundException("Cannot move a file when that file already exists.");

            var newEntry = new FSFile(HelixUtil.JoinUniversal(Root.FullName, destinationPath), Root.TryGetEntry(destinationDirectory) as FSDirectory, WhatIf);
            newEntry.PopulateFromInfo(this.LastWriteTimeUtc, this.Length);

            if (!WhatIf)
                File.Move(HelixUtil.PathNative(this.FullName), HelixUtil.PathNative(Path.Combine(Root.FullName, destinationPath)));

            ((IFSDirectoryCore)Parent).Remove(this);
            ((IFSDirectoryCore)(Root.TryGetEntry(destinationDirectory) as FSDirectory)).Add(newEntry);

            return newEntry;
        }

        /// <summary>
        /// Deletes the file
        /// </summary>
        public void Delete()
        {
            if (!WhatIf)
                File.Delete(HelixUtil.PathNative(this.FullName));
            ((IFSDirectoryCore)Parent).Remove(this);
            this.Exists = false;
        }
    }
}
