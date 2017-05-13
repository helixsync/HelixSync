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
    public class DirectoryHeaderTests
    {
        [SetUp]
        [TearDown]
        public void ResetDirectory()
        {
            //Environment.CurrentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            System.IO.Directory.SetCurrentDirectory(Path.GetDirectoryName(typeof(AssemblySetup).GetTypeInfo().Assembly.Location));
        }

        [Test]
        public void DirectoryHeader_NewSaveLoad()
        {
            var newHeader = DirectoryHeader.New();

            DateTime start = DateTime.Now;
            newHeader.Save("header.hx", DerivedBytesProvider.FromPassword("password"), HelixFileVersion.UnitTest);
            double dur = (DateTime.Now - start).TotalMilliseconds;

            var loadHeader = DirectoryHeader.Load("header.hx", DerivedBytesProvider.FromPassword("password"));

            //Assert.AreEqual(newHeader.DerivedBytesProvider.GetDerivedBytes().Key.ToHex(), loadHeader.DerivedBytesProvider.Key.ToHex());
            //Assert.AreEqual(newHeader.DerivedBytesProvider.GetDerivedBytes().Salt.ToHex(), loadHeader.DerivedBytesProvider.Salt.ToHex());
            Assert.AreEqual(newHeader.FileNameKey.ToHex(), loadHeader.FileNameKey.ToHex());
            Assert.AreEqual(newHeader.DirectoryId, loadHeader.DirectoryId);
            File.Delete("header.hx");
        }

        [Test]
        public void DirectoryHeader_ThrowsExceptionWhenLoading()
        {
            var newHeader = DirectoryHeader.New();

            newHeader.GetType().GetProperty(nameof(newHeader.DirectoryId)).SetValue(newHeader, "currupt**");
            newHeader.Save("header.hx", DerivedBytesProvider.FromPassword("password"), HelixFileVersion.UnitTest);

            try
            {
                DirectoryHeader.Load("header.hx", DerivedBytesProvider.FromPassword("password"));
                Assert.Fail("Did not detect curruption");
            }
            catch (HelixException)
            {

            }
        }
    }
}
