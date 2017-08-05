// This file is part of HelixSync, which is released under GPL-3.0
// see the included LICENSE file for full details

using System;
using System.Text;

namespace HelixSync
{
    public class PreSyncDetails
    {
        public PreSyncDetails()
        {

        }

        public FileEntry DecrInfo { get; set; }
        public FileEntry EncrInfo { get; set; }
        public FileEntry EncrHeader { get; set; }
        public SyncLogEntry LogEntry { get; set; }


        public string EncrFileName { get; set; }
        public string DecrFileName { get; set; }
        
        public PreSyncMode SyncMode { get; set; }
        public FileEntryType DisplayEntryType { get; set; }
        public long DisplayFileLength { get; set; }
        public PreSyncOperation DisplayOperation { get; set; }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();

            if (DisplayEntryType == FileEntryType.Directory)
                builder.Append("<DIR> ");
            else
                builder.Append(HelixUtil.FormatBytes5(DisplayFileLength) + " "); //ex 1.5KB
            
            if (DisplayOperation == PreSyncOperation.Add)
                builder.Append("+ ");
            else if (DisplayOperation == PreSyncOperation.Remove)
                builder.Append("- ");
            else if (DisplayOperation == PreSyncOperation.Change)
                builder.Append("c ");
            else if (DisplayOperation == PreSyncOperation.Error)
                builder.Append("! ");
            else
                builder.Append("  ");

            if (SyncMode == PreSyncMode.Match || SyncMode == PreSyncMode.Unchanged)
                builder.Append("         ");
            else if (SyncMode == PreSyncMode.DecryptedSide)
                builder.Append("DEC=>ENC ");
            else if (SyncMode == PreSyncMode.EncryptedSide)
                builder.Append("ENC=>DEC ");
            else //(SyncMode == PreSyncMode.Unknown)
                builder.Append("UNKNOWN  ");

            builder.Append((EncrFileName ?? "------").Substring(0, 6) + "... ");

            builder.Append(DecrFileName ?? "");
            return builder.ToString();
        }
    }
}
