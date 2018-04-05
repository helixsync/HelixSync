﻿using System;
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


        private FSFile(string fullName, FSDirectory parent, bool whatIf)
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
            if (Path.IsPathRooted(destinationPath))
                destinationPath = RemoveRootFromPath(destinationPath, Root.FullName);

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            var newParent = Root.TryGetEntry(destinationDirectory) as FSDirectory;
            if (newParent == null)
                throw new System.IO.DirectoryNotFoundException("Could not find a part of the path");

            if (newParent.TryGetEntry(Path.GetFileName(destinationPath)) != null)
                throw new System.IO.DirectoryNotFoundException("Cannot move a file when that file already exists.");



            var newEntry = new FSFile(destinationPath, newParent, WhatIf)
            {
                m_LastWriteTimeUtc = this.LastWriteTimeUtc,
            };

            if (!WhatIf)
                File.Move(this.FullName, Path.Combine(Root.FullName, destinationPath));

            ((IFSDirectoryCore)Parent).Remove(this);
            ((IFSDirectoryCore)newParent).Add(newEntry);

            return newEntry;
        }

        /// <summary>
        /// Deletes the file
        /// </summary>
        public void Delete()
        {
            if (!WhatIf)
                File.Delete(this.FullName);
            ((IFSDirectoryCore)Parent).Remove(this);
        }
    }
}