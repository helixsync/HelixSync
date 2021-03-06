﻿// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using HelixSync;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

namespace HelixSync.Test
{
    [TestClass]
    public class DirectoryPair_Tests : IntegratedDirectoryTester
    {
        public DirectoryPair_Tests()
        {
            ResetDirectory();
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ResetDirectory();

            base.Dispose(disposing);
        }


        //FAT > 2000 (to accomidate time stamp presision
        //NFTS > 10 
        const int timeStampPrecision = 2000;
        

        public void ResetDirectory()
        {
            System.IO.Directory.SetCurrentDirectory(Path.GetDirectoryName(this.GetType().GetTypeInfo().Assembly.Location));
            if (Directory.Exists("1-Orig"))
                Directory.Delete("1-Orig", true);
            if (Directory.Exists("2-Encr"))
                Directory.Delete("2-Encr", true);
            if (Directory.Exists("3-Decr"))
                Directory.Delete("3-Decr", true);

            if (Directory.Exists("Orig")) Directory.Delete("Orig", true);
            if (Directory.Exists("Decr")) Directory.Delete("Decr", true);
            if (Directory.Exists("Encr")) Directory.Delete("Encr", true);
        }

        [TestMethod]
        public void DirectoryPair_DecrToEncr()
        {
            if (Directory.Exists("Orig")) Directory.Delete("Orig", true);
            if (Directory.Exists("Decr")) Directory.Delete("Decr", true);
            if (Directory.Exists("Encr")) Directory.Delete("Encr", true);

            Directory.CreateDirectory("Orig");
            Directory.CreateDirectory("Encr");
            Directory.CreateDirectory("Decr");

            //New (Orig => Encr)
            Util.WriteTextFile("Orig/test.txt", "hello world");
            using (var origToEncr = DirectoryPair.Open("Orig", "Encr", DerivedBytesProvider.FromPassword("password"), true, HelixFileVersion.UnitTest))
            using (var encrToDecr = DirectoryPair.Open("Decr", "Encr", DerivedBytesProvider.FromPassword("password"), true, HelixFileVersion.UnitTest))
            {
                var changes = origToEncr.FindChanges(clearCache: false);
                Assert.AreEqual(1, changes.Count);
                Assert.AreEqual("test.txt", changes[0].DecrFileName);

                Assert.AreEqual(SyncStatus.Success, origToEncr.TrySync(changes[0]).SyncStatus);

                changes = origToEncr.FindChanges(clearCache: true);
                Assert.IsTrue(0 == changes.Count, "Single file sync still contains changes");

                //New (Encr => Decr)
                changes = encrToDecr.FindChanges(clearCache: true);
                Assert.AreEqual(1, changes.Count);
                Assert.IsTrue(changes[0].SyncMode == PreSyncMode.EncryptedSide);
                Assert.IsTrue(changes[0].EncrFileName.EndsWith(".hx"));

                Assert.AreEqual(SyncStatus.Success, encrToDecr.TrySync(changes[0]).SyncStatus);

                Assert.AreEqual(0, encrToDecr.FindChanges(clearCache: true).Count);

                Assert.AreEqual(HelixUtil.TruncateTicks(File.GetLastWriteTimeUtc(Util.Path("Orig/test.txt"))), HelixUtil.TruncateTicks(File.GetLastWriteTimeUtc(Util.Path("Decr/test.txt"))));
                Assert.AreEqual("hello world", File.ReadAllText(Path.Combine("Decr", "test.txt")));


                //Add (Orig => Encr)
                origToEncr.ClearCache();
                Util.WriteTextFile("Orig/test2.txt", "aa");
                changes = origToEncr.FindChanges(clearCache: true);
                Assert.AreEqual(1, changes.Count);
                Assert.IsTrue(changes[0].SyncMode == PreSyncMode.DecryptedSide);
                Assert.AreEqual("test2.txt", changes[0].DecrFileName);

                Assert.AreEqual(SyncStatus.Success, origToEncr.TrySync(changes[0]).SyncStatus);

                Assert.AreEqual(0, origToEncr.FindChanges(clearCache: true).Count);

                //Add (Encr => Decr)
                encrToDecr.ClearCache();
                changes = encrToDecr.FindChanges(clearCache: true);
                Assert.AreEqual(1, changes.Count);
                Assert.IsTrue(changes[0].SyncMode == PreSyncMode.EncryptedSide);
                Assert.IsTrue(changes[0].EncrFileName.EndsWith(".hx"));

                Assert.AreEqual(SyncStatus.Success, encrToDecr.TrySync(changes[0]).SyncStatus);

                Assert.AreEqual(0, encrToDecr.FindChanges().Count);

                Assert.AreEqual(HelixUtil.TruncateTicks(File.GetLastWriteTimeUtc(Util.Path("Orig/test2.txt"))),
                                HelixUtil.TruncateTicks(File.GetLastWriteTimeUtc(Util.Path(@"Decr/test2.txt"))));
                Assert.AreEqual("aa", File.ReadAllText(Util.Path("Decr/test2.txt")));



                //System.Threading.Thread.Sleep(timeStampPrecision); //ensure the timestap changes

                //Update (Orig => Encr)
                origToEncr.ClearCache();
                Util.WriteTextFile("Orig/test.txt", "hello world2");
                changes = origToEncr.FindChanges();
                Assert.AreEqual(1, changes.Count);
                Assert.IsTrue(changes[0].SyncMode == PreSyncMode.DecryptedSide);
                Assert.AreEqual("test.txt", changes[0].DecrFileName);

                Assert.AreEqual(SyncStatus.Success, origToEncr.TrySync(changes[0]).SyncStatus);

                Assert.AreEqual(0, origToEncr.FindChanges().Count);

                //Update (Encr => Decr)
                encrToDecr.ClearCache();
                changes = encrToDecr.FindChanges();
                Assert.AreEqual(1, changes.Count);
                Assert.IsTrue(changes[0].SyncMode == PreSyncMode.EncryptedSide);
                Assert.IsTrue(changes[0].EncrFileName.EndsWith(".hx"));

                Assert.AreEqual(SyncStatus.Success, encrToDecr.TrySync(changes[0]).SyncStatus);

                Assert.AreEqual(0, encrToDecr.FindChanges().Count);
                Assert.AreEqual(
                    HelixUtil.TruncateTicks(File.GetLastWriteTimeUtc(Util.Path("Orig/test.txt"))),
                    HelixUtil.TruncateTicks(File.GetLastWriteTimeUtc(Util.Path("Decr/test.txt"))));



                Assert.AreEqual("hello world2", File.ReadAllText(Util.Path("Decr/test.txt")));

                //Delete (Orig => Encr)
                origToEncr.ClearCache();
                File.Delete(Util.Path("Orig/test.txt"));
                changes = origToEncr.FindChanges();
                Assert.AreEqual(1, changes.Count);
                Assert.IsTrue(changes[0].SyncMode == PreSyncMode.DecryptedSide);
                Assert.AreEqual("test.txt", changes[0].DecrFileName);

                Assert.AreEqual(SyncStatus.Success, origToEncr.TrySync(changes[0]).SyncStatus);
                Assert.IsTrue(origToEncr.SyncLog.FindByDecrFileName("test.txt").EntryType == FileEntryType.Removed);
                Assert.AreEqual(0, origToEncr.FindChanges().Count);


                //Delete (Encr => Decr)
                encrToDecr.ClearCache();
                changes = encrToDecr.FindChanges();
                Assert.IsTrue(1 == changes.Count, "Delete change did not propigate correctly");
                Assert.IsTrue(changes[0].SyncMode == PreSyncMode.EncryptedSide);
                Assert.IsTrue(changes[0].EncrFileName.EndsWith(".hx"));

                Assert.AreEqual(SyncStatus.Success, encrToDecr.TrySync(changes[0]).SyncStatus);

                Assert.IsFalse(File.Exists(Util.Path("Decr/test.txt")), "Missing file Decr/test.txt");
            }
        }

#if FSCHECK
        [Test]
        public void DirectoryPair_RandomizedInitialization()
        {
            RandomValue.NewTest(1836678571);

            for (int i = 0; i < 10; i++)
            {
                var dirStruct = RandomValue.GetDirectoryStructure(10)
                                           .Where(d => HelixUtil.IsValidPath(d))
                                           .ToArray();

                ResetDirectory();

                var setupItems = RandomValue.ChooseMany(dirStruct);
                foreach (var item in setupItems)
                {
                    if (item.EndsWith(Path.DirectorySeparatorChar.ToString()))
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine("1-Orig", item + "a")));
                    else
                    {
                        var fileName = Path.Combine("1-Orig", item);
                        //Creates parent directory
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine("1-Orig", item + "a")));
                        File.WriteAllText(fileName, RandomValue.GetValue<string>());
                    }
                }

                string password = RandomValue.GetValue<string>() ?? "";
                using (var origToEncr = DirectoryPair.Open("2-Encr", "1-Orig", DerivedBytesProvider.FromPassword(password), true, HelixFileVersion.UnitTest))
                using (var encrToDecr = DirectoryPair.Open("2-Encr", "3-Decr", DerivedBytesProvider.FromPassword(password), true, HelixFileVersion.UnitTest))
                {
                    //Orig => Encr
                    origToEncr.SyncChanges();
                    Assert.AreEqual(0, origToEncr.FindChanges().Count);


                    //Encr => Decr
                    encrToDecr.SyncChanges();
                    Assert.AreEqual(0, origToEncr.FindChanges().Count);
                }
            }
        }
