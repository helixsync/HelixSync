// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HelixSync.Test
{
    public class DirectoryTester_Tests
    {
        [Fact]
        public void DirectoryTester_Test()
        {
            using (DirectoryTester tester = new DirectoryTester("test1"))
            {
                tester.UpdateTo(
                    "bb/file1.txt:0 < aa",
                    "aa/file2.txt:2 < bb",
                    "xxx/:0 ");
                
                Assert.True(tester.EqualTo(
                    "bb/file1.txt:0 < aa",
                    "aa/file2.txt:2 < bb",
                    "xxx/:0 "));
            }
        }

        [Fact]
        public void DirectoryTester_CollectionRemovedDuplicateEntries()
        {
            var directoryEntry = new DirectoryTester.DirectoryEntryCollection("a", "b", "a:2");
            Assert.Equal(2, directoryEntry.Count());
            Assert.True(directoryEntry.Any(e => e.FileName == "a" && e.Time == 2));
        }
    }
}
