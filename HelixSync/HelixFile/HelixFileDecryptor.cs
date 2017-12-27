using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    public class HelixFileDecryptor : IDisposable
    {
        private Stream streamIn;

        public HelixFileDecryptor(Stream streamIn)
        {
            if (streamIn == null)
                throw new ArgumentNullException(nameof(streamIn));
            if (!streamIn.CanRead)
                throw new ArgumentException("streamIn must be readable", nameof(streamIn));

            this.streamIn = streamIn;
        }

        MultiBlockDecryptor decryptor;
        
        bool m_Initialized;
        bool m_HeaderRead;
        bool m_IsDisposed;

        public void Initialize(DerivedBytesProvider derivedBytesProvider, Action<Dictionary<string, byte[]>> afterHeaderRead = null)
        {
            if (m_IsDisposed)
                throw new ObjectDisposedException(nameof(HelixFileDecryptor));
            if (m_Initialized)
                throw new InvalidOperationException("Decryptor has already been initialized");
            if (derivedBytesProvider == null)
                throw new ArgumentNullException(nameof(derivedBytesProvider));

            try
            {
                decryptor = new MultiBlockDecryptor(streamIn);
                decryptor.Initialize(derivedBytesProvider, afterHeaderRead);
                m_Initialized = true;
            }
            catch (FileDesignatorException ex)
            {
                throw new UnsupportedFileTypeException("Unsupported file type", ex);
            }
            catch (HMACAuthenticationException ex)
            {
                throw new InvalidPasswordException("Invalid password", ex);
            }
        }


        public FileEntry ReadHeader(Action<string> afterRawMetadata = null)
        {
            if (m_IsDisposed)
                throw new ObjectDisposedException(nameof(HelixFileDecryptor));
            if (!m_Initialized)
                throw new InvalidOperationException("Decryptor has not been initialized");
            if (m_HeaderRead)
                throw new InvalidOperationException("Header previously been read");

            m_HeaderRead = true;
            try
            {
                string headerSerialized = GetContentString(10 * 1024 * 1024);
                afterRawMetadata?.Invoke(headerSerialized);
                FileEntry header = JsonConvert.DeserializeObject<FileEntry>(headerSerialized);
                return header;
            }
            catch(HMACException ex)
            {
                throw new FileCorruptionException("HMAC Error, most likely file corruption, " + ex.Message, ex);
            }
        }


        private class StreamWrapper : Stream
        {
            private Stream m_ParentStream;

            public StreamWrapper(Stream parentStream)
            {
                m_ParentStream = parentStream;
            }

            public override bool CanRead
            {
                get { return m_ParentStream.CanRead; }
            }

            public override bool CanSeek
            {
                get { return m_ParentStream.CanSeek; }
            }

            public override bool CanWrite
            {
                get { return m_ParentStream.CanWrite; }
            }

            public override long Length
            {
                get { return m_ParentStream.Length; }
            }

            public override long Position
            {
                get { return m_ParentStream.Position; }
                set { m_ParentStream.Position = value; }
            }

            public override void Flush()
            {
                m_ParentStream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                try
                {
                    return m_ParentStream.Read(buffer, offset, count);
                }
                catch (HMACException ex)
                {
                    throw new FileCorruptionException("HMAC Error, most likely file corruption, " + ex.Message, ex);
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return m_ParentStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                m_ParentStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                m_ParentStream.Write(buffer, offset, count);
            }
        }
        public Stream GetContentStream()
        {
            if (m_IsDisposed)
                throw new ObjectDisposedException(nameof(HelixFileDecryptor));
            if (!m_Initialized)
                throw new InvalidOperationException("Must initialize before retreiving the content stream");
            if (!m_HeaderRead)
                throw new InvalidOperationException("Must read header before retrieving content stream");

            return new StreamWrapper(decryptor.NextBlockAsStream());
        }
        public string GetContentString(int maxLength = 10*1024*1024)
        {
            if (m_IsDisposed)
                throw new ObjectDisposedException(nameof(HelixFileDecryptor));
            if (!m_Initialized)
                throw new InvalidOperationException("Must initialize before retreiving the content stream");
            if (!m_HeaderRead)
                throw new InvalidOperationException("Must read header before retrieving content stream");

            int remainingLength = maxLength;

            using (Stream contentOut = GetContentStream())
            using (StreamReader reader = new StreamReader(contentOut))
            {
                StringBuilder sw = new StringBuilder();
                while (true)
                {
                    char[] buffer = new char[4096];
                    int length = reader.Read(buffer, 0, buffer.Length > remainingLength ? remainingLength : buffer.Length);
                    sw.Append(buffer, 0, length);
                    remainingLength -= length;
                    if (reader.EndOfStream)
                        return sw.ToString();
                    if (remainingLength == 0 && !reader.EndOfStream)
                        throw new FileCorruptionException("Block exceeds the maximum size (" + maxLength.ToString() + ")");
                }
            }
        }

        public DerivedBytes DerivedBytes
        {
            get
            {
                if (m_IsDisposed)
                    throw new ObjectDisposedException(nameof(HelixFileDecryptor));

                return decryptor.DerivedBytes;
            }
        }
        public HelixFileVersion FileVersion
        {
            get
            {
                if (m_IsDisposed)
                    throw new ObjectDisposedException(nameof(HelixFileDecryptor));

                return decryptor.FileVersion;
            }
        }


        public bool IsDisposed
        {
            get { return m_IsDisposed; }
        }

        /// <summary>
        /// Disposes this object and the underlying stream
        /// </summary>
        public void Dispose()
        {
            if (!m_IsDisposed)
            {
                m_IsDisposed = true;

                decryptor.Dispose();
                streamIn.Dispose();
            }
        }
    }
}
