// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading.Tasks;
using HelixSync.HMACEncryption;

namespace HelixSync
{
    class AuthenticatedDecryptor : Stream
    {
        bool initialized = false;
#pragma warning disable IDE0069 // Disposable fields should be disposed
        readonly Stream streamIn;
#pragma warning restore IDE0069 // Disposable fields should be disposed

        HMACDecrypt hmacTransform;
        HMACSHA256 hmacHash;
        CryptoStream2 hmacStream;

        Aes aesTransform;
        CryptoStream2 aesStream;

        GZipStream gzipStream;

        public DerivedBytes DerivedBytes { get; private set; }
        public HelixFileVersion FileVersion { get; private set; }

        
        public AuthenticatedDecryptor(Stream streamIn)
        {
            if (streamIn == null)
                throw new ArgumentNullException(nameof(streamIn));
            if (!streamIn.CanRead)
                throw new ArgumentException("streamIn must support read", nameof(streamIn));

            this.streamIn = streamIn;
        }
        
        public void Initialize(DerivedBytesProvider derivedBytesProvider, Action<Dictionary<string,byte[]>> afterHeaderRead = null)
        {
            if (derivedBytesProvider == null)
                throw new ArgumentNullException(nameof(derivedBytesProvider));

            //header
            //FileDesignator (8 bytes) + passwordSalt (32 bytes) + hmac salt (32 bytes) + iv (32 bytes)
            FileHeader header = new FileHeader();

            StreamRead(streamIn, header.fileDesignator);
            StreamRead(streamIn, header.passwordSalt);
            StreamRead(streamIn, header.hmacSalt);
            StreamRead(streamIn, header.iv);
            StreamRead(streamIn, header.headerAuthnDisk);

            afterHeaderRead?.Invoke(header.ToDictionary());


            FileVersion = HelixFileVersion.GetVersion(header.fileDesignator);
            if (FileVersion == null)
                throw new FileDesignatorException("Invalid file format, file designator not correct");

            DerivedBytes = derivedBytesProvider.GetDerivedBytes(header.passwordSalt, FileVersion.DerivedBytesIterations);

            byte[] headerBytes = header.GetBytesToHash();
            byte[] hmacFullKey = ByteBlock.ConcatenateBytes(header.hmacSalt, DerivedBytes.Key);

            hmacHash = new HMACSHA256(hmacFullKey);


            //Validate Header HMAC
            byte[] headerAuthnComputed = hmacHash.ComputeHash(headerBytes);
            if (!ByteBlock.Equals(header.headerAuthnDisk, headerAuthnComputed))
                throw new HMACAuthenticationException("Header HMAC Authentication Failed");

            hmacTransform = new HMACDecrypt(headerAuthnComputed, hmacHash);
            hmacStream = new CryptoStream2(streamIn, hmacTransform, CryptoStreamMode.Read);

            aesTransform = Aes.Create();
            aesTransform.KeySize = 256;
            aesTransform.BlockSize = 128;
            aesTransform.Mode = CipherMode.CBC;
            aesTransform.Padding = PaddingMode.PKCS7;
            aesTransform.IV = header.iv;
            aesTransform.Key = DerivedBytes.Key;
            
            aesStream = new CryptoStream2(hmacStream, aesTransform.CreateDecryptor(), CryptoStreamMode.Read);

            gzipStream = new GZipStream(aesStream, CompressionMode.Decompress, true);

            initialized = true;
        }

        static void StreamRead(Stream stream, byte[] bytes)
        { 
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            int offset = 0;
            while (offset < bytes.Length)
            {
                int count = stream.Read(bytes, offset, bytes.Length - offset);
                if (count == 0)
                    throw new AuthenticatedEncryptionException("Unexpected end of stream");
                offset += count;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (gzipStream == null)
                throw new InvalidOperationException("Object not initialized or has been disposed");

            return gzipStream.Read(buffer, offset, count);
        }

        public override void Flush()
        {
            if (initialized)
            {
                gzipStream.Flush();
                aesStream.Flush();
                hmacStream.Flush();
                streamIn.Flush();
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (initialized)
            {
                gzipStream.Dispose();

                aesStream.Dispose();
                aesTransform.Dispose();

                hmacStream.Dispose();
                hmacTransform.Dispose();
                hmacHash.Dispose();
            }
        }

        #region StreamStuff
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
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

        

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
        #endregion


        public override ValueTask DisposeAsync()
        {
            streamIn.Dispose();
            return base.DisposeAsync();
        }

    }
}
