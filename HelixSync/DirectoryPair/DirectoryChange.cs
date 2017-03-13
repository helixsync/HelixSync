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
    public class DirectoryChange
    {
        public DirectoryChange(PairSide side, string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentNullException(nameof(fileName));
            if (Path.IsPathRooted(fileName))
                throw new ArgumentOutOfRangeException(nameof(fileName), "name should be relitive");


            this.Side = side;
            this.FileName = fileName;
        }
        public PairSide Side { get; private set; }
        public string FileName { get; private set; }
        
        public override string ToString()
        {
            return "(" + Side.ToString().Substring(0, 1) + ") " + FileName;
        }

        public override int GetHashCode()
        {
            return Side.ToString().GetHashCode() + FileName.ToString().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            DirectoryChange obj1 = obj as DirectoryChange;
            if (obj1 == null)
                return false;

            return Side == obj1.Side && FileName == obj1.FileName;
        }
    }
}
