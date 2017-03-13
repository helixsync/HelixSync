// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    public class FileNameEncoder
    {
        private byte[] key;

        public FileNameEncoder(byte[] key)
        {
            this.key = key;
        }

        public string EncodeName(string fileName)
        {
            var hmac = new HMACSHA256(key);
            
            byte[] data = hmac.ComputeHash(Encoding.UTF8.GetBytes(fileName));
            
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
                builder.Append(data[i].ToString("x2"));
            return builder.ToString().ToUpperInvariant().Substring(0, 32) + ".hx";
        }
    }
}