#endif

        [TestMethod]
        public void DirectoryPair_RenameCaseOnly()
        {
            string password = "password";
            ResetDirectory();
            Directory.CreateDirectory("1-Orig");
            File.WriteAllText(Util.Path("1-Orig/file.txt"), "hello");
            using (var origToEncr = DirectoryPair.Open("1-Orig", "2-Encr", DerivedBytesProvider.FromPassword(password), true, HelixFileVersion.UnitTest))
            using (var encrToDecr = DirectoryPair.Open("3-Decr", "2-Encr", DerivedBytesProvider.FromPassword(password), true, HelixFileVersion.UnitTest))
            {
                //Orig => Encr
                origToEncr.SyncChanges();
                Assert.AreEqual(0, origToEncr.FindChanges().Count);


                //Encr => Decr
                encrToDecr.SyncChanges();
                Assert.AreEqual(0, origToEncr.FindChanges().Count);
            }

            System.Threading.Thread.Sleep(timeStampPrecision);

            File.Move(Util.Path("1-Orig/file.txt"), Util.Path("1-Orig/FILE1.txt"));
            File.Move(Util.Path("1-Orig/FILE1.txt"), Util.Path("1-Orig/FILE.txt"));

            using (var origToEncr = DirectoryPair.Open("1-Orig", "2-Encr", DerivedBytesProvider.FromPassword(password), true, HelixFileVersion.UnitTest))
            using (var encrToDecr = DirectoryPair.Open("3-Decr", "2-Encr", DerivedBytesProvider.FromPassword(password), true, HelixFileVersion.UnitTest))
            {
                //Orig => Encr
                Assert.AreEqual(2, origToEncr.FindChanges().Count); //delete + create
                origToEncr.SyncChanges();
                Assert.AreEqual(0, origToEncr.FindChanges().Count);

                //Encr => Decr
                Assert.AreEqual(2, encrToDecr.FindChanges().Count); //delete + create
                Assert.IsTrue(encrToDecr.FindChanges().All(e => e.SyncMode == PreSyncMode.EncryptedSide));
                encrToDecr.SyncChanges();
                Assert.AreEqual(0, origToEncr.FindChanges().Count);

                Assert.AreEqual("FILE.txt", (new DirectoryInfo(@"1-Orig")).GetFileSystemInfos("*.txt").First().Name);
            }

            //Assert.IsTrue(false, "Sometimes fails, sometimes works depending on the order (false until properly fixed)");
        }


        [TestMethod]
        public void DirectoryPair_NestedFolders()
        {
            ResetDirectory();
            string password = "password";
            using (var origToEncr = DirectoryPair.Open("1-Orig", "2-Encr", DerivedBytesProvider.FromPassword(password), true, HelixFileVersion.UnitTest))
            using (var encrToDecr = DirectoryPair.Open("3-Decr", "2-Encr", DerivedBytesProvider.FromPassword(password), true, HelixFileVersion.UnitTest))
            {
                //Creates nested folders
                Directory.CreateDirectory(Util.Path("1-Orig/Nested/Directory"));
                File.WriteAllText(Util.Path("1-Orig/Nested/File.txt"), "nested content");

                //Orig => Encr
                origToEncr.SyncChanges();
                Assert.AreEqual(0, origToEncr.FindChanges().Count);


                //Encr => Decr
                encrToDecr.SyncChanges();
                Assert.AreEqual(0, origToEncr.FindChanges().Count);


                //Verifies nested folders
                Assert.IsTrue(Directory.Exists(Util.Path("3-Decr/Nested/Directory")), "Directory Exists: 3-Decr/Nested/Directory");
                Assert.IsTrue(File.Exists(Util.Path("3-Decr/Nested/File.txt")), "File Exists: 3-Decr/Nested/File.txt");
            }
            ResetDirectory();
        }

        [TestMethod]
        public void FindChanges_ReturtsInRandomOrder()
        {
            Decr1.UpdateTo("file1.txt < aa", "file2.txt", "file3.txt");

            HashSet<int> indexes = new HashSet<int>();

            using (var pair = DirectoryPair.Open(Decr1.DirectoryPath, Encr1.DirectoryPath, DerivedBytesProvider.FromPassword("password"), true, HelixFileVersion.UnitTest))
            {
                for (int i = 0; i < 100; i++)
                {
                    var changes = pair.FindChanges();
                    var index = changes.FindIndex(c => c.DecrFileName == "file1.txt");
                    indexes.Add(index);
                }

                Assert.IsTrue(indexes.Count == 3, "file1.txt not found in all random spots");
            }
        }

        [TestMethod]
        public void FindChanges_AddsInOrderParentToChildren()
        {
            Decr1.UpdateTo("file1.txt < aa", "zz/file2.txt");

            using (var pair = DirectoryPair.Open(Decr1.DirectoryPath, Encr1.DirectoryPath, DerivedBytesProvider.FromPassword("password"), true, HelixFileVersion.UnitTest))
            {
                for (int i = 0; i < 100; i++)
                {
                    var changes = pair.FindChanges();
                    var parentIndex = changes.FindIndex(c => c.DecrFileName == "zz");
                    var childIndex = changes.FindIndex(c => HelixUtil.PathNative(c.DecrFileName) == HelixUtil.PathNative("zz/file2.txt"));
                    Assert.IsTrue(parentIndex < childIndex, "Parent Directory Add did not come before Child File Add");
                }
            }
        }


        [TestMethod]
        public void FindChanges_CaseChangeDeletesBeforeAdds()
        {
            Decr1.UpdateTo("file1.txt");

            using (var pair = DirectoryPair.Open(Decr1.DirectoryPath, Encr1.DirectoryPath, DerivedBytesProvider.FromPassword("password"), true, HelixFileVersion.UnitTest))
            {
                pair.SyncChanges();


                Decr1.UpdateTo("FILE1.txt");


                for (int i = 0; i < 100; i++)
                {
                    var changes = pair.FindChanges();
                    var deleteIndex = changes.FindIndex(c => c.DecrFileName == "file1.txt");
                    var addIndex = changes.FindIndex(c => c.DecrFileName == "FILE1.txt");

                    Assert.IsTrue(deleteIndex < addIndex, "Case Rename did not order delete before add");
                }
            }
        }


        [TestMethod]
        public void FindChanges_RemovesInOrderParentToChildren()
        {
            Decr1.UpdateTo("file1.txt < aa", "zz/file2.txt");
            
            using (var pair = DirectoryPair.Open(Decr1.DirectoryPath, Encr1.DirectoryPath, DerivedBytesProvider.FromPassword("password"), true, HelixFileVersion.UnitTest))
            {
                pair.SyncChanges();


                Decr1.UpdateTo("");


                for (int i = 0; i < 100; i++)
                {
                    var changes = pair.FindChanges();
                    var parentIndex = changes.FindIndex(c => c.DecrFileName == "zz");
                    var childIndex = changes.FindIndex(c => HelixUtil.PathNative(c.DecrFileName) == HelixUtil.PathNative("zz/file2.txt"));
                    Assert.IsTrue(parentIndex > childIndex, "Parent Directory Add did not come before Child File Add");
                }
            }
        }


        //todo: test 2 encr to 1 decr
        //todo: test renaming a directory (with children) only by case
        //todo: test replacing a directory with file and vica-versa
    }
}
