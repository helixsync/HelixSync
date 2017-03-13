// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using HelixSync;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using System.Reflection;

namespace HelixSync.NUnit
{
    [TestFixture]
    public class HelixEncrDirectoryTests
    {
        [SetUp]
        [TearDown]
        public void ResetDirectory()
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            Directory.CreateDirectory("hxdir");
            Directory.Delete("hxdir", true);

            Directory.CreateDirectory("hxdir2");
            Directory.Delete("hxdir2", true);
        }


        [Test]
        public void HelixEncrDirectory_NewThenLoad()
        {
            Directory.CreateDirectory("hxdir");
            Directory.Delete("hxdir", true);

            HelixEncrDirectory helixDirectory1 = new HelixEncrDirectory("hxdir");
            helixDirectory1.Initialize(DerivedBytesProvider.FromPassword("password"), HelixFileVersion.UnitTest);
            helixDirectory1.Open(DerivedBytesProvider.FromPassword("password"));

            HelixEncrDirectory helixDirectory2 = new HelixEncrDirectory("hxdir");
            helixDirectory2.Open(DerivedBytesProvider.FromPassword("password"));
            helixDirectory2.Clean();
        }
    }
}
