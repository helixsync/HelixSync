// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using HelixSync;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace HelixSync.NUnit
{
    [TestFixture]
    public class HelixUtilTests
    {
        [Test]
        public void HelixUtil_RemoveRootFromPath()
        {
            Assert.AreEqual(@"test", HelixUtil.RemoveRootFromPath(Util.Path(@"c:\test"), @"c:"));
            Assert.AreEqual(@"test", HelixUtil.RemoveRootFromPath(Util.Path(@"c:\test"), Util.Path(@"c:\")));
            Assert.AreEqual(Util.Path(@"aa\bb"), HelixUtil.RemoveRootFromPath(Util.Path(@"aa\bb"), @""));
            Assert.AreEqual(@"bb", HelixUtil.RemoveRootFromPath(Util.Path(@"aa\bb"), @"aa"));
        }

        [FsCheck.NUnit.Property]
        public void HelixUtil_QuoteUnquote(string val)
        {
            if (val == null)
                return;

            Assert.AreEqual(val, HelixUtil.Unquote(HelixUtil.Quote(val)));
        }
    }
}
