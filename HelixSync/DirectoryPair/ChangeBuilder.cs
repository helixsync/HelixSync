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
                DecrFileName = DecrFileName
            };
            DirectoryPair.RefreshPreSyncMode(val);
            return val;
        }
    }
}
