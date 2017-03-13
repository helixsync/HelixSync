using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    class MultiBlockStreamReader : IDisposable
    {
        Stream streamIn;
        MultiBlockLimitedStreamReader activeBlock;
        public MultiBlockStreamReader(Stream streamIn)
        {
            if (streamIn == null)
                throw new ArgumentNullException(nameof(streamIn));
            if (!streamIn.CanRead)
                throw new ArgumentException("streamIn must be readable", nameof(streamIn));

            this.streamIn = streamIn;
        }

        public Stream NextBlockAsStream()
        {
            if (activeBlock != null && activeBlock.Position != activeBlock.Length)
                throw new InvalidOperationException("Unable to retrieve the next block untill the previous block has been fully read");

            byte[] bytes = new byte[sizeof(long)];
            streamIn.Read(bytes, 0, sizeof(long));
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            long length = BitConverter.ToInt64(bytes, 0);
            activeBlock = new MultiBlockLimitedStreamReader(streamIn, length);

            return activeBlock;
        }


        bool isDisposed;
        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                streamIn.Dispose();
            }
        }
    }
}
