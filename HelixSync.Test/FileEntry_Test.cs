// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelixSync.Test
{
    [TestClass]
    public class FileEntry_Test
    {
        [TestMethod]
        public void FileEntryTest_ConvertsSlashes()
        {
            FileEntry a = FileEntry.FromFile("a\\b", null);
            Assert.AreEqual("a" + HelixUtil.UniversalDirectorySeparatorChar + "b", a.FileName);

            FileEntry b = FileEntry.FromFile("a/b", null);
            Assert.AreEqual("a" + HelixUtil.UniversalDirectorySeparatorChar + "b", b.FileName);
        }
    }
}
