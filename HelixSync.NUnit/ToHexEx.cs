// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync.NUnit
{
    static class ToHexEx
    {
        public static string ToHex(this byte[] bytes)
        {
            string hex = BitConverter.ToString(bytes, 0, bytes.Length);
            return hex.Replace("-", "");
        }
    }
}
