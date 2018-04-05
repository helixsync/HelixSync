// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using HelixSync;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HelixSync.Test
{

    public class HelixUtil_Tests
    {
        [Fact]
        public void HelixUtil_RemoveRootFromPath()
        {
            Assert.Equal(@"test", HelixUtil.RemoveRootFromPath(Util.Path(@"c:\test"), @"c:"));
            Assert.Equal(@"test", HelixUtil.RemoveRootFromPath(Util.Path(@"c:\test"), Util.Path(@"c:\")));
            Assert.Equal(Util.Path(@"aa\bb"), HelixUtil.RemoveRootFromPath(Util.Path(@"aa\bb"), @""));
            Assert.Equal(@"bb", HelixUtil.RemoveRootFromPath(Util.Path(@"aa\bb"), @"aa"));
        }

        [Fact]
        public void HelixUtil_GetExactPathName()
        {
            
            try
            {
                Util.Remove("AA");
                Directory.CreateDirectory("AA");
                Directory.CreateDirectory(@"AA\A1");
                File.WriteAllText(@"AA\A1\A2", "xx");
                File.WriteAllText(@"AA\AA", "aa");
                File.WriteAllText(@"AA\ABCDEFGHIJKLMNOP", "Long File Name");

                if (File.Exists(@"aa\ABCDEF~1")) //only tests on certain file systems with DOS 8.3 name support
                {
                    var exactPath = HelixUtil.GetExactPathName(@"aa\ABCDEF~1");
                    Assert.Equal(@"AA\ABCDEFGHIJKLMNOP", exactPath);
                }

                {
                    var exactPath = HelixUtil.GetExactPathName(@"aa\a1\a2");
                    Assert.Equal(@"AA\A1\A2", exactPath);
                }
                {
                    var exactPath = HelixUtil.GetExactPathName(@"aa\aa");
                    Assert.Equal(@"AA\AA", exactPath);
                }

            }
            finally
            {
                Util.Remove("AA");
            }

        }


#if FSCHECK
        [FsCheck.NUnit.Property]
        public void HelixUtil_QuoteUnquote(string val)
        {
            if (val == null)
                return;

            Assert.AreEqual(val, HelixUtil.Unquote(HelixUtil.Quote(val)));
        }
#endif
    }
}
