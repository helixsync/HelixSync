// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    internal class CryptoStream2 : CryptoStream
    {
        private CryptoStreamMode baseMode;
        private Stream baseStream;

        public CryptoStream2(Stream stream, ICryptoTransform transform, CryptoStreamMode mode) 
            : base(stream, transform, mode)
        {
            this.baseStream = stream;
            this.baseMode = mode;
        }

        protected override void Dispose(bool disposing)
        {

            if (disposing && this.baseMode == CryptoStreamMode.Read)
            {
                base.Dispose(false); //if the final block has not been read a padding exception is thrown, this will prevent that exception
                baseStream.Dispose();
            }
            else
                base.Dispose(disposing);
        }
    }
}
