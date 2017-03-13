// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Text;

namespace HelixSync
{
    public class PreSyncDetails
    {
        public FileEntry DecrInfo { get; set; }
        public FileEntry EncrInfo { get; set; }
        public FileEntry EncrHeader { get; set; }
        public SyncLogEntry LogEntry { get; set; }
        public PairSide Side { get; set; }
        public string ShouldBeEncrName { get; set; }

        internal bool DecrChanged
        {
            get
            {
                bool decrChanged = true;
                if (LogEntry == null && DecrInfo == null)
                {
                    decrChanged = false;
                }
                else if (LogEntry == null && DecrInfo.EntryType == FileEntryType.Removed)
                {
                    decrChanged = false;
                }
                else if (LogEntry == null && DecrInfo.EntryType != FileEntryType.Removed)
                {
                    decrChanged = true;
                }
                else if (LogEntry.DecrFileName == DecrInfo.FileName
                    && LogEntry.DecrModified == DecrInfo.LastWriteTimeUtc
                    && LogEntry.EntryType == DecrInfo.EntryType)
                {
                    decrChanged = false;
                }
                return decrChanged;
            }
        }
        internal bool EncrChanged
        {
            get
            {
                bool encrChanged = true;

                if (LogEntry == null && EncrInfo == null)
                {
                    encrChanged = false;
                }
                else if (LogEntry == null && EncrInfo.EntryType == FileEntryType.Removed)
                {
                    encrChanged = false;
                }
                else if (LogEntry == null && EncrInfo.EntryType != FileEntryType.Removed)
                {
                    encrChanged = true;
                }
                else if (LogEntry.EncrFileName == EncrInfo.FileName
                    && LogEntry.EncrModified == EncrInfo.LastWriteTimeUtc)
                {
                    encrChanged = false;
                }
                return encrChanged;
            }
        }




        public string Mode2
        {
            get
            {
                bool decrChanged = DecrChanged;
                bool encrChanged = EncrChanged;


                if (encrChanged == false && decrChanged == false)
                {
                    return "<UNCHG>"; //Unchanged
                }
                else if (encrChanged == true && decrChanged == true)
                {
                    if (DecrInfo.EntryType == FileEntryType.Removed && EncrHeader == null)
                    {
                        return "<MATCH>";
                    }
                    else if (DecrInfo.LastWriteTimeUtc == EncrHeader.LastWriteTimeUtc
                        && DecrInfo.EntryType == EncrHeader.EntryType)
                    {
                        return "<MATCH>"; //Both changed however still match
                    }

                    return "<CNFLC>"; //Both changed however one is in conflict
                }
                else if (encrChanged)
                {
                    return "<ENCRP>";
                }
                else if (decrChanged)
                {
                    return "<DECRP>";
                }
                else
                {
                    return "<UNKNO>";
                }
            }
        }

        public override string ToString()
        {
            //"Key: [+] Add  [-] Remove  [c] Change  [x] Drop Delete Stub"
            //112KB + ENC=>DEC 1D3FE0... => Folder\File Name.txt

            StringBuilder builder = new StringBuilder();
            builder.Append(Mode2);
            if (Side == PairSide.Decrypted)
            {
                if (DecrInfo.EntryType == FileEntryType.Directory)
                    builder.Append("<DIR> ");
                else if (EncrHeader?.EntryType == FileEntryType.Removed)
                    builder.Append("*DEL* ");
                else
                    builder.Append(FormatBytes5(DecrInfo.Length) + " ");
                //builder.Append(Mode + " ");
                builder.Append("DEC=>ENC ");
                builder.Append(ShouldBeEncrName.Substring(0, 6) + "... ");
                builder.Append(DecrInfo.FileName);

                return builder.ToString();
            }
            else //if (Side == PairSide.Encrypted)
            {
                if (EncrHeader.EntryType == FileEntryType.Directory)
                    builder.Append("<DIR> ");
                else if (EncrHeader.EntryType == FileEntryType.Removed)
                    builder.Append("*DEL* ");
                else
                    builder.Append(FormatBytes5(EncrHeader.Length) + " ");
                //builder.Append(Mode + " ");
                builder.Append("ENC=>DEC ");
                builder.Append(ShouldBeEncrName.Substring(0, 6) + "... ");
                builder.Append(EncrHeader.FileName);

                return builder.ToString();
            }
        }

        /// <summary>
        /// Formats a size to ensure that it can fit in 5 characters.
        /// It will adjusting the decimal percision as necessary.
        /// </summary>
        private static string FormatBytes5(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            double dblSByte = bytes;
            foreach (string unit in units)
            {
                if (dblSByte < 1000 && unit == "B") //Byte should never show a decimal
                    return string.Format("{0,3:0}{1,-2}", dblSByte, unit);
                else if (dblSByte < 10)
                    return string.Format("{0,3:0.0}{1,-2}", dblSByte, unit);
                else if (dblSByte >= 10 && dblSByte <= 1000)
                    return string.Format("{0,3:0}{1,-2}", dblSByte, unit);

                dblSByte = dblSByte / 1024.0;
            }

            return "MAX  ";
        }
    }
}
