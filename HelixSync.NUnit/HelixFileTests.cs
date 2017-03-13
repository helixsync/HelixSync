// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using HelixSync;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FsCheck;
using NUnit.Framework;

namespace HelixSync.NUnit
{
    [TestFixture]
    public class HelixFileTests
    {
        public static void Delete(params string[] files)
        {
            foreach (var file in files)
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
        }


        [Test]
        public void HelixFile_EncryptDecrypt()
        {
            if (File.Exists("encr1.hx")) File.Delete("encr1.hx");
            File.WriteAllText("original1.txt", "hello world");
            HelixFile.Encrypt("original1.txt", "encr1.hx", "password", new FileEncryptOptions { FileVersion = HelixFileVersion.UnitTest });
            HelixFile.Decrypt("encr1.hx", "decrypted1.txt", "password");
            Assert.AreEqual("hello world", File.ReadAllText("decrypted1.txt"));
        }

        [Test]
        public void HelixFile_IllegalFileNameChar()
        {
            //Delete("original1.txt", "encr1.hx", "decrypted1.txt");
            if (File.Exists("encr1.hx")) File.Delete("encr1.hx");

            File.WriteAllText("original1.txt", "hello world");
            HelixFile.Encrypt("original1.txt", "encr1.hx", "password", new FileEncryptOptions
            {
                BeforeWriteHeader = (header) =>
                {
                    header.FileName = "./original1.txt";
                },
                FileVersion = HelixFileVersion.UnitTest,
            });

            try
            {
                HelixFile.Decrypt("encr1.hx", "decrypted1.txt", "password");
                Assert.Fail("Failed to detect file name curruption");
            }
            catch (HeaderCorruptionException) { }
        }

        
        [Test]
        public void HelixFile_RandomPasswordAndContent()
        {
            FsCheck.Prop.ForAll<string, string>((pass, inp) =>
             {
                 if (File.Exists("temp.txt")) File.Delete("temp.txt");
                 if (File.Exists("temp.hx")) File.Delete("temp.hx");
                 if (File.Exists("temp.decr")) File.Delete("temp.hx");
                 File.WriteAllText("temp.txt", inp ?? "");
                 HelixFile.Encrypt("temp.txt", "temp.hx", DerivedBytesProvider.FromPassword(pass ?? ""), new FileEncryptOptions { FileVersion = HelixFileVersion.UnitTest });
                 HelixFile.Decrypt("temp.hx", "temp.decr", pass ?? "");
                 Assert.AreEqual(inp ?? "", File.ReadAllText("temp.decr"));
             }).QuickCheckThrowOnFailure();
        }

        [Test]
        public void HelixFile_RandomCurruption()
        {

            Prop.ForAll<NonNull<string>, PositiveInt, byte>((inp, pos, adj) =>
            {
                TestRandomCurruption(inp.Get, pos.Get, adj);
            }).QuickCheckThrowOnFailure();
        }

        [Test]
        public void HelixFile_RandomCurruption_KnownIssues()
        {
            TestRandomCurruption("H", 5, 149);
        }
        private static byte TestRandomCurruption(string inp, int pos, byte adj)
        {
            Delete("temp.txt", "temp.decr", "temp.hx");

            File.WriteAllText("temp.txt", inp);
            HelixFile.Encrypt("temp.txt", "temp.hx", "password", new FileEncryptOptions { FileVersion = HelixFileVersion.UnitTest } );
            try
            {
                if (adj == 0)
                    adj = 1;
                using (var file = File.Open("temp.hx", FileMode.Open, FileAccess.ReadWrite))
                {
                    byte[] content = new byte[1];
                    file.Seek(pos % file.Length, SeekOrigin.Begin);
                    file.Read(content, 0, 1);
                    file.Seek(pos % file.Length, SeekOrigin.Begin);
                    unchecked
                    {
                        content[0] = (byte)(content[0] + adj);
                    }
                    file.Write(content, 0, 1);

                }
                HelixFile.Decrypt("temp.hx", "temp.decr", "password");
                throw new Exception("Expected to see curruption none present");
            }
            catch (HelixException)
            {

            }
            Assert.IsFalse(File.Exists("temp.decr"), "File temp.decr expected to have been deleted however has not");
            Delete("temp.txt", "temp.decr", "temp.hx");
            return adj;
        }


        [Test]
        public void HelixFile_LargerFile()
        {
            byte[] b = new byte[50000];
            System.Security.Cryptography.RandomNumberGenerator.Create().GetBytes(b);
            string file1 = nameof(HelixFile_LargerFile);
            string encrFile = file1 + ".hx";
            string file2 = file1 + "_2";
            Delete(file1, encrFile, file2);

            File.WriteAllBytes(file1, b);
            HelixFile.Encrypt(file1, encrFile, "password", new FileEncryptOptions { FileVersion = HelixFileVersion.UnitTest });
            HelixFile.Decrypt(encrFile, file2, "password");
            byte[] b2 = File.ReadAllBytes(file2);
            Assert.IsTrue(Util.BytesEqual(b, b2));

            Delete(file1, encrFile, file2);
        }

        [Test]
        public void HelixFile_LargerFileTruncate()
        {
            byte[] b = new byte[50000];
            System.Security.Cryptography.RandomNumberGenerator.Create().GetBytes(b);
            string file1 = nameof(HelixFile_LargerFileTruncate);
            string encrFile = file1 + ".hx";
            string file2 = file1 + "_2";
            Delete(file1, encrFile, file2);

            File.WriteAllBytes(file1, b);
            HelixFile.Encrypt(file1, encrFile, "password", new FileEncryptOptions { FileVersion = HelixFileVersion.UnitTest });

            //Truncates File by 10 bytes
            byte[] encryptBytes = File.ReadAllBytes(encrFile);
            Assert.IsTrue(encryptBytes.Length > 50000);
            Delete(encrFile);
            Array.Resize(ref encryptBytes, encryptBytes.Length - 10);
            File.WriteAllBytes(encrFile, encryptBytes);


            try
            {
                HelixFile.Decrypt(encrFile, file2, "password");
                Assert.Fail("Expected FileCurruptionException");
            }
            catch(FileCorruptionException)
            {

            }

            Assert.IsFalse(File.Exists(file2));

            Delete(file1, encrFile, file2);
        }
    }
}
