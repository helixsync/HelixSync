using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    class ByteBlockStream : Stream
    {
        private Queue<ByteBlock> m_underlyingArrays;
        private ByteBlock m_CurrentByteBlock;
        private int m_CurrentByteBlockPosition;
        public ByteBlockStream(params ByteBlock[] arrays)
        {
            this.m_underlyingArrays = new Queue<ByteBlock>(arrays);
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

        public override void Flush()
        {

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (m_CurrentByteBlock == null && !m_underlyingArrays.Any())
                return 0; //end of stream

            if (m_CurrentByteBlock == null)
            {
                m_CurrentByteBlock = m_underlyingArrays.Dequeue();
                m_CurrentByteBlockPosition = 0;
            }

            int bytesTaken = 0;
            while (true)
            {
                int currentBytesTaken = m_CurrentByteBlock.Copy(m_CurrentByteBlockPosition, buffer, offset + bytesTaken, count - bytesTaken);
                bytesTaken += currentBytesTaken;
                m_CurrentByteBlockPosition += currentBytesTaken;


                if (m_CurrentByteBlock.Count <= m_CurrentByteBlockPosition)
                {
                    //EOF
                    if (m_underlyingArrays.Any())
                    {
                        m_CurrentByteBlock = m_underlyingArrays.Dequeue();
                        m_CurrentByteBlockPosition = 0;
                    }
                    else
                    {
                        m_CurrentByteBlock = null;
                        m_CurrentByteBlockPosition = 0;
                        return bytesTaken;
                    }
                }

                if (bytesTaken == count)
                    return bytesTaken;
            }
        }
    }
}
