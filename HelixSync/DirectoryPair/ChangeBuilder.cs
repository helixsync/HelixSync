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
