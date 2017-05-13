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

namespace HelixSync.NUnit
{

    public class HelixUtilTests
    {
        [Fact]
        public void HelixUtil_RemoveRootFromPath()
        {
            Assert.Equal(@"test", HelixUtil.RemoveRootFromPath(Util.Path(@"c:\test"), @"c:"));
            Assert.Equal(@"test", HelixUtil.RemoveRootFromPath(Util.Path(@"c:\test"), Util.Path(@"c:\")));
            Assert.Equal(Util.Path(@"aa\bb"), HelixUtil.RemoveRootFromPath(Util.Path(@"aa\bb"), @""));
            Assert.Equal(@"bb", HelixUtil.RemoveRootFromPath(Util.Path(@"aa\bb"), @"aa"));
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
