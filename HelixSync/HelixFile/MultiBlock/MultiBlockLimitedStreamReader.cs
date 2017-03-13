using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    class MultiBlockLimitedStreamReader : Stream
    {
        private Stream streamIn;
        private long remaining;
        private long length;

        public MultiBlockLimitedStreamReader(Stream streamIn, long length)
        {
            if (streamIn == null)
                throw new ArgumentNullException(nameof(streamIn));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "length must be greater than or equal to 0");

            this.streamIn = streamIn;
            this.remaining = length;
            this.length = length;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (remaining == 0)
                return 0;

            int num = streamIn.Read(buffer, offset, remaining < count ? (int)remaining : count);
            if (num == 0 && remaining > 0 && count > 0)
                throw new EndOfStreamException("Unexpected end of stream");

            remaining -= num;
            return num;

        }

        #region stream
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
            get { return length; }
        }

        public override long Position
        {
            get { return length - remaining; }
            set { throw new NotSupportedException(); }
        }

        public override void Flush()
        {
            streamIn.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }


        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
        #endregion
    }
}
