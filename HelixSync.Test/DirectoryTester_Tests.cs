// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync.Test
{
    [TestClass]
    public class DirectoryTester_Tests
    {
        [TestMethod]
        public void DirectoryTester_Test()
        {
            using (DirectoryTester tester = new DirectoryTester("test1"))
            {
                tester.UpdateTo(new string[] 
                {
                    "bb/file1.txt:0 < aa",
                    "aa/file2.txt:2 < bb",
                    "xxx/:0 "
                });

                tester.AssertEqual(new string[] 
                {
                    "bb/file1.txt:0 < aa",
                    "aa/file2.txt:2 < bb",
                    "xxx/:0 "
                });
            }
        }

        [TestMethod]
        public void DirectoryTester_CollectionRemovedDuplicateEntries()
        {
            var directoryEntry = new DirectoryTester.DirectoryEntryCollection("a", "b", "a:2");
            Assert.AreEqual(2, directoryEntry.Count());
            Assert.IsTrue(directoryEntry.Any(e => e.FileName == "a" && e.Time == 2));
        }

        [TestMethod]
        public void DirectoryEntryParsing()
        {
            {
                var de = new DirectoryTester.DirectoryEntry("aa.txt:* < *");
                Assert.IsNull(de.Time);
                Assert.IsNull(de.Content);
            }

            {
                var de = new DirectoryTester.DirectoryEntry("aa.txt");
                Assert.IsNull(de.Time);
                Assert.IsNull(de.Content);
            }

            {
                var de = new DirectoryTester.DirectoryEntry("aa.txt:5 < ");
                Assert.AreEqual(5, de.Time);
                Assert.AreEqual("", de.Content);
            }
        }

        [TestMethod]
        public void WildcardMatching()
        {
            using (DirectoryTester tester = new DirectoryTester("test1"))
            {
                tester.UpdateTo(new string[]
                {
                    "file1.txt:5 < aa",
                });

                tester.AssertEqual(new string[]
                {
                    "file1.txt:* < aa"
                });
                tester.AssertEqual(new string[]
                {
                    "file1.txt:5 < *"
                });

                Assert.ThrowsException<DirectoryTester.DirectoryMismatchException>(
                    () => tester.AssertEqual(new string[] { "file2.txt" }));
                Assert.ThrowsException<DirectoryTester.DirectoryMismatchException>(
                    () => tester.AssertEqual(new string[] { "file1.txt:4 < aa" }));
                Assert.ThrowsException<DirectoryTester.DirectoryMismatchException>(
                    () => tester.AssertEqual(new string[] { "file1.txt:5 < qq" }));
            }
        }
    }
}
