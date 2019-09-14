using System;
using System.Collections.Generic;
using System.Text;
using HelixSync.FileSystem;
using System.Linq;

namespace HelixSync
{
    class ChangeBuilder
    {
        //three way join
        public SyncLogEntry LogEntry { get; set; }
        public FSEntry DecrInfo { get; set; }
        public FSEntry EncrInfo { get; set; }
        public string EncrFileName { get; set; }
        public string DecrFileName { get; set; }

        //load header
        public FileEntry EncrHeader { get; set; }

        //relationships
        public List<ChangeBuilder> RelationParents { get; set; }
        public List<ChangeBuilder> RelationCaseDifference { get; set; }
        public List<ChangeBuilder> RelationChildren { get; set; }


        //operations
        public PreSyncOperation DecrChange { get; set; }
        public PreSyncOperation EncrChange { get; set; }
        
        
        //dependencies
        public List<ChangeBuilder> Dependencies { get; set; }

        //sync mode
        public PreSyncMode SyncMode { get; set; }
        public List<ConflictType> Conflicts { get; } = new List<ConflictType>();
        

        public override string ToString()
        {
            string AsStrOp(PreSyncOperation op)
            {
                if (op == PreSyncOperation.Add) return "+";
                if (op == PreSyncOperation.Change) return "~";
                if (op == PreSyncOperation.Remove) return "-";
                if (op == PreSyncOperation.Error) return "!";
                if (op == PreSyncOperation.Purge) return "^";
                if (op == PreSyncOperation.None) return "";
                return "?";
            }

            string AsStrMode(PreSyncMode mode)
            {
                if (mode == PreSyncMode.Unknown) return "=?=";
                if (mode == PreSyncMode.DecryptedSide) return "=>";
                if (mode == PreSyncMode.EncryptedSide) return "<=";
                if (mode == PreSyncMode.Match) return "~=";
                if (mode == PreSyncMode.Unchanged) return "==";
                return "=?=";
            }

            return $"{DecrFileName} {AsStrOp(DecrChange)}{AsStrMode(SyncMode)}{AsStrOp(EncrChange)} {EncrFileName}";
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
                DecrFileName = DecrFileName,
                SyncMode = SyncMode
            };

            if (SyncMode == PreSyncMode.DecryptedSide)
            {
                val.DisplayEntryType = val.DecrInfo?.EntryType ?? FileEntryType.Removed;
                val.DisplayFileLength = val.DecrInfo?.Length ?? val.EncrHeader?.Length ?? 0;

                if (val.DecrInfo == null || val.DecrInfo.EntryType == FileEntryType.Removed)
                    val.DisplayOperation = PreSyncOperation.Remove;
                else if (val.EncrInfo == null || val.EncrInfo.EntryType == FileEntryType.Removed)
                    val.DisplayOperation = PreSyncOperation.Add;
                else if (val.EncrHeader?.EntryType == FileEntryType.Removed)
                    val.DisplayOperation = PreSyncOperation.Add;
                else
                    val.DisplayOperation = PreSyncOperation.Change;
            }
            else if (val.SyncMode == PreSyncMode.EncryptedSide)
            {
                val.DisplayEntryType = val.EncrHeader?.EntryType ?? FileEntryType.Removed;
                val.DisplayFileLength = val.EncrHeader?.Length ?? val.DecrInfo?.Length ?? 0;

                if ((val.EncrInfo?.EntryType == FileEntryType.Removed || val.EncrInfo?.EntryType == null)
                     && val.LogEntry.EntryType == FileEntryType.Removed)
                {
                    val.DisplayEntryType = FileEntryType.Purged;
                    val.DisplayFileLength = 0;
                    val.DisplayOperation = PreSyncOperation.Purge;
                }
                else if (val.EncrInfo == null || val.EncrInfo.EntryType == FileEntryType.Removed)
                    val.DisplayOperation = PreSyncOperation.Remove;
                else if (val.EncrHeader?.EntryType == FileEntryType.Removed)
                    val.DisplayOperation = PreSyncOperation.Remove;
                else if (val.DecrInfo == null || val.DecrInfo.EntryType == FileEntryType.Removed)
                    val.DisplayOperation = PreSyncOperation.Add;
                else
                    val.DisplayOperation = PreSyncOperation.Change;
            }
            else if (val.SyncMode == PreSyncMode.Match || val.SyncMode == PreSyncMode.Unchanged)
            {
                val.DisplayEntryType = val.DecrInfo?.EntryType ?? FileEntryType.Removed;
                val.DisplayFileLength = val.DecrInfo?.Length ?? val.EncrHeader?.Length ?? 0;

                val.DisplayOperation = PreSyncOperation.None;
            }
            else
            {
                val.DisplayEntryType = val.DecrInfo?.EntryType ?? FileEntryType.Removed;
                val.DisplayFileLength = val.DecrInfo?.Length ?? val.EncrHeader?.Length ?? 0;

                val.DisplayOperation = PreSyncOperation.Error;
            }


            return val;
        }
    }
}
