// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using HelixSync;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelixSync.Test
{
    [TestClass]
    public class HelixUtil_Tests
    {
        [TestMethod]
        public void HelixUtil_RemoveRootFromPath()
        {
            Assert.AreEqual(@"test", HelixUtil.RemoveRootFromPath(Util.Path(@"c:\test"), @"c:"));
            Assert.AreEqual(@"test", HelixUtil.RemoveRootFromPath(Util.Path(@"c:\test"), Util.Path(@"c:\")));
            Assert.AreEqual(Util.Path(@"aa\bb"), HelixUtil.RemoveRootFromPath(Util.Path(@"aa\bb"), @""));
            Assert.AreEqual(@"bb", HelixUtil.RemoveRootFromPath(Util.Path(@"aa\bb"), @"aa"));
        }

        [TestMethod]
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
                    Assert.AreEqual(@"AA\ABCDEFGHIJKLMNOP", exactPath);
                }

                {
                    var exactPath = HelixUtil.GetExactPathName(@"aa\a1\a2");
                    Assert.AreEqual(@"AA\A1\A2", exactPath);
                }
                {
                    var exactPath = HelixUtil.GetExactPathName(@"aa\aa");
                    Assert.AreEqual(@"AA\AA", exactPath);
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
