// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelixSync.Test
{
    [TestClass]
    public class ByteBlock_Tests
    {
        [TestMethod]
        public void ByteBlock_CompareConstantTime_SimpleZeroOneTest()
        {
            Assert.AreEqual(ByteBlock.CompareConstantTime(new byte[] { 0 }, new byte[] { 0 }), 0);
            Assert.AreEqual(ByteBlock.CompareConstantTime(new byte[] { 1 }, new byte[] { 0 }), 1);
            Assert.AreEqual(ByteBlock.CompareConstantTime(new byte[] { 0 }, new byte[] { 1 }), -1);            
        }

#if FSCHECK
        [FsCheck.NUnit.Property]
        public void ByteBlock_CompareConstantTime_RandomTests(byte a, byte b, byte c, byte d)
        {
            try
            {
                Console.WriteLine("a:{0}, b: {1}, c:{2}", a, b, c);
                Assert.AreEqual(ByteBlock.CompareConstantTime(new byte[] { a }, new byte[] { b }), Compare2(a, b));
                Assert.AreEqual(ByteBlock.CompareConstantTime(new byte[] { c, a }, new byte[] { c, b }), Compare2(a, b));

                if (a != b)
                {
                    Assert.AreEqual(ByteBlock.CompareConstantTime(new byte[] { a, c }, new byte[] { b, c }), Compare2(a, b));
                    Assert.AreEqual(ByteBlock.CompareConstantTime(new byte[] { a, b }, new byte[] { b, a }), Compare2(a, b));
                    Assert.AreEqual(ByteBlock.CompareConstantTime(new byte[] { a, c }, new byte[] { b, d }), Compare2(a, b));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fail: " + ex.Message);
                throw;
            }

        }
#endif

        public int Compare2(byte byteA, byte byteB)
        {
            if (byteA == byteB)
                return 0;
            else if (byteA > byteB)
                return 1;
            else //if (byteB < byteA)
                return -1;
        }
    }
}
