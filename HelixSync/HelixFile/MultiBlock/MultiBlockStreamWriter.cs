using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    class MultiBlockStreamWriter : IDisposable
    {
        protected readonly Stream streamOut;

        public MultiBlockStreamWriter(Stream streamOut)
        {
            if (streamOut == null)
                throw new ArgumentNullException(nameof(streamOut));
            if (!streamOut.CanWrite)
                throw new ArgumentException("streamOut must be writable", nameof(streamOut));
            this.streamOut = streamOut;
        }
        
        public void WriteBlock(Stream streamIn, long length, int bufferSize = 81920)
        {
            if (streamIn == null)
                throw new ArgumentNullException(nameof(streamIn));
            if (!streamIn.CanRead)
                throw new ArgumentException("streamIn must be readable", nameof(streamIn));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "length must be 0 or larger");
            if (bufferSize < 1)
                throw new ArgumentOutOfRangeException(nameof(bufferSize), "bufferSize must be 1 or larger");

            //Write the length of the block
            byte[] bytes = BitConverter.GetBytes(length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            streamOut.Write(bytes, 0, sizeof(long));

            //Write the data
            using (Stream limitedReader = new MultiBlockLimitedStreamReader(streamIn, length))
            {
                limitedReader.CopyTo(streamOut, bufferSize);
            }
        }

        public void WriteBlock(string stringIn)
        {
            using (MemoryStream stream = new MemoryStream())
            using (StreamWriter writer = new StreamWriter(stream))
            {
                writer.Write(stringIn);
                writer.Flush();
                stream.Position = 0;
                WriteBlock(stream, stream.Length);
            }

            //var stringBytes = System.Text.Encoding.Unicode.GetBytes(stringIn);
            //WriteBlock(new MemoryStream(stringBytes, false), stringBytes.Length);
        }

        public void Flush()
        {
            streamOut.Flush();
        }

        /// <summary>
        /// Closes the current stream and releases resources
        /// </summary>
        public void Close()
        {
            streamOut.Close();
        }

        bool isDisposed;
        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                streamOut.Close();
            }
        }
    }
}
