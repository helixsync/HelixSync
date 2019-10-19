// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Security.Cryptography;

namespace HelixSync
{
    public class DerivedBytes
    {
        public DerivedBytes(byte[] key, byte[] salt, int derivedBytesIterations)
        {
            this.Key = key ?? throw new ArgumentNullException(nameof(key));
            this.Salt = salt ?? throw new ArgumentNullException(nameof(salt));
            this.DerivedBytesIterations = derivedBytesIterations;
        }

        public byte[] Key { get; private set; }
        public byte[] Salt { get; private set; }
        public int DerivedBytesIterations { get; private set; }
    }
}
