using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace HelixSync.Test
{
    public abstract class IntegratedDirectoryTester : IDisposable
    {
        public string BaseDir { get; }
        
        public DirectoryTester Decr1 { get; }
        public DirectoryTester Decr2 { get; }
        public DirectoryTester Encr1 { get; }
        public DirectoryTester Encr2 { get; }                              

        public IntegratedDirectoryTester()
        {
            BaseDir = this.GetType().Name;
            Decr1  = new DirectoryTester($"{BaseDir}/Decr1", new Regex(@"\.helix.*"));
            Decr2  = new DirectoryTester($"{BaseDir}/Decr2", new Regex(@"\.helix.*"));
            Encr1  = new DirectoryTester($"{BaseDir}/Encr1");
            Encr2  = new DirectoryTester($"{BaseDir}/Encr2");

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
