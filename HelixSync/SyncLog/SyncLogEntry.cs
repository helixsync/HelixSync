// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HelixSync
{
    public class SyncLogEntry
    {
        private SyncLogEntry()
        {

        }
        
        public SyncLogEntry(FileEntryType entryType, string decrFileName, DateTime decrModified, string encrFileName, DateTime encrModified)
        {
            if (string.IsNullOrWhiteSpace(decrFileName))
                throw new ArgumentNullException(nameof(decrFileName));
            if (string.IsNullOrWhiteSpace(encrFileName))
                throw new ArgumentNullException(nameof(encrFileName));
            if (!HelixUtil.IsValidPath(decrFileName))
                throw new ArgumentOutOfRangeException(nameof(decrFileName), "Invalid Path '" + decrFileName + "'");
            if (!HelixUtil.IsValidPath(encrFileName))
                throw new ArgumentOutOfRangeException(nameof(encrFileName), "Invalid Path '" + encrFileName + "'");

            this.EntryType = entryType;

            this.DecrFileName = HelixUtil.PathUniversal(decrFileName);

            if (entryType != FileEntryType.File)
                this.DecrModified = DateTime.MinValue;
            else
                this.DecrModified = HelixUtil.TruncateTicks(decrModified);

            this.EncrFileName = HelixUtil.PathUniversal(encrFileName);
            this.EncrModified = HelixUtil.TruncateTicks(encrModified);
        }

        public const string dateFormat = "yyyyMMdd't'HHmmssfff";
        const string dateZero =          "00000000t000000000";

        private static Regex lineParse = new Regex(@"^" 
                            + @"(?<Type>[DF\-\~]?) "
                            + @"(?<DecrModified>[0-9]{8}[tT][0-9]{9}) "
                            + @"(?<DecrFileName>" + HelixUtil.QuotedPattern + @") "
                            + @"(?<EncrModified>[0-9]{8}[tT][0-9]{9}) "
                            + @"(?<EncrFileName>" + HelixUtil.QuotedPattern + @")$"
                        , RegexOptions.Compiled);

        public static SyncLogEntry TryParseFromString(string line)
        {
            var match = lineParse.Match(line);
            if (!match.Success)
                return null;

            var typeFlag = match.Groups["Type"].Value;

            DateTime decrModified;
            if (match.Groups["DecrModified"].Value == dateZero)
                decrModified = DateTime.MinValue;
            else if (!DateTime.TryParseExact(match.Groups["DecrModified"].Value, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out decrModified))
                return null;

            string decrFileName = HelixUtil.Unquote(match.Groups["DecrFileName"].Value);

            DateTime encrModified;
            if (match.Groups["EncrModified"].Value == dateZero)
                encrModified = DateTime.MinValue;
            else  if (!DateTime.TryParseExact(match.Groups["EncrModified"].Value, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out encrModified))
                return null;

            string encrFileName = HelixUtil.Unquote(match.Groups["EncrFileName"].Value);

            return new SyncLogEntry
            {
                DecrFileName = HelixUtil.PathUniversal(decrFileName),
                DecrModified = HelixUtil.TruncateTicks(decrModified),
                EncrFileName = HelixUtil.PathUniversal(encrFileName),
                EncrModified = HelixUtil.TruncateTicks(encrModified),
                EntryTypeFlag = typeFlag,
            };
        }
        
        public string DecrFileName { get; private  set; }
        public DateTime DecrModified { get; private set; }
        
        public string EncrFileName { get; private set; }
        public DateTime EncrModified { get; private set; }
        
        public string Key
        {
            get
            {
                return string.Format("\"{0}\" \"{1}\"", DecrFileName, EncrFileName);
            }
        }

        private string EntryTypeFlag
        {
            get
            {
                string entryTypeFlag;
                if (EntryType == FileEntryType.Directory)
                    entryTypeFlag = "D";
                else if (EntryType == FileEntryType.File)
                    entryTypeFlag = "F";
                else if (EntryType == FileEntryType.Removed)
                    entryTypeFlag = "-";
                else if (EntryType == FileEntryType.Purged)
                    entryTypeFlag = "~";
                else
                    entryTypeFlag = "?";
                return entryTypeFlag;
            }
            set
            {
                if (value == "D")
                    EntryType = FileEntryType.Directory;
                else if (value == "F")
                    EntryType = FileEntryType.File;
                else if (value == "-")
                    EntryType = FileEntryType.Removed;
                else if (value == "~")
                    EntryType = FileEntryType.Purged;
                else
                    EntryType = FileEntryType.File;
            }
        }

        public FileEntryType EntryType { get; private set; }

        public override string ToString()
        {
            return string.Format("{0} {1} {2} {3} {4}",
                                    EntryTypeFlag,
                                    (DecrModified == DateTime.MinValue ? dateZero : DecrModified.ToString(dateFormat)),
                                    HelixUtil.Quote(DecrFileName),
                                    (EncrModified == DateTime.MinValue ? dateZero : EncrModified.ToString(dateFormat)),
                                    HelixUtil.Quote(EncrFileName));
        }
    }
}
