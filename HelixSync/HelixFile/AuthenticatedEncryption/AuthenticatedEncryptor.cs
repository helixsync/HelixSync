// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using HelixSync.HMACEncryption;

namespace HelixSync
{
    class AuthenticatedEncryptor : Stream
    {
        Stream streamOut;

        HMACEncrypt hmacTransform;
        HMACSHA256 hmacHash;
        CryptoStream hmacStream;

        AesManaged aesTransform;
        CryptoStream aesStream;

        GZipStream gzipStream;

        public AuthenticatedEncryptor(Stream streamOut, DerivedBytes derivedBytes, HelixFileVersion hxVersion, byte[] hmacSalt = null, byte[] iv = null)
        {
            if (streamOut == null)
                throw new ArgumentNullException(nameof(streamOut));
            if (derivedBytes == null)
                throw new ArgumentNullException(nameof(derivedBytes));
            if (hxVersion == null)
                throw new ArgumentNullException(nameof(hxVersion));
            if (hmacSalt != null && hmacSalt.Length != HelixConsts.HMACSaltSize)
                throw new ArgumentOutOfRangeException(nameof(hmacSalt));
            if (iv != null && iv.Length != HelixConsts.IVSize)
                throw new ArgumentOutOfRangeException(nameof(iv));

            using (var random = RandomNumberGenerator.Create())
            {
                if (hmacSalt == null)
                {
                    hmacSalt = new byte[HelixConsts.HMACSaltSize];
                    random.GetBytes(hmacSalt);
                }

                if (iv == null)
                {
                    iv = new byte[HelixConsts.IVSize];
                    random.GetBytes(iv);
                }
            }

            FileHeader header = new FileHeader
            {
                fileDesignator = hxVersion.FileDesignator,
                passwordSalt = derivedBytes.Salt,
                hmacSalt = hmacSalt,
                iv = iv,
            };

            byte[] hmacFullKey = ByteBlock.ConcatenateBytes(hmacSalt, derivedBytes.Key);


            this.streamOut = streamOut;

            byte[] bytesToHash = header.GetBytesToHash();
            streamOut.Write(bytesToHash, 0, bytesToHash.Length);

            hmacHash = new HMACSHA256(hmacFullKey);

            header.headerAuthnDisk = hmacHash.ComputeHash(header.GetBytesToHash());
            streamOut.Write(header.headerAuthnDisk, 0, header.headerAuthnDisk.Length);

            hmacTransform = new HMACEncrypt(header.headerAuthnDisk, hmacHash);
            hmacStream = new CryptoStream(streamOut, hmacTransform, CryptoStreamMode.Write);

            aesTransform = new AesManaged { KeySize = 256, BlockSize = 128, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7, IV = iv, Key = derivedBytes.Key };
            aesStream = new CryptoStream(hmacStream, aesTransform.CreateEncryptor(), CryptoStreamMode.Write);

            gzipStream = new GZipStream(aesStream, CompressionMode.Compress, true);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            gzipStream.Write(buffer, offset, count);
        }

        public override void Flush()
        {
            gzipStream.Flush();
            aesStream.Flush();
            hmacStream.Flush();
            streamOut.Flush();
        }

        /// <summary>
        /// Updates the underlying data source or repository with the current state of the buffer including the final encryption block then clears the buffer.
        /// </summary>
        public void FlushFinalBlock()
        {
            gzipStream.Close();
            aesStream.FlushFinalBlock();
            hmacStream.Flush();
            streamOut.Flush();
        }

        bool isDisposed;
        protected override void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                isDisposed = true;
                base.Dispose(disposing);

                //Dispose in the reverse order of creation
                gzipStream.Dispose();

                aesStream.Dispose();
                aesTransform.Dispose();

                hmacStream.Dispose();
                hmacTransform.Dispose();
                hmacHash.Dispose();
            }
        }



        #region StreamStuff
        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
        #endregion
    }
}
