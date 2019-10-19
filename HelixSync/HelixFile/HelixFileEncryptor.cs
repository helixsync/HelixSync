using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    public class HelixFileEncryptor : IDisposable
    {
        readonly DerivedBytes DerivedBytes;
        readonly Stream StreamOut;
        MultiBlockEncryptor encryptor;
        private readonly HelixFileVersion FileVersion;

        public HelixFileEncryptor(Stream streamOut, string password)
            : this(streamOut, DerivedBytesProvider.FromPassword(password))
        {

        }
        public HelixFileEncryptor(Stream streamOut, DerivedBytesProvider derivedBytesProvider, HelixFileVersion fileVersion = null)
        {
            if (streamOut == null)
                throw new ArgumentNullException(nameof(streamOut));
            if (!streamOut.CanWrite)
                throw new ArgumentException("streamOut must be writable", nameof(streamOut));
            if (derivedBytesProvider == null)
                throw new ArgumentNullException(nameof(derivedBytesProvider));

            this.FileVersion = fileVersion ?? HelixFileVersion.Default;

            this.StreamOut = streamOut;
            this.DerivedBytes = derivedBytesProvider.GetDerivedBytes(FileVersion.DerivedBytesIterations);
        }

        bool headerWritten;
        public void WriteHeader(FileEntry header)
        {
            if (header == null)
                throw new ArgumentNullException(nameof(header));
            if (headerWritten)
                throw new InvalidOperationException("Header was already written");
            
            headerWritten = true;

            encryptor = new MultiBlockEncryptor(StreamOut, DerivedBytes, FileVersion);

            var headerSerialized = JsonConvert.SerializeObject(header, Formatting.Indented);
            encryptor.WriteBlock(headerSerialized);
        }
        
        private bool contentWritten;
        public void WriteContent(Stream streamIn)
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(HelixFileEncryptor));
            if (streamIn == null)
                throw new ArgumentNullException(nameof(streamIn));
            if (!streamIn.CanRead)
                throw new ArgumentException("streamIn must be readable", nameof(streamIn));

            WriteContent(streamIn, streamIn.Length);
        }
        public void WriteContent(Stream streamIn, long length)
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(HelixFileEncryptor));
            if (streamIn == null)
                throw new ArgumentNullException(nameof(streamIn));
            if (!streamIn.CanRead)
                throw new ArgumentException("stream must be readable", nameof(streamIn));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "length must be greater than or equal to 0");

            if (!headerWritten)
                throw new InvalidOperationException("Header must be written before content");

            if (contentWritten)
                throw new InvalidOperationException("Content was already written");

            encryptor.WriteBlock(streamIn, length);
            contentWritten = true;
        }
        public void WriteContent(string content)
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(HelixFileEncryptor));
            if (!headerWritten)
                throw new InvalidOperationException("Header must be written before content");
            if (contentWritten)
                throw new InvalidOperationException("Content was already written");
            
            encryptor.WriteBlock(content);
        }

        public void Flush()
        {
            encryptor.Flush();
        }

        /// <summary>
        /// Updates the underlying data source or repository with the current state of the buffer including the final encryption block then clears the buffer.
        /// </summary>
        public void FlushFinalBlock()
        {
            encryptor.FlushFinalBlock();
        }

        private bool isDisposed;

        public bool IsDisposed
        {
            get { return isDisposed; }
        }
        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;

                encryptor.Dispose();
                StreamOut.Dispose();
            }
        }
    }
}
