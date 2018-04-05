// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using HelixSync;
using HelixSync.FileSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync.Test
{

    [TestClass]
    public class FileSystem_Tests
    {
        [TestMethod]
        public void TryGetEntryTest()
        {
            if (Directory.Exists("AA"))
                Directory.Delete("AA", true);

            try
            {
                Directory.CreateDirectory("AA");

                File.WriteAllText("abc.txt", "example");
                Directory.CreateDirectory($@"AA\AB");
                File.WriteAllText(@"AA\AB\xyz.txt", "example");

                var fsDirectory = new FSDirectory("AA", true);
                var child = fsDirectory.TryGetEntry(@"AB\xyz.txt");

                Assert.AreEqual(@"AB\xyz.txt", child.RelativePath);
                Assert.IsNull(fsDirectory.TryGetEntry("missing.txt"));
                Assert.IsInstanceOfType(fsDirectory.TryGetEntry("AB"), typeof(FSDirectory));
            }
            finally
            {
                Directory.Delete("AA", true);
            }
        }

        [TestMethod]
        public void MoveLive()
        {
            if (Directory.Exists("AA"))
                Directory.Delete("AA", true);

            try
            {
                Directory.CreateDirectory("AA");


                //Setup
                File.WriteAllText(@"AA\abc.txt", "example");
                Directory.CreateDirectory($@"AA\AB");

                //Test
                var fsDirectory = new FSDirectory("AA", false);
                (fsDirectory.TryGetEntry("abc.txt") as FSFile).MoveTo(@"AB\def.txt");

                //Check
                Assert.IsNull(fsDirectory.TryGetEntry("abc.txt"));
                Assert.IsNotNull(fsDirectory.TryGetEntry(@"AB\def.txt"));

                Assert.IsTrue(File.Exists(@"AA\AB\def.txt"));
                Assert.IsFalse(File.Exists(@"AA\abc.txt"));
            }
            finally
            {
                Directory.Delete("AA", true);
            }


        }

        [TestMethod]
        public void MoveWhatIf()
        {
            if (Directory.Exists("AA"))
                Directory.Delete("AA", true);

            try
            {
                Directory.CreateDirectory("AA");

                //Setup
                File.WriteAllText(@"AA\abc.txt", "example");
                Directory.CreateDirectory($@"AA\AB");

                //Test
                var fsDirectory = new FSDirectory("AA", true);
                (fsDirectory.TryGetEntry("abc.txt") as FSFile).MoveTo(@"AB\def.txt");

                //Check
                //fsDirectory should be updated
                Assert.IsNull(fsDirectory.TryGetEntry("abc.txt"));
                Assert.IsNotNull(fsDirectory.TryGetEntry(@"AB\def.txt"));

                //Filesystem should be unchanged
                Assert.IsFalse(File.Exists(@"AA\AB\def.txt"));
                Assert.IsTrue(File.Exists(@"AA\abc.txt"));
            }
            finally
            {
                Directory.Delete("AA", true);
            }
        }
    }
}
