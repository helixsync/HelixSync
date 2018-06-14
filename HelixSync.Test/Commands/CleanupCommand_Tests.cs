using HelixSync.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace HelixSync.Test
{
    [TestClass]
    public class CleanupCommand_Tests
    {
        [TestMethod]
        public void MultipleCleanups()
        {
            using (var directoryTester = new DirectoryTester($"{this.GetType().Name}/AA"))
            {
                directoryTester.UpdateTo(new string[]
                {
                    $"aa.txt < 1",
                    $"aa.txt{HelixConsts.StagedHxExtention} < 2",
                    $"bb.txt{HelixConsts.BackupExtention} < 2",
                    $"cc/",
                    $"cc/dd.txt{HelixConsts.BackupExtention} < 4",
                });

                CleanupCommand.Cleanup(new CleanupOptions { DecrDirectory = directoryTester.DirectoryPath });

                directoryTester.AssertEqual(new string[]
                {
                    $"aa.txt < 1",
                    $"bb.txt < 2",
                    $"cc/",
                    $"cc/dd.txt < 4",
                });
            }
        }
        [TestMethod]
        public void CleanupWithStagedFiles()
        {
            using (var directoryTester = new DirectoryTester($"{this.GetType().Name}/AA"))
            {
                directoryTester.UpdateTo(new string[]
                {
                $"aa.txt < 1",
                $"aa.txt{HelixConsts.StagedHxExtention} < 2",
                });
                CleanupCommand.Cleanup(new CleanupOptions { DecrDirectory = directoryTester.DirectoryPath });
                directoryTester.AssertEqual(new string[]
                {
                "aa.txt < 1",
                });
            }
        }

        [TestMethod]
        public void CleanupWithBackupFile()
        {
            using (var directoryTester = new DirectoryTester($"{this.GetType().Name}/AA"))
            {
                directoryTester.UpdateTo(new string[]
                {
                    $"aa.txt < 1",
                    $"aa.txt{HelixConsts.BackupExtention} < 2",
                    $"bb.txt < 3",
                });
                CleanupCommand.Cleanup(new CleanupOptions { DecrDirectory = directoryTester.DirectoryPath });
                directoryTester.AssertEqual(new string[]
                {
                    "aa.txt < 2",
                    "bb.txt < 3",
                });
            }
        }

        [TestMethod]
        public void WhatIf()
        {
            //What If
            using (var directoryTester = new DirectoryTester($"{this.GetType().Name}/AA"))
            {
                directoryTester.UpdateTo(new string[]
                {
                    $"aa.txt < 1",
                    $"aa.txt{HelixConsts.StagedHxExtention} < 2",
                    $"bb.txt{HelixConsts.BackupExtention} < 2",
                });

                CleanupCommand.Cleanup(new CleanupOptions { DecrDirectory = directoryTester.DirectoryPath, WhatIf = true });

                directoryTester.AssertEqual(new string[]
                {
                    $"aa.txt < 1",
                    $"aa.txt{HelixConsts.StagedHxExtention} < 2",
                    $"bb.txt{HelixConsts.BackupExtention} < 2",
                });
            }
        }
    }
}
