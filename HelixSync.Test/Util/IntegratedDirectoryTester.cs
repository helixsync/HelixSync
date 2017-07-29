using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace HelixSync.Test
{
    public class IntegratedDirectoryTester : IDisposable
    {
        public DirectoryTester Decr1 { get; }  = new DirectoryTester("Decr1", new Regex(@"\.helix.*"));
        public DirectoryTester Decr2 { get; }  = new DirectoryTester("Decr2", new Regex(@"\.helix.*"));
        public DirectoryTester Encr1 { get; }  = new DirectoryTester("Encr1");
        public DirectoryTester Encr2 { get; }  = new DirectoryTester("Encr2");

        public IntegratedDirectoryTester()
        {
            Decr1.Clear(true);
            Decr2.Clear(true);
            Encr1.Clear(true);
            Encr2.Clear(true);
            Assert.False(Directory.Exists(Decr1.DirectoryPath));
            Assert.False(Directory.Exists(Decr2.DirectoryPath));
            Assert.False(Directory.Exists(Encr1.DirectoryPath));
            Assert.False(Directory.Exists(Encr2.DirectoryPath));
        }


        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    Decr1.Dispose();
                    Decr2.Dispose();
                    Encr1.Dispose();
                    Encr2.Dispose();
                }

                IsDisposed = true;
            }
        }

        ~IntegratedDirectoryTester()
        {
            Dispose(false);
        }
    }
}
