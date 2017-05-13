// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using HelixSync;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Xunit;

namespace HelixSync.Test
{
    public class DirectoryPair_Tests : IDisposable
    {
        //FAT > 2000 (to accomidate time stamp presision
        //NFTS > 10 
        const int timeStampPrecision = 2000;

        public DirectoryPair_Tests(){
            ResetDirectory();
        }

        public void Dispose(){
            ResetDirectory();
        }

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

        [Fact]
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
            using (var origToEncr = DirectoryPair.Open("Encr", "Orig", DerivedBytesProvider.FromPassword("password"), true, HelixFileVersion.UnitTest))
            using (var encrToDecr = DirectoryPair.Open("Encr", "Decr", DerivedBytesProvider.FromPassword("password"), true, HelixFileVersion.UnitTest))
            {
                var changes = origToEncr.FindChanges();
                Assert.Equal(1, changes.Count);
                Assert.Equal("test.txt", changes[0].DecrFileName);

                Assert.Equal(SyncStatus.Success, origToEncr.TrySync(changes[0]));

                Assert.True(0 == origToEncr.FindChanges().Count, "Single file sync still contains changes");

                //New (Encr => Decr)
                changes = encrToDecr.FindChanges();
                Assert.Equal(1, changes.Count);
                Assert.True(changes[0].SyncMode == PreSyncMode.EncryptedSide);
                Assert.True(changes[0].EncrFileName.EndsWith(".hx"));

                Assert.Equal(SyncStatus.Success, encrToDecr.TrySync(changes[0]));

                Assert.Equal(0, encrToDecr.FindChanges().Count);

                Assert.Equal(HelixUtil.TruncateTicks(File.GetLastWriteTimeUtc(Util.Path("Orig/test.txt"))), HelixUtil.TruncateTicks(File.GetLastWriteTimeUtc(Util.Path("Decr/test.txt"))));
                Assert.Equal("hello world", File.ReadAllText(Path.Combine("Decr", "test.txt")));


                //Add (Orig => Encr)
                Util.WriteTextFile("Orig/test2.txt", "aa");
                changes = origToEncr.FindChanges();
                Assert.Equal(1, changes.Count);
                Assert.True(changes[0].SyncMode == PreSyncMode.DecryptedSide);
                Assert.Equal("test2.txt", changes[0].DecrFileName);

                Assert.Equal(SyncStatus.Success, origToEncr.TrySync(changes[0]));

                Assert.Equal(0, origToEncr.FindChanges().Count);

                //Add (Encr => Decr)
                changes = encrToDecr.FindChanges();
                Assert.Equal(1, changes.Count);
                Assert.True(changes[0].SyncMode == PreSyncMode.EncryptedSide);
                Assert.True(changes[0].EncrFileName.EndsWith(".hx"));

                Assert.Equal(SyncStatus.Success, encrToDecr.TrySync(changes[0]));

                Assert.Equal(0, encrToDecr.FindChanges().Count);

                Assert.Equal(HelixUtil.TruncateTicks(File.GetLastWriteTimeUtc(Util.Path("Orig/test2.txt"))),
                                HelixUtil.TruncateTicks(File.GetLastWriteTimeUtc(Util.Path(@"Decr/test2.txt"))));
                Assert.Equal("aa", File.ReadAllText(Util.Path("Decr/test2.txt")));



                //System.Threading.Thread.Sleep(timeStampPrecision); //ensure the timestap changes

                //Update (Orig => Encr)

                Util.WriteTextFile("Orig/test.txt", "hello world2");
                changes = origToEncr.FindChanges();
                Assert.Equal(1, changes.Count);
                Assert.True(changes[0].SyncMode == PreSyncMode.DecryptedSide);
                Assert.Equal("test.txt", changes[0].DecrFileName);

                Assert.Equal(SyncStatus.Success, origToEncr.TrySync(changes[0]));

                Assert.Equal(0, origToEncr.FindChanges().Count);

                //Update (Encr => Decr)
                changes = encrToDecr.FindChanges();
                Assert.Equal(1, changes.Count);
                Assert.True(changes[0].SyncMode == PreSyncMode.EncryptedSide);
                Assert.True(changes[0].EncrFileName.EndsWith(".hx"));

                Assert.Equal(SyncStatus.Success, encrToDecr.TrySync(changes[0]));

                Assert.Equal(0, encrToDecr.FindChanges().Count);
                Assert.Equal(
                    HelixUtil.TruncateTicks(File.GetLastWriteTimeUtc(Util.Path("Orig/test.txt"))),
                    HelixUtil.TruncateTicks(File.GetLastWriteTimeUtc(Util.Path("Decr/test.txt"))));



                Assert.Equal("hello world2", File.ReadAllText(Util.Path("Decr/test.txt")));

                //System.Threading.Thread.Sleep(timeStampPrecision); //ensure the timestap changes

                //Delete (Orig => Encr)
                File.Delete(Util.Path("Orig/test.txt"));
                changes = origToEncr.FindChanges();
                Assert.Equal(1, changes.Count);
                Assert.True(changes[0].SyncMode == PreSyncMode.DecryptedSide);
                Assert.Equal("test.txt", changes[0].DecrFileName);

                Assert.Equal(SyncStatus.Success, origToEncr.TrySync(changes[0]));
                Assert.True(origToEncr.DecrDirectory.SyncLog.FindByDecrFileName("test.txt").EntryType == FileEntryType.Removed);
                Assert.Equal(0, origToEncr.FindChanges().Count);


                //Delete (Encr => Decr)
                changes = encrToDecr.FindChanges();
                Assert.True(1 == changes.Count, "Delete change did not propigate correctly");
                Assert.True(changes[0].SyncMode == PreSyncMode.EncryptedSide);
                Assert.True(changes[0].EncrFileName.EndsWith(".hx"));

                Assert.Equal(SyncStatus.Success, encrToDecr.TrySync(changes[0]));

                Assert.False(File.Exists(Util.Path("Decr/test.txt")), "Missing file Decr/test.txt");
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

        [Fact]
        public void DirectoryPair_RenameCaseOnly()
        {
            string password = "password";
            ResetDirectory();
            Directory.CreateDirectory("1-Orig");
            File.WriteAllText(Util.Path("1-Orig/file.txt"), "hello");
            using (var origToEncr = DirectoryPair.Open("2-Encr", "1-Orig", DerivedBytesProvider.FromPassword(password), true, HelixFileVersion.UnitTest))
            using (var encrToDecr = DirectoryPair.Open("2-Encr", "3-Decr", DerivedBytesProvider.FromPassword(password), true, HelixFileVersion.UnitTest))
            {
                //Orig => Encr
                origToEncr.SyncChanges();
                Assert.Equal(0, origToEncr.FindChanges().Count);


                //Encr => Decr
                encrToDecr.SyncChanges();
                Assert.Equal(0, origToEncr.FindChanges().Count);
            }

            System.Threading.Thread.Sleep(timeStampPrecision);

            File.Move(Util.Path("1-Orig/file.txt"), Util.Path("1-Orig/FILE1.txt"));
            File.Move(Util.Path("1-Orig/FILE1.txt"), Util.Path("1-Orig/FILE.txt"));

            using (var origToEncr = DirectoryPair.Open("2-Encr", "1-Orig", DerivedBytesProvider.FromPassword(password), true, HelixFileVersion.UnitTest))
            using (var encrToDecr = DirectoryPair.Open("2-Encr", "3-Decr", DerivedBytesProvider.FromPassword(password), true, HelixFileVersion.UnitTest))
            {
                //Orig => Encr
                Assert.Equal(2, origToEncr.FindChanges().Count); //delete + create
                origToEncr.SyncChanges();
                Assert.Equal(0, origToEncr.FindChanges().Count);

                //Encr => Decr
                Assert.Equal(2, encrToDecr.FindChanges().Count); //delete + create
                Assert.True(encrToDecr.FindChanges().All(e => e.SyncMode == PreSyncMode.EncryptedSide));
                encrToDecr.SyncChanges();
                Assert.Equal(0, origToEncr.FindChanges().Count);

                Assert.Equal("FILE.txt", (new DirectoryInfo(@"1-Orig")).GetFileSystemInfos("*.txt").First().Name);
            }
        }


        [Fact]
        public void DirectoryPair_NestedFolders()
        {
            ResetDirectory();
            string password = "password";
            using (var origToEncr = DirectoryPair.Open("2-Encr", "1-Orig", DerivedBytesProvider.FromPassword(password), true, HelixFileVersion.UnitTest))
            using (var encrToDecr = DirectoryPair.Open("2-Encr", "3-Decr", DerivedBytesProvider.FromPassword(password), true, HelixFileVersion.UnitTest))
            {
                //Creates nested folders
                Directory.CreateDirectory(Util.Path("1-Orig/Nested/Directory"));
                File.WriteAllText(Util.Path("1-Orig/Nested/File.txt"), "nested content");

                //Orig => Encr
                origToEncr.SyncChanges();
                Assert.Equal(0, origToEncr.FindChanges().Count);


                //Encr => Decr
                encrToDecr.SyncChanges();
                Assert.Equal(0, origToEncr.FindChanges().Count);


                //Verifies nested folders
                Assert.True(Directory.Exists(Util.Path("3-Decr/Nested/Directory")), "Directory Exists: 3-Decr/Nested/Directory");
                Assert.True(File.Exists(Util.Path("3-Decr/Nested/File.txt")), "File Exists: 3-Decr/Nested/File.txt");
            }
            ResetDirectory();
        }

        //todo: test 2 encr to 1 decr
        //todo: test renaming a directory (with children) only by case
        //todo: test replacing a directory with file and vica-versa
    }
}
