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
    public class HelixWatcher
    {
        public string EncrDirectory { get; private set; }
        public string DecrDirectory { get; private set; }

        public HelixWatcher(string encrDirectory, string decrDirectory)
        {
            this.EncrDirectory = encrDirectory;
            this.DecrDirectory = decrDirectory;

        }

        FileSystemWatcher EncrWatcher;
        FileSystemWatcher DecrWatcher;
        public void Start()
        {

            EncrWatcher = new FileSystemWatcher();
            EncrWatcher.Path = EncrDirectory;
            EncrWatcher.IncludeSubdirectories = true;
            EncrWatcher.Created += EncrWatcher_Changed;
            EncrWatcher.Changed += EncrWatcher_Changed;
            EncrWatcher.Deleted += EncrWatcher_Changed;
            EncrWatcher.Renamed += EncrWatcher_Changed;
            EncrWatcher.EnableRaisingEvents = true;

            DecrWatcher = new FileSystemWatcher();
            DecrWatcher.Path = DecrDirectory;
            DecrWatcher.IncludeSubdirectories = true;
            DecrWatcher.Created += DecrWatcher_Changed;
            DecrWatcher.Changed += DecrWatcher_Changed;
            DecrWatcher.Deleted += DecrWatcher_Changed;
            DecrWatcher.Renamed += DecrWatcher_Changed;
            DecrWatcher.EnableRaisingEvents = true;
        }

        private void DecrWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                Enqueue(new HelixSync.DirectoryChange(PairSide.Decrypted, HelixUtil.RemoveRootFromPath(e.FullPath, DecrDirectory)));
            }
            else if (e.ChangeType == WatcherChangeTypes.Created ||
                e.ChangeType == WatcherChangeTypes.Changed)
            {
                Enqueue(new HelixSync.DirectoryChange(PairSide.Decrypted, HelixUtil.RemoveRootFromPath(e.FullPath, DecrDirectory)));
            }
            else if (e.ChangeType == WatcherChangeTypes.Renamed)
            {
                Enqueue(new HelixSync.DirectoryChange(PairSide.Decrypted, HelixUtil.RemoveRootFromPath((e as RenamedEventArgs).OldFullPath, DecrDirectory)));
                Enqueue(new HelixSync.DirectoryChange(PairSide.Decrypted, HelixUtil.RemoveRootFromPath(e.FullPath, DecrDirectory)));
            }
        }
        private void EncrWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                Enqueue(new HelixSync.DirectoryChange(PairSide.Encrypted, HelixUtil.RemoveRootFromPath(e.FullPath, DecrDirectory)));
            }
            else if (e.ChangeType == WatcherChangeTypes.Created ||
                e.ChangeType == WatcherChangeTypes.Changed)
            {
                Enqueue(new HelixSync.DirectoryChange(PairSide.Encrypted, HelixUtil.RemoveRootFromPath(e.FullPath, DecrDirectory)));
            }
            else if (e.ChangeType == WatcherChangeTypes.Renamed)
            {
                Enqueue(new HelixSync.DirectoryChange(PairSide.Encrypted, HelixUtil.RemoveRootFromPath((e as RenamedEventArgs).OldFullPath, DecrDirectory)));
                Enqueue(new HelixSync.DirectoryChange(PairSide.Encrypted, HelixUtil.RemoveRootFromPath(e.FullPath, DecrDirectory)));
            }
        }

        private void Enqueue(DirectoryChange change)
        {

        }
    }
}
