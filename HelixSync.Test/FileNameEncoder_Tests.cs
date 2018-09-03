// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using HelixSync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelixSync.Test
{
    [TestClass]
    public class FileNameEncoder_Tests
    {
        [TestMethod]
        public void FileNameEncoder_EncodeName()
        {
            var encoder1 = new FileNameEncoder(new byte[0]);
            var encodedName1 = encoder1.EncodeName("hello world.txt");

            var encoder2 = new FileNameEncoder(new byte[0]);
            var encodedName2 = encoder2.EncodeName("hello world.txt");

            Assert.AreEqual(encodedName1, encodedName2);
        }
    }
}
