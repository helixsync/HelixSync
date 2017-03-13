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
    internal class HMACEncrypt : HMACBase, ICryptoTransform
    {
        public HMACEncrypt(byte[] payloadPrefix, HMAC hmac)
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
            get { return m_BlockSize; }
        }

        public int OutputBlockSize
        {
            get { return m_BlockSize + hmac.HashSize / 8; }
        }

        public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        {
            ByteBlock payload = new ByteBlock(inputBuffer, inputOffset, inputCount);
            ByteBlock hash = ComputeHash(blockId, false, payload);
            blockId++;
            hash.Copy(outputBuffer, outputOffset);
            payload.Copy(outputBuffer, outputOffset + hash.Count);
            return hash.Count + payload.Count;
        }

        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            ByteBlock payload = new ByteBlock(inputBuffer, inputOffset, inputCount);
            ByteBlock hash = ComputeHash(blockId, true, payload);
            blockId++;
            int outputOffset = 0;
            byte[] outputBuffer = new byte[hash.Count + payload.Count];
            hash.Copy(outputBuffer, outputOffset);
            payload.Copy(outputBuffer, outputOffset + hash.Count);
            
            return outputBuffer;
        }
    }
}
