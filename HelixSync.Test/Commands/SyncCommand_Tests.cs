// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections;
using HelixSync;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelixSync.Test
{
    [TestClass]
    public class SyncCommand_Tests : IntegratedDirectoryTester
    {

        public SyncCommand_Tests()
        {

        }

        public (int ExitCode, List<PreSyncDetails> Changes) SyncDecr1andEncr1(Action<PreSyncDetails> onPreSyncDetails = null)
        {
            List<PreSyncDetails> changes = new List<PreSyncDetails>();
            SyncOptions options = new SyncOptions
            {
                DecrDirectory = Decr1.DirectoryPath,
                EncrDirectory = Encr1.DirectoryPath,
                Password = "secret",
                Initialize = true
            };
            ConsoleEx console = new ConsoleEx
            {
                BeforeWriteLine = (o) =>
                {
                    if (o is PreSyncDetails preSync)
                    {
                        onPreSyncDetails?.Invoke(preSync);
                        changes.Add(preSync);
                    }
                    System.Diagnostics.Debug.WriteLine(o);
                }
            };
            var exitCode = SyncCommand.Sync(options, console, HelixFileVersion.UnitTest);
            return (exitCode, changes);
        }

        public (int ExitCode, List<PreSyncDetails> Changes) SyncDecr2andEncr1(Action<PreSyncDetails> onPreSyncDetails = null)
        {
            List<PreSyncDetails> changes = new List<PreSyncDetails>();

            SyncOptions options = new SyncOptions
            {
                DecrDirectory = Decr2.DirectoryPath,
                EncrDirectory = Encr1.DirectoryPath,
                Password = "secret",
                Initialize = true
            };
            ConsoleEx console = new ConsoleEx
            {
                BeforeWriteLine = (o) =>
                {
                    if (o is PreSyncDetails preSync)
                    {
                        onPreSyncDetails?.Invoke(preSync);
                        changes.Add(preSync);
                    }
                    System.Diagnostics.Debug.WriteLine(o);
                }
            };

            var exitCode = SyncCommand.Sync(options, console, HelixFileVersion.UnitTest);

            return (exitCode, changes);
        }

        public SyncLogEntry[] Decr1AndEncr1SyncLog()
        {
            using (var pair = new DirectoryPair(Decr1.DirectoryPath, Encr1.DirectoryPath, DerivedBytesProvider.FromPassword("secret"), true))
            {
                pair.OpenEncr(null);
                pair.OpenDecr(null);
                return pair.SyncLog.ToArray();
            }
        }

        [TestMethod]
        public void SyncCommand_SimpleSync()
        {
            Decr1.UpdateTo("file1.txt < aa");
            var sync1 = SyncDecr1andEncr1();
            Assert.AreEqual(1, sync1.Changes.Count);

            var sync2 = SyncDecr2andEncr1();
            Assert.AreEqual(1, sync1.Changes.Count);


            Decr2.AssertEqual(new string[] { "file1.txt < aa" });

            Decr1.UpdateTo("file1.txt < aa",
                           "file2.txt < bb");

            SyncDecr1andEncr1();
            SyncDecr2andEncr1();
            Decr2.AssertEqual(new string[] { "file1.txt < aa",
                           "file2.txt < bb" }); ;
        }

        [TestMethod]
        public void SyncCommand_MultipleSyncUnchanged()
        {
            Decr1.UpdateTo("file1.txt < aa");
            var sync1 = SyncDecr1andEncr1();
            var logLength = Decr1AndEncr1SyncLog().Length;
            Assert.AreEqual(1, sync1.Changes.Count);
            var sync2 = SyncDecr1andEncr1();
            Assert.AreEqual(0, sync2.Changes.Count);
            Assert.AreEqual(logLength, Decr1AndEncr1SyncLog().Length);
        }


        [TestMethod]
        public void SyncCommand_DeleteThenReAdd()
        {
            Decr1.UpdateTo("file1.txt < aa");
            SyncDecr1andEncr1();
            SyncDecr2andEncr1();

            Decr1.UpdateTo("");
            SyncDecr1andEncr1();
            SyncDecr2andEncr1();
            DirectoryAreEqual(Decr2, "", "deleted file propigated");

            Decr1.UpdateTo("file1.txt < abc");
            SyncDecr1andEncr1();
            SyncDecr2andEncr1();

            Decr2.AssertEqual("file1.txt < abc");
        }

        [TestMethod]
        public void SyncCommand_ChangeCaseOnly()
        {
            for (int i = 0; i < 10; i++)
            {
                Decr1.UpdateTo("A < xyz");
                SyncDecr1andEncr1(p => Assert.IsTrue(p.SyncMode == PreSyncMode.DecryptedSide));
                SyncDecr2andEncr1(p => Assert.IsTrue(p.SyncMode == PreSyncMode.EncryptedSide));
                Decr1.AssertEqual(new string[] { "A < xyz" });
                Decr2.AssertEqual(new string[] { "A < xyz" });

                Decr1.UpdateTo("a");
                SyncDecr1andEncr1(p => Assert.IsTrue(p.SyncMode == PreSyncMode.DecryptedSide));
                SyncDecr2andEncr1(p => Assert.IsTrue(p.SyncMode == PreSyncMode.EncryptedSide));
                Decr1.AssertEqual(new string[] { "a" });
                Decr2.AssertEqual(new string[] { "a" });
            }

            //Assert.IsTrue(false, "Sometimes works sometimes fails depending if th delete comes before the add");
        }

        static readonly string[] choices = new string[]
        {
            "a",
            "a:2",
            "A",
        };

#if FSCHECK
        [FsCheck.NUnit.Property]
        public void SyncCommand_OneWaySync_NoConflict_FuzzTesting(byte[] d1, byte[] d2)
        {
            
            Decr1.Clear(true);
            Decr2.Clear(true);

            string[] d1str = (d1 ?? (new byte[] { })).Select(d => choices[d % choices.Length]).ToArray();
            string[] d2str = (d2 ?? (new byte[] { })).Select(d => choices[d % choices.Length]).ToArray();

            Decr1.UpdateTo(d1str);
            SyncDecr1andEncr1(p => Assert.IsTrue(p.SyncMode == PreSyncMode.DecryptedSide));
            SyncDecr2andEncr1(p => Assert.IsTrue(p.SyncMode == PreSyncMode.EncryptedSide));
            Decr1.AssertEqual(d1str);
            Decr2.AssertEqual(d1str);

            Decr1.UpdateTo(d2str);
            SyncDecr1andEncr1(p => Assert.IsTrue(p.SyncMode == PreSyncMode.DecryptedSide));
            SyncDecr2andEncr1(p => Assert.IsTrue(p.SyncMode == PreSyncMode.EncryptedSide));
            Decr1.AssertEqual(d2str);
            Decr2.AssertEqual(d2str);
        }
#endif

#if FSCHECK
        [FsCheck.NUnit.Property]
        public void SyncCommand_TwoWaySync_NoConflict_FuzzTesting(byte[] d1, byte[] d2)
        {
            string[] d1str = (d1 ?? (new byte[] { })).Select(d => choices[d % choices.Length]).ToArray();
            string[] d2str = (d2 ?? (new byte[] { })).Select(d => choices[d % choices.Length]).ToArray();

            Decr1.Subdirectory("d1").UpdateTo(d1str);
            Decr2.Subdirectory("d2").UpdateTo(d2str);

            Decr1.Subdirectory("d1").AssertEqual(d1str);
            Decr2.Subdirectory("d2").AssertEqual(d2str);

            SyncDecr1andEncr1(p => Assert.IsTrue(p.SyncMode == PreSyncMode.DecryptedSide));
            Decr1.Subdirectory("d1").AssertEqual(d1str);
            SyncDecr1andEncr1(p => Assert.Fail("Unexpected change post sync"));

            SyncDecr2andEncr1();
            Decr2.Subdirectory("d1").AssertEqual(d1str);
            Decr2.Subdirectory("d2").AssertEqual(d2str);
            SyncDecr2andEncr1(p => Assert.Fail("Unexpected change post sync"));

            SyncDecr1andEncr1(p => Assert.IsTrue(p.SyncMode == PreSyncMode.EncryptedSide));
            Decr1.Subdirectory("d1").AssertEqual(d1str);
            Decr1.Subdirectory("d2").AssertEqual(d2str);

        }
#endif

        public void DirectoryAreEqual(DirectoryTester tester, string content, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                Assert.AreEqual(new DirectoryTester.DirectoryEntryCollection(content).ToString(), tester.GetContent().ToString());
            }
            else
            {
                Assert.IsTrue(new DirectoryTester.DirectoryEntryCollection(content).ToString() == tester.GetContent().ToString(),
                            message);
            }
        }

        [TestMethod]
        public void SyncCommand_PurgedFile()
        {
            Decr1.UpdateTo("file1.txt < aa");
            SyncDecr1andEncr1();

            {
                var syncLog = Decr1AndEncr1SyncLog();
                Assert.IsTrue(syncLog.Length == 1);
                Assert.IsTrue(syncLog[0].DecrFileName == "file1.txt");
                Assert.IsTrue(syncLog[0].EntryType == FileEntryType.File);
            }

            Decr1.UpdateTo("");
            SyncDecr1andEncr1();

            {
                var syncLog = Decr1AndEncr1SyncLog();
                Assert.IsTrue(syncLog.Length == 1);
                Assert.IsTrue(syncLog[0].DecrFileName == "file1.txt");
                Assert.IsTrue(syncLog[0].EntryType == FileEntryType.Removed);
            }

            //removes the encr file should trigger a purge
            Encr1.Clear(new Regex("helix.hx"));

            SyncDecr1andEncr1();

            {
                var syncLog = Decr1AndEncr1SyncLog();
                Assert.IsTrue(syncLog.Length == 0);
            }
        }


        [TestMethod]
        public void SyncCommand_DeleteFolderOn1andAddFileOn2()
        {
            Decr1.UpdateTo(@"aa\001.txt < 001");
            SyncDecr1andEncr1();
            SyncDecr2andEncr1();

            Decr1.UpdateTo(@"");
            SyncDecr1andEncr1();

            Decr2.UpdateTo(@"aa\001.txt < 001", @"aa\002.txt < 002");
            SyncDecr2andEncr1();
            Assert.Fail("should prompt for directory not empty conflict");
        }
    }
}
