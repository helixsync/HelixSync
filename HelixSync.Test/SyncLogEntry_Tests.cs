// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using HelixSync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HelixSync.Test
{
    public class SyncLogEntry_Tests
    {        
#if FSCHECK
        [Test]
        public void SyncLogEntry_PropTesting()
        {
            for (int i = 0; i < 100; i++)
            {
                var entryParam = new {
                    type = RandomValue.GetValue<FileEntryType>(),
                    decrFileName = RandomValue.GetString(HelixUtil.IsValidPath),
                    decrModified = RandomValue.GetValue<DateTime>(),
                    encrFileName = RandomValue.GetString(HelixUtil.IsValidPath),
                    encrModified = RandomValue.GetValue<DateTime>(),
                };
                var entry = new SyncLogEntry(entryParam.type,
                                entryParam.decrFileName,
                                entryParam.decrModified,
                                entryParam.encrFileName,
                                entryParam.encrModified);

                Assert.IsNotNull(entry.ToString(), "Null string returned" + entryParam.ToString());

                var entry2 = SyncLogEntry.TryParseFromString(entry.ToString());
                Assert.IsNotNull(entry2, 
                    "Failed parsing to string '" + entry.ToString() + "' " + entryParam.ToString());
                
                Assert.AreEqual(entry.DecrModified, entry2.DecrModified);
                Assert.AreEqual(entry.EncrModified, entry2.EncrModified);
                Assert.AreEqual(entry.DecrFileName, entry2.DecrFileName);
                Assert.AreEqual(entry.DecrModified, entry2.DecrModified);
            }
        }
#endif

    }
}
