using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    public class ByteBlock
    {
        private byte[] bytes;
        private int count;
        private int offset;

        public ByteBlock(byte[] bytes)
            : this(bytes, 0, bytes.Length)
        {

        }

        public ByteBlock(byte[] bytes, int offset, int count)
        {
            if (bytes == null)
                throw new ArgumentNullException("bytes");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", "offset must be greater than or equal to zero");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", "count must be greater than or equal to zero");
            if (bytes.Length < offset + count)
                throw new Exception("offset + count exceeds bytes length");

            this.bytes = bytes;
            this.offset = offset;
            this.count = count;
        }

        public ByteBlock Subset(int offset, int length)
        {
            return new ByteBlock(bytes, Offset + offset, length);
        }

        public ByteBlock Subset(int offset)
        {
            return new ByteBlock(bytes, Offset + offset, Count - offset);
        }
        
        public bool Equals(byte[] obj)
        {
            return Equals(this.bytes, obj);
        }
        public bool Equals(ByteBlock obj)
        {
            return Equals(this, obj);
        }

        public static bool Equals(byte[] obj1, byte[] obj2)
        {
            if (obj1 == null && obj2 == null)
                return true;
            if (obj1 == null || obj2 == null)
                return false;

            return Equals(new ByteBlock(obj1), new ByteBlock(obj2));
        }

        /// <summary>
        /// Performs a constant time equality check (prevents side channel attacks)
        /// </summary>
        public static bool Equals(ByteBlock obj, ByteBlock obj2)
        {
            
            if (obj == null && obj2 == null)
                return true;
            if (obj == null || obj2 == null)
                return false;

            if (obj.Count != obj2.Count)
                return false;

            //Compare Tag with constant time comparison
            var compare = 0;
            for (var i = 0; i < obj2.count; i++)
                compare |= obj2[i] ^ obj[i];
            
            return compare == 0;
        }

        /// <summary>
        /// Performs a comparison of two identical sized byte arrays. 
        /// Performs in constant time (to prevent timing/side channel attacks)  
        /// </summary>
        public static int CompareConstantTime(byte[] bytesA, byte[] bytesB)
        {
            if (bytesA == null)
                throw new ArgumentNullException(nameof(bytesA));
            if (bytesB == null)
                throw new ArgumentNullException(nameof(bytesB));
            if (bytesA.Length != bytesB.Length)
                throw new ArgumentOutOfRangeException("byte length must be equal");

            //only the lest significant bit is used
            int aIsLarger = 0;
            int bIsLarger = 0;

            unchecked
            {
                for (int i = 0; i < bytesA.Length; i++)
                {
                    int byteA = bytesA[i];
                    int byteB = bytesB[i];

                    int byteAIsLarger = ((byteB - byteA) >> 8) & 1;
                    int byteBIsLarger = ((byteA - byteB) >> 8) & 1;

                    aIsLarger = aIsLarger | (byteAIsLarger & ~bIsLarger);
                    bIsLarger = bIsLarger | (byteBIsLarger & ~aIsLarger);
                }

                //fixes to standard compaire results (0 if A = B, 1 if A > B, -1: B > A)
                int result = aIsLarger - bIsLarger;
                return result;
            }
        }


        public byte this[int position]
        {
            get
            {
                return bytes[position + offset];
            }
        }
        
        public Byte[] Bytes
        {
            get { return bytes; }
        }
        public int Count
        {
            get { return count; }
        }
        public int Offset
        {
            get { return offset; }
        }

        public int Copy(int position, byte[] buffer, int offset, int count)
        {
            if (position > Count)
                throw new ArgumentOutOfRangeException("Position", "Position exceeds the count");

            var actualCount = Math.Min(count, Count - position);

            Array.Copy(Bytes, Offset + position, buffer, offset, actualCount);
            return actualCount;
        }
        public int Copy(byte[] outputBuffer, int outputOffset)
        {
            return this.Copy(0, outputBuffer, outputOffset, count);
        }
        public int Copy(byte[] outputBuffer)
        {
            return this.Copy(0, outputBuffer, 0, count);
        }

        public static byte[] ConcatenateBytes(params byte[][] bytes)
        {
            int length = 0;
            foreach (byte[] subbyte in bytes)
            {
                length += subbyte.Length;
            }

            byte[] returnVal = new byte[length];
            int position = 0;
            foreach (byte[] subbyte in bytes)
            {
                subbyte.CopyTo(returnVal, position);
                position += subbyte.Length;
            }

            return returnVal;
        }

        public string ToHex()
        {
            string hex = BitConverter.ToString(this.Bytes, this.Offset, this.Count);
            return hex.Replace("-", "");
        }

        public override string ToString()
        {
            return ToHex();
        }
    }
}
