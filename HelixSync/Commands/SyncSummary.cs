using System;
using System.Collections.Generic;
using System.Text;

namespace HelixSync.Commands
{
    public class SyncSummary
    {
        public int DecrAdd { get; set; }
        public int DecrRemove { get; set; }
        public int DecrChange { get; set; }
        public int DecrOther { get; set; }

        public int EncrAdd { get; set; }
        public int EncrRemove { get; set; }
        public int EncrChange { get; set; }
        public int EncrOther { get; set; }

        public int Conflict { get; set; }
        public Exception Error { get; set; }

        public int DecrTotal => DecrAdd + DecrRemove + DecrChange + DecrOther;
        public int EncrTotal => EncrAdd + EncrRemove + EncrChange + EncrOther;
        public int Total => DecrTotal + EncrTotal;

        public override string ToString()
        {
            return ""
                + $"           | Add     | Remove  | Change  | Other   |" + Environment.NewLine
                + $"ENC->DEC   | {EncrAdd,7} | {EncrRemove,7} | {EncrChange,7} | {EncrOther,7} |" + Environment.NewLine
                + $"DEC->ENC   | {DecrAdd,7} | {DecrRemove,7} | {DecrChange,7} | {DecrOther,7} |" + Environment.NewLine
                + $"Other      |     --- |     --- |     --- | {Conflict,7:#,0} |";
        }
    }
}
