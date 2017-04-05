// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using NUnit.Framework;
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

namespace HelixSync.NUnit
{
    [TestFixture]
    public class SyncCommandTest
    {
        public DirectoryTester Decr1 = new DirectoryTester("Decr1", new Regex(@"\.helix.*"));
        public DirectoryTester Decr2 = new DirectoryTester("Decr2", new Regex(@"\.helix.*"));
        public DirectoryTester Encr1 = new DirectoryTester("Encr1");
        public DirectoryTester Encr2 = new DirectoryTester("Encr2");

        [SetUp]
        public void SetUp()
        {
            Decr1.Clear(true);
            Decr2.Clear(true);
            Encr1.Clear(true);
            Encr2.Clear(true);
            Assert.IsFalse(Directory.Exists(Decr1.DirectoryPath));
            Assert.IsFalse(Directory.Exists(Decr2.DirectoryPath));
            Assert.IsFalse(Directory.Exists(Encr1.DirectoryPath));
            Assert.IsFalse(Directory.Exists(Encr2.DirectoryPath));
        }

        [TearDown]
        public void TearDown()
        {
            Decr1.Dispose();
            Decr2.Dispose();
            Encr1.Dispose();
            Encr2.Dispose();
        }

        public void SyncDecr1andEncr1(Action<PreSyncDetails> onPreSyncDetails = null)
        {
            SyncOptions options = new SyncOptions
            {
                DecrDirectory = "Decr1",
                EncrDirectory = "Encr1",
                Password = "secret",
                Initialize = true
            };
            ConsoleEx console = new ConsoleEx();
            console.BeforeWriteLine = (o) =>
            {
                PreSyncDetails preSync = o as PreSyncDetails;
                if (preSync != null)
                    onPreSyncDetails?.Invoke(preSync);
            };
            SyncCommand.Sync(options, console, HelixFileVersion.UnitTest);
        }

        public void SyncDecr2andEncr1(Action<PreSyncDetails> onPreSyncDetails = null)
        {
            SyncOptions options = new SyncOptions
            {
                DecrDirectory = "Decr2",
                EncrDirectory = "Encr1",
                Password = "secret",
                Initialize = true
            };
            ConsoleEx console = new ConsoleEx();
            console.BeforeWriteLine = (o) =>
            {
                PreSyncDetails preSync = o as PreSyncDetails;
                if (preSync != null)
                    onPreSyncDetails?.Invoke(preSync);
            };

            SyncCommand.Sync(options, console, HelixFileVersion.UnitTest);
        }

        [Test]
        public void SyncCommand_SimpleSync()
        {
            Decr1.UpdateTo("file1.txt < aa");
            SyncDecr1andEncr1();
            SyncDecr2andEncr1();
            Assert.IsTrue(Decr2.EqualTo("file1.txt < aa"));

            Decr1.UpdateTo("file1.txt < aa",
                           "file2.txt < bb");
        }
        
        [Test]
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

            Assert.IsTrue(Decr2.EqualTo("file1.txt < abc"));
        }

        [Test]
        public void SyncCommand_ChangeCaseOnly()
        {
            for (int i = 0; i < 10; i++)
            {
                Decr1.UpdateTo("A < xyz");
                SyncDecr1andEncr1(p => Assert.IsTrue(p.Side == PairSide.Decrypted));
                SyncDecr2andEncr1(p => Assert.IsTrue(p.Side == PairSide.Encrypted));
                Decr1.AssertEqual(new string[] { "A < xyz" });
                Decr2.AssertEqual(new string[] { "A < xyz" });

                Decr1.UpdateTo("a");
                SyncDecr1andEncr1(p => Assert.IsTrue(p.Side == PairSide.Decrypted));
                SyncDecr2andEncr1(p => Assert.IsTrue(p.Side == PairSide.Encrypted));
                Decr1.AssertEqual(new string[] { "a" });
                Decr2.AssertEqual(new string[] { "a" });
            }

            Assert.Fail("Sometimes works sometimes fails depending if th delete comes before the add");
        }

        static string[] choices = new string[]
        {
            "a",
            "a:2",
            "A",
        };

        [FsCheck.NUnit.Property]
        public void SyncCommand_OneWaySync_NoConflict_FuzzTesting(byte[] d1, byte[] d2)
        {
            
            Decr1.Clear(true);
            Decr2.Clear(true);

            string[] d1str = (d1 ?? (new byte[] { })).Select(d => choices[d % choices.Length]).ToArray();
            string[] d2str = (d2 ?? (new byte[] { })).Select(d => choices[d % choices.Length]).ToArray();

            Decr1.UpdateTo(d1str);
            SyncDecr1andEncr1(p => Assert.IsTrue(p.Side == PairSide.Decrypted));
            SyncDecr2andEncr1(p => Assert.IsTrue(p.Side == PairSide.Encrypted));
            Decr1.AssertEqual(d1str);
            Decr2.AssertEqual(d1str);

            Decr1.UpdateTo(d2str);
            SyncDecr1andEncr1(p => Assert.IsTrue(p.Side == PairSide.Decrypted));
            SyncDecr2andEncr1(p => Assert.IsTrue(p.Side == PairSide.Encrypted));
            Decr1.AssertEqual(d2str);
            Decr2.AssertEqual(d2str);
        }


        [FsCheck.NUnit.Property]
        public void SyncCommand_TwoWaySync_NoConflict_FuzzTesting(byte[] d1, byte[] d2)
        {
            string[] d1str = (d1 ?? (new byte[] { })).Select(d => choices[d % choices.Length]).ToArray();
            string[] d2str = (d2 ?? (new byte[] { })).Select(d => choices[d % choices.Length]).ToArray();

            Decr1.Subdirectory("d1").UpdateTo(d1str);
            Decr2.Subdirectory("d2").UpdateTo(d2str);

            Decr1.Subdirectory("d1").AssertEqual(d1str);
            Decr2.Subdirectory("d2").AssertEqual(d2str);

            SyncDecr1andEncr1(p => Assert.IsTrue(p.Side == PairSide.Decrypted));
            Decr1.Subdirectory("d1").AssertEqual(d1str);
            SyncDecr1andEncr1(p => Assert.Fail("Unexpected change post sync"));

            SyncDecr2andEncr1();
            Decr2.Subdirectory("d1").AssertEqual(d1str);
            Decr2.Subdirectory("d2").AssertEqual(d2str);
            SyncDecr2andEncr1(p => Assert.Fail("Unexpected change post sync"));

            SyncDecr1andEncr1(p => Assert.IsTrue(p.Side == PairSide.Encrypted));
            Decr1.Subdirectory("d1").AssertEqual(d1str);
            Decr1.Subdirectory("d2").AssertEqual(d2str);

        }


        public void DirectoryAreEqual(DirectoryTester tester, string content, string message)
        {
            Assert.AreEqual(new DirectoryTester.DirectoryEntryCollection(content).ToString(), 
                            tester.GetContent().ToString(),
                            message);
        }

    }
}
