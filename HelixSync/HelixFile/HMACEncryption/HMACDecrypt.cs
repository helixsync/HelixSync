using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync.HMACEncryption
{
    internal class HMACDecrypt : HMACBase, ICryptoTransform
    {
        public HMACDecrypt(byte[] payloadPrefix, HMAC hmac)
            : base(new ByteBlock(payloadPrefix), hmac)
        {
        }

        public bool CanReuseTransform
        {
            get { return false; }
        }

        public bool CanTransformMultipleBlocks
        {
            get { return false; }
        }

        public int InputBlockSize
        {
            get { return m_BlockSize + hmac.HashSize / 8; }
        }

        public int OutputBlockSize
        {
            get { return m_BlockSize; }
        }

        private ByteBlock checkHash(int blockID, bool finalBlock, ByteBlock input)
        {
            if (input.Count < (hmac.HashSize / 8))
                throw new HMACException("Block insufficient size");

            ByteBlock hash = input.Subset(0, hmac.HashSize / 8);
            ByteBlock payload = input.Subset(hmac.HashSize / 8);

            ByteBlock computedHash = ComputeHash(blockID, finalBlock, payload);
            if (!computedHash.Equals(hash))
                throw new HMACException("HMAC does not match");
            return payload;
        }

        public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        {
            var input = new ByteBlock(inputBuffer, inputOffset, inputCount);
            var output = checkHash(blockId, false, input);
            output.Copy(outputBuffer, outputOffset);
            blockId++;
            return output.Count;
        }

        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            var input = new ByteBlock(inputBuffer, inputOffset, inputCount);
            var output = checkHash(blockId, true, input);
            var outputBytes = new byte[output.Count];
            output.Copy(outputBytes);
            blockId++;
            return outputBytes;
        }
    }
}
