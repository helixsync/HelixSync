using System;
using System.Collections.Generic;
using System.Text;
using HelixSync.FileSystem;
using System.Linq;

namespace HelixSync
{
    class PreSyncBuilder
    {
        //three way join
        public SyncLogEntry LogEntry { get; set; }
        public FSEntry DecrInfo { get; set; }
        public FSEntry EncrInfo { get; set; }
        public string EncrFileName { get; set; }
        public string DecrFileName { get; set; }

        //load header
        public FileEntry EncrHeader { get; set; }

        //add relationships
        public List<PreSyncBuilder> RelationParents { get; internal set; }
        public List<PreSyncBuilder> RelationCaseDifference { get; internal set; }
        public List<PreSyncBuilder> RelationChildren { get; internal set; }


        public PreSyncOperation DecrChange
        {
            get
            {
                if (LogEntry == null || LogEntry.EntryType == FileEntryType.Removed || LogEntry.EntryType == FileEntryType.Purged)
                {
                    if (DecrInfo == null || DecrInfo.EntryType == FileEntryType.Removed)
                        return PreSyncOperation.None;
                    else
                        return PreSyncOperation.Add;
                }
                else
                {
                    if (DecrInfo == null || DecrInfo.EntryType == FileEntryType.Removed)
                    {
                        return PreSyncOperation.Remove;
                    }
                    else if (LogEntry.EntryType == DecrInfo.EntryType
                        && LogEntry.DecrFileName == DecrInfo.RelativePath
                        && LogEntry.DecrModified == DecrInfo.LastWriteTimeUtc)
                    {
                        return PreSyncOperation.None;
                    }
                    else
                    {
                        return PreSyncOperation.Change;
                    }
                }
            }
        }
        public PreSyncOperation EncrChange
        {
            get
            {
                //test if the encr info matches the log (this is the quicker method)
                if (LogEntry == null || LogEntry.EntryType == FileEntryType.Removed || LogEntry.EntryType == FileEntryType.Purged)
                {
                    if (EncrInfo == null || EncrInfo.EntryType == FileEntryType.Removed || EncrInfo.EntryType == FileEntryType.Purged)
                        return PreSyncOperation.None;
                }

                if (LogEntry != null && EncrInfo != null)
                {
                    if (LogEntry.EncrModified == EncrInfo.LastWriteTimeUtc)
                        return PreSyncOperation.None; 
                }

                //test if the header matches the log
                if (LogEntry == null || LogEntry.EntryType == FileEntryType.Removed || LogEntry.EntryType == FileEntryType.Purged)
                {
                    if (EncrHeader.EntryType == FileEntryType.Removed || EncrHeader.EntryType == FileEntryType.Purged)
                        return PreSyncOperation.None;
                    else
                        return PreSyncOperation.Add;
                }
                else
                {
                    if (LogEntry.EntryType == EncrHeader.EntryType
                        && LogEntry.DecrFileName == EncrHeader.FileName
                        && LogEntry.DecrModified == EncrHeader.LastWriteTimeUtc)
                    {
                        return PreSyncOperation.None;
                    }
                    else if (EncrHeader.EntryType == FileEntryType.Removed)
                        return PreSyncOperation.Remove;
                    else
                        return PreSyncOperation.Change;
                }

            }
        }

        public List<PreSyncBuilder> GetDependencies()
        {
            List<PreSyncBuilder> dependencies = new List<PreSyncBuilder>();

            if (DecrInfo == null || DecrInfo.EntryType == FileEntryType.Removed || DecrInfo.EntryType == FileEntryType.Purged)
                RelationChildren?.ForEach(r => dependencies.Add(r));
            else
            {
                RelationParents?.ForEach(r => dependencies.Add(r));
                RelationCaseDifference?.Where(r => r != this && r.DecrChange == PreSyncOperation.Remove)?.ToList()?.ForEach(r => dependencies.Add(r));
            }



            if (EncrHeader == null || EncrHeader.EntryType == FileEntryType.Removed || EncrHeader.EntryType == FileEntryType.Purged)
                RelationChildren?.ForEach(r => dependencies.Add(r));
            else
            {
                RelationParents?.ForEach(r => dependencies.Add(r));
                RelationCaseDifference?.Where(r => r != this && r.EncrChange == PreSyncOperation.Remove)?.ToList()?.ForEach(r => dependencies.Add(r));
            }

            return dependencies;
        }

        public override string ToString()
        {
            return $"{(EncrFileName ?? "------").Substring(0, 6)}... {DecrFileName}";
        }

        public PreSyncDetails ToSyncEntry()
        {
            var val = new PreSyncDetails
            {
                LogEntry = LogEntry,
                DecrInfo = DecrInfo,
                EncrInfo = EncrInfo,
                EncrHeader = EncrHeader,
                EncrFileName = EncrFileName,
                DecrFileName = DecrFileName
            };
            DirectoryPair.RefreshPreSyncMode(val);
            return val;
        }
    }
}
