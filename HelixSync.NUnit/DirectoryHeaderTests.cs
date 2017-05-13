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

namespace HelixSync.NUnit
{
    public class DirectoryHeaderTests : IDisposable
    {
        public DirectoryHeaderTests ()
        {
            ResetDirectory();
        }
        public void Dispose() 
        {
            ResetDirectory();
        }


        public void ResetDirectory()
        {
            System.IO.Directory.SetCurrentDirectory(Path.GetDirectoryName(this.GetType().GetTypeInfo().Assembly.Location));
        }

        [Fact]
        public void DirectoryHeader_NewSaveLoad()
        {
            var newHeader = DirectoryHeader.New();

            DateTime start = DateTime.Now;
            newHeader.Save("header.hx", DerivedBytesProvider.FromPassword("password"), HelixFileVersion.UnitTest);
            double dur = (DateTime.Now - start).TotalMilliseconds;

            var loadHeader = DirectoryHeader.Load("header.hx", DerivedBytesProvider.FromPassword("password"));

            //Assert.Equal(newHeader.DerivedBytesProvider.GetDerivedBytes().Key.ToHex(), loadHeader.DerivedBytesProvider.Key.ToHex());
            //Assert.Equal(newHeader.DerivedBytesProvider.GetDerivedBytes().Salt.ToHex(), loadHeader.DerivedBytesProvider.Salt.ToHex());
            Assert.Equal(newHeader.FileNameKey.ToHex(), loadHeader.FileNameKey.ToHex());
            Assert.Equal(newHeader.DirectoryId, loadHeader.DirectoryId);
            File.Delete("header.hx");
        }

        [Fact]
        public void DirectoryHeader_ThrowsExceptionWhenLoading()
        {
            var newHeader = DirectoryHeader.New();

            newHeader.GetType().GetProperty(nameof(newHeader.DirectoryId)).SetValue(newHeader, "currupt**");
            newHeader.Save("header.hx", DerivedBytesProvider.FromPassword("password"), HelixFileVersion.UnitTest);

            try
            {
                DirectoryHeader.Load("header.hx", DerivedBytesProvider.FromPassword("password"));
                Assert.True(false, "Did not detect curruption");
            }
            catch (HelixException)
            {

            }
        }
    }
}
