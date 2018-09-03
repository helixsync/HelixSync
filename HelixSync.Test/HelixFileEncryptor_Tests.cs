// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HelixSync;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelixSync.Test
{
    [TestClass]
    public class HelixFileEncryptor_Tests
    {
        [TestMethod]
        public void HelixFileDecryptor_EncryptThenDecrypt()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                FileEntry headerSaved;
                using (HelixFileEncryptor encryptor = new HelixFileEncryptor(stream, DerivedBytesProvider.FromPassword( "password"), HelixFileVersion.UnitTest))
                {
                    headerSaved = new FileEntry();
                    headerSaved.FileName = "testfile.txt";
                    encryptor.WriteHeader(headerSaved);
                    encryptor.WriteContent("example content");

                    Assert.IsTrue(stream.Length > 10);
                }
                //Assert.IsTrue(stream.CanRead, "stream.CanRead");
                //Assert.IsTrue(stream.CanWrite, "stream.CanWrite");


                using (var readStream = new MemoryStream(stream.ToArray(), false))
                using (HelixFileDecryptor decryptor = new HelixFileDecryptor(readStream))
                {
                    decryptor.Initialize(DerivedBytesProvider.FromPassword("password"));
                    var header2 = decryptor.ReadHeader();
                    Assert.AreEqual(headerSaved.FileName, header2.FileName);
                    var content = decryptor.GetContentString();
                    Assert.AreEqual("example content", content);
                }
            }
        }

        [TestMethod]
        public void HelixFileDecryptor_UnflushedEncryptThrowsException()
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    FileEntry headerSaved;
                    using (HelixFileEncryptor encryptor = new HelixFileEncryptor(stream, DerivedBytesProvider.FromPassword("password"), HelixFileVersion.UnitTest))
                    {
                        headerSaved = new FileEntry();
                        headerSaved.FileName = "testfile.txt";
                        encryptor.WriteHeader(headerSaved);
                        encryptor.WriteContent("example content");

                        Assert.IsTrue(stream.Length > 10);


                        using (var readStream = new MemoryStream(stream.ToArray(), false))
                        using (HelixFileDecryptor decryptor = new HelixFileDecryptor(readStream))
                        {
                            decryptor.Initialize(DerivedBytesProvider.FromPassword("password"));
                            var header2 = decryptor.ReadHeader();
                            Assert.AreEqual(headerSaved.FileName, header2.FileName);
                            var content = decryptor.GetContentString();
                            Assert.AreEqual("example content", content);
                        }
                    }
                }
                Assert.IsTrue(false, "Expected a HelixException to be raised");
            }
            catch(HelixException)
            {

            }
        }
        
        [TestMethod]
        public void HelixFileDecryptor_WrongPassword()
        {
            using (MemoryStream stream = new MemoryStream())
            using (HelixFileEncryptor encryptor = new HelixFileEncryptor(stream, DerivedBytesProvider.FromPassword("password"), HelixFileVersion.UnitTest)) 
            {
                var header = new FileEntry();
                header.FileName = "testfile.txt";
                encryptor.WriteHeader(header);
                encryptor.WriteContent("example content");
                encryptor.FlushFinalBlock();
                Assert.IsTrue(stream.Length > 10);

                stream.Position = 0;
                try
                {
                    using (HelixFileDecryptor decryptor = new HelixFileDecryptor(stream))
                    {
                        decryptor.Initialize(DerivedBytesProvider.FromPassword("wrongpassword"));
                        var header2 = decryptor.ReadHeader();
                        Assert.AreEqual(header.FileName, header2.FileName);
                        var content = decryptor.GetContentString();
                        Assert.AreEqual("example content", content);
                    }
                    Assert.IsTrue(false, "Expecting InvalidPasswordException");
                }
                catch(InvalidPasswordException)
                {
                    
                }
            }
        }

        [TestMethod]
        public void HelixFileDecryptor_DecryptHeaderOnlyLargeFile()
        {
            byte[] b = new byte[50000];
            System.Security.Cryptography.RandomNumberGenerator.Create().GetBytes(b);

            byte[] enc;
            using (MemoryStream originalContent = new MemoryStream(b, 0, b.Length))
            using (MemoryStream encryptedStream = new MemoryStream())
            using (var encryptor = new HelixFileEncryptor(encryptedStream, DerivedBytesProvider.FromPassword("password"), HelixFileVersion.UnitTest))
            {
                encryptor.WriteHeader(new FileEntry());
                encryptor.WriteContent(originalContent);

                encryptor.FlushFinalBlock();

                enc = encryptedStream.ToArray();
                Assert.IsTrue(enc.Length > 50000);
            }

            using (MemoryStream encryptedStream = new MemoryStream(enc, true))
            using (var decryptor = new HelixFileDecryptor(encryptedStream))
            {
                decryptor.Initialize(DerivedBytesProvider.FromPassword("password"));
                var header = decryptor.ReadHeader();
                Assert.IsNotNull(header);
                decryptor.Dispose();
            }
        }

    }
}
