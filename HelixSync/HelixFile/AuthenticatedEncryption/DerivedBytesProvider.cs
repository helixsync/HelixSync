// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HelixSync
{
    /// <summary>
    /// Password Derived Bytes Provider
    /// Returns a derived bytes based on a password
    /// </summary>
    public class DerivedBytesProvider
    {
        private DerivedBytes lastDerivedBytes;
        private byte[] m_password;

        private DerivedBytesProvider(byte[] password)
        {
            m_password = password;
        }



        public static DerivedBytesProvider FromPassword(string password, params string[] keyFiles)
        {
            if (password == null && (keyFiles ?? new string[] { }).Length == 0)
                throw new ArgumentOutOfRangeException("A password or a key file is required");

            List<byte[]> checksums = new List<byte[]>();

            using (SHA256Managed sha = new SHA256Managed())
            {
                if (password != null)
                {
                    byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
                    {
                        byte[] checksum = sha.ComputeHash(passwordBytes);
                        checksums.Add(checksum);
                    }
                }

                foreach (string file in keyFiles ?? new string[] { })
                {
                    using (FileStream stream = File.OpenRead(file))
                    {
                        byte[] checksum = sha.ComputeHash(stream);
                        checksums.Add(checksum);
                    }
                }                    

                if (checksums.Count == 1)
                    return new DerivedBytesProvider(checksums[0]);

                
                checksums.Sort(ByteBlock.CompareConstantTime); //todo: Constant sort
                using (MemoryStream combinedHashes = new MemoryStream(checksums.Count * (sha.HashSize / 8)))
                {
                    foreach (var checksum in checksums)
                        combinedHashes.Write(checksum, 0, checksum.Length);
                    combinedHashes.Position = 0;
                    return new DerivedBytesProvider(sha.ComputeHash(combinedHashes));
                }
                

                
            }
        }

        public DerivedBytes GetDerivedBytes(int derivedBytesIterations)
        {
            if (derivedBytesIterations < 1)
                throw new ArgumentOutOfRangeException(nameof(derivedBytesIterations));

            if (lastDerivedBytes != null && lastDerivedBytes.DerivedBytesIterations == derivedBytesIterations)
                return lastDerivedBytes;

            byte[] salt = new byte[256 / 8];

            RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            return GetDerivedBytes(salt, derivedBytesIterations);
        }

        public DerivedBytes GetDerivedBytes(byte[] salt, int derivedBytesIterations)
        {
            if (derivedBytesIterations < 1)
                throw new ArgumentOutOfRangeException(nameof(derivedBytesIterations));
            if (salt == null)
                throw new ArgumentOutOfRangeException(nameof(salt));

            if (ByteBlock.Equals(salt, lastDerivedBytes?.Salt))
                return lastDerivedBytes;
            
            using (Rfc2898DeriveBytes deriveBytes = new Rfc2898DeriveBytes(m_password, salt, derivedBytesIterations))
            {
                lastDerivedBytes = new DerivedBytes(deriveBytes.GetBytes(256 / 8), deriveBytes.Salt, derivedBytesIterations);
                return lastDerivedBytes;
            }
        }
    }
}
