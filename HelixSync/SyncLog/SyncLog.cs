// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    public class SyncLog : IEnumerable<SyncLogEntry>, IDisposable
    {

        private Dictionary<string, SyncLogEntry> LogEntriesByKey = new Dictionary<string, SyncLogEntry>();

        private string path;
        private StreamWriter writer;

        public SyncLogEntry LastEntry { get; private set; }
        public bool WhatIf { get; private set; }

        private SyncLog(string path)
        {
            this.path = path;
        }

        public static SyncLog GetLog(string path, bool whatIf)
        {
            SyncLog syncLog = new SyncLog(path);
            syncLog.WhatIf = whatIf;
            syncLog.Reload();
            return syncLog;
        }

        private void ToMemory(SyncLogEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException("entry");

            var key = entry.Key;

            if (LogEntriesByKey.ContainsKey(key))
            {
                var removeEntry = LogEntriesByKey[key];
                LogEntriesByKey.Remove(key);
            }

            if (entry.EntryType != FileEntryType.Purged)
                LogEntriesByKey.Add(key, entry);
        }
        private void ToDisk(SyncLogEntry entry)
        {
            if (WhatIf)
                throw new InvalidOperationException("Unable to perform action in WhatIf mode");
            writer.WriteLine();
            writer.Write(entry.ToString());
        }

        private void InitializeWriter()
        {
            if (WhatIf)
                throw new InvalidOperationException("Unable to perform action in WhatIf mode");

            if (writer != null)
            {
                writer.Dispose();
                writer = null;
            }

            writer = File.AppendText(path);
            writer.AutoFlush = true;
        }

        public SyncLogEntry FindByDecrFileName(string decrFileName)
        {
            return LogEntriesByKey.Values.FirstOrDefault(e => e.DecrFileName == decrFileName);
        }
        public SyncLogEntry FindByEncrFileName(string fileName)
        {
            return LogEntriesByKey.Values.FirstOrDefault(e => e.EncrFileName == fileName);
        }


        public void Add(SyncLogEntry entry)
        {
            ToMemory(entry);
            if (!WhatIf)
                ToDisk(entry);
        }

        public void Flush()
        {
            if (writer == null) throw new ObjectDisposedException("LogWriter");

            writer.Flush();
        }

        public void Dispose()
        {
            if (writer != null)
            {
                writer.Dispose();
                writer = null;
            }
        }


        public IEnumerator<SyncLogEntry> GetEnumerator()
        {
            return LogEntriesByKey.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return LogEntriesByKey.Values.GetEnumerator();
        }

        /// <summary>
        /// Reloads the log
        /// </summary>
        public void Reload()
        {
            if (writer != null)
            {
                writer.Dispose();
                writer = null;
            }

            if (File.Exists(path)) //missing file
            {
                using (var reader = File.OpenText(path))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        if (line.StartsWith("#") || string.IsNullOrEmpty(line))
                            continue; //comment (ignore)

                        var logEntry = SyncLogEntry.TryParseFromString(line);
                        if (logEntry != null)
                        {
                            ToMemory(logEntry);
                            LastEntry = logEntry;
                        }
                    }
                }
            }

            if (!WhatIf)
                InitializeWriter();
        }

    }
}
