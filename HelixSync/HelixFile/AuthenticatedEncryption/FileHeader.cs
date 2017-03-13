// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.IO;

namespace HelixSync
{
    class FileHeader
    {
        public byte[] fileDesignator = new byte[HelixConsts.FileDesignatorSize];
        public byte[] passwordSalt = new byte[32];
        public byte[] hmacSalt = new byte[HelixConsts.HMACSaltSize];
        public byte[] iv = new byte[HelixConsts.IVSize];
        public byte[] headerAuthnDisk = new byte[32];

        public byte[] GetBytesToHash()
        {
            return ByteBlock.ConcatenateBytes(fileDesignator, passwordSalt, hmacSalt, iv);
        }

        public Dictionary<string, byte[]> ToDictionary()
        {
            return new Dictionary<string, byte[]>() {
                { nameof(fileDesignator), fileDesignator},
                { nameof(passwordSalt), passwordSalt},
                { nameof(hmacSalt), hmacSalt },
                { nameof(iv), iv },
                { nameof(headerAuthnDisk), headerAuthnDisk }
            };
        }
    }
}
