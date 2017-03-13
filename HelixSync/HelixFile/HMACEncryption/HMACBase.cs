using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync.HMACEncryption
{
    internal class HMACBase : IDisposable
    {
        protected readonly HMAC hmac;
        protected readonly ByteBlock prefix;

        /// <summary>
        /// Size of the blocks not including the HMAC itself
        /// </summary>
        protected int m_BlockSize = 1024 * 32;

        protected int blockId = 0;

        public HMACBase(ByteBlock prefix, HMAC hmac)
        {
            if (hmac == null)
                throw new ArgumentNullException("hmac");

            this.hmac = hmac;
            this.prefix = prefix ?? new ByteBlock(new byte[0]);
        }

        protected ByteBlock ComputeHash(int blockId, bool finalBlock, ByteBlock payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            byte[] blockIdBytes = BitConverter.GetBytes(blockId);
            byte[] finalBlockBytes = new byte[1] { finalBlock ? (byte)1 : (byte)0 };

            var byteStream = new ByteBlockStream(prefix, new ByteBlock(blockIdBytes), new ByteBlock(finalBlockBytes), payload);
            return new ByteBlock(hmac.ComputeHash(byteStream));
        }

        public void Dispose()
        {
            this.hmac.Dispose();
        }
    }
}
