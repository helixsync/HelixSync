// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using HelixSync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HelixSync.Test
{
    public class FileNameEncoder_Tests
    {
        [Fact]
        public void FileNameEncoder_EncodeName()
        {
            var encoder1 = new FileNameEncoder(new byte[0]);
            var encodedName1 = encoder1.EncodeName("hello world.txt");

            var encoder2 = new FileNameEncoder(new byte[0]);
            var encodedName2 = encoder2.EncodeName("hello world.txt");

            Assert.Equal(encodedName1, encodedName2);
        }
    }
}
