// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using Xunit;

namespace HelixSync.NUnit
{
    public class FileEntry_Test
    {
        [Fact]
        public void FileEntryTest_ConvertsSlashes()
        {
            FileEntry a = FileEntry.FromFile("a\\b", null);
            Assert.Equal("a" + HelixUtil.UniversalDirectorySeparatorChar + "b", a.FileName);

            FileEntry b = FileEntry.FromFile("a/b", null);
            Assert.Equal("a" + HelixUtil.UniversalDirectorySeparatorChar + "b", b.FileName);
        }
    }
}
