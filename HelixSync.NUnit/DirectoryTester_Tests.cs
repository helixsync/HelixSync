// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync.NUnit
{
    [TestFixture]
    public class DirectoryTester_Tests
    {
        [Test]
        public void DirectoryTester_Test()
        {
            using (DirectoryTester tester = new DirectoryTester("test1"))
            {
                tester.UpdateTo(
                    "bb/file1.txt:0 < aa",
                    "aa/file2.txt:2 < bb",
                    "xxx/:0 ");
                
                Assert.IsTrue(tester.EqualTo(
                    "bb/file1.txt:0 < aa",
                    "aa/file2.txt:2 < bb",
                    "xxx/:0 "));
            }
        }

        [Test]
        public void DirectoryTester_CollectionRemovedDuplicateEntries()
        {
            var directoryEntry = new DirectoryTester.DirectoryEntryCollection("a", "b", "a:2");
            Assert.AreEqual(2, directoryEntry.Count());
            Assert.IsTrue(directoryEntry.Any(e => e.FileName == "a" && e.Time == 2));
        }
    }
}
