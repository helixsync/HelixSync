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
    public class HelixEncrDirectoryTests : IDisposable
    {
        public HelixEncrDirectoryTests(){
            ResetDirectory();
        }
        public void Dispose() {
            ResetDirectory();
        }

        public void ResetDirectory()
        {
            System.IO.Directory.SetCurrentDirectory(Path.GetDirectoryName(this.GetType().GetTypeInfo().Assembly.Location));
            Directory.CreateDirectory("hxdir");
            Directory.Delete("hxdir", true);

            Directory.CreateDirectory("hxdir2");
            Directory.Delete("hxdir2", true);
        }


        [Fact]
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
