// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HelixSync.Test
{
    public class DirectoryTester : IDisposable
    {
        public class DirectoryEntry : IComparable<DirectoryEntry>
        {
            static Regex patern = new Regex(@"^\s*" + @"(?<filename>[^\:\<]+)" + @"\s*" + @"(\:(?<time>[0-9\*]*))?" + @"\s*" + @"(\<(?<content>.*))?" + @"\s*" + @"(?<comments>\#.*)?" + "$", RegexOptions.Compiled);
            public DirectoryEntry(string encoded)
            {
                Match match = patern.Match(encoded);
                string fileName = match.Groups["filename"].Value;
                string time = match.Groups["time"].Value;
                string content = match.Groups["content"].Value;
                bool isDirectory = false;

                if (string.IsNullOrWhiteSpace(fileName))
                    throw new ArgumentOutOfRangeException("Unable to Parse Directory Entry -- " + encoded);



                fileName = fileName.Trim().Replace('\\', '/');
                if (fileName.EndsWith("/"))
                {
                    isDirectory = true;
                    fileName = fileName.TrimEnd('/');
                }

                this.FileName = fileName;
                this.IsDirectory = isDirectory;
                if (int.TryParse(time, out int timeint))
                    this.Time = timeint;
                else
                    this.Time = null;
                if (!match.Groups["content"].Success || (content ?? "").Trim() == "*")
                    this.Content = null;
                else
                    this.Content =  (content ?? "").Trim();
            }
            public DirectoryEntry(string fileName, bool isDirectory, int? time, string content)
            {
                fileName = fileName.Trim().Replace('\\', '/');
                this.FileName = fileName;
                this.IsDirectory = isDirectory;
                this.Time = time;
                this.Content = content;
            }

            public string Content { get; private set; }
            public string FileName { get; private set; }
            public bool IsDirectory { get; private set; }
            public int? Time { get; private set; }

            public override string ToString()
            {
                return FileName + (IsDirectory ? "/" : "") + ":" + (Time == null ? "*" : Time.ToString()) + (IsDirectory ? "" : " < " + (Content == null ? "*" : Content));
            }

            public override bool Equals(object obj)
            {
                if (!(obj is DirectoryEntry))
                    return false;
                return this.ToString() == obj.ToString();
            }

            public override int GetHashCode()
            {
                return this.ToString().GetHashCode();
            }

            public int CompareTo(DirectoryEntry other)
            {
                return FileName.CompareTo(other.FileName);
            }
        }

        public class DirectoryEntryCollection : IEnumerable<DirectoryEntry>
        {
            List<DirectoryEntry> m_List = new List<DirectoryEntry>();
            
            public DirectoryEntryCollection(params string[] content)
            {
                List<DirectoryEntry> directoryEntries = string.Join("\n", content)
                    .Split('\n', '\r')
                   .Where(c => !string.IsNullOrWhiteSpace(c) && !c.Trim().StartsWith("#"))
                   .Select(c => new DirectoryEntry(c))
                   .ToList();

                fillWithMissingDirectories(directoryEntries);
                if (!HelixUtil.FileSystemCaseSensitive)
                    fixCasing(directoryEntries);
                removeDuplicateEntries(directoryEntries);
                directoryEntries.Sort();
                this.m_List = directoryEntries;
            }



            public DirectoryEntryCollection(params DirectoryEntry[] entries)
            {
                List<DirectoryEntry> directoryEntries = new List<DirectoryEntry>(entries);
                fillWithMissingDirectories(directoryEntries);
                directoryEntries.Sort();
                this.m_List = directoryEntries;
            }

            private static void fillWithMissingDirectories(List<DirectoryEntry> directoryEntries)
            {
                //adds in missing directories
                foreach (DirectoryEntry entry in directoryEntries.ToList())
                {
                    if (entry.FileName.Contains('/'))
                    {
                        var pathParts = entry.FileName.Split('/');
                        string workingPath = null;
                        foreach (var part in pathParts)
                        {
                            if (workingPath == null)
                                workingPath = part;
                            else
                                workingPath += '/' + part;

                            if (!directoryEntries.Any(e => e.FileName == workingPath))
                                directoryEntries.Add(new DirectoryEntry(workingPath, true, 0, ""));
                        }
                    }
                }
            }

            private void fixCasing(List<DirectoryEntry> directoryEntries)
            {
                List<DirectoryEntry> clonedList = new List<DirectoryEntry>(directoryEntries);

                for (int i = 0; i < clonedList.Count; i++)
                {
                    var entry = clonedList[i];
                    for (int j = 0; j < i; j++)
                    {
                        if (directoryEntries[j] != null &&
                            directoryEntries[j].FileName.StartsWith(entry.FileName, StringComparison.OrdinalIgnoreCase))
                        {
                            string newFileName = entry.FileName + directoryEntries[j].FileName.Substring(entry.FileName.Length);

                            directoryEntries[j] = new DirectoryEntry(newFileName, directoryEntries[j].IsDirectory, directoryEntries[j].Time, directoryEntries[j].Content);
                        }
                    }
                }          
            }

            private static void removeDuplicateEntries(List<DirectoryEntry> directoryEntries)
            {
                List<DirectoryEntry> clonedList = new List<DirectoryEntry>(directoryEntries);

                for (int i = 0; i < clonedList.Count; i++)
                {
                    var entry = clonedList[i];
                    for (int j = 0; j < i; j++)
                    {
                        if (directoryEntries[j]?.FileName == entry.FileName)
                            directoryEntries[j] = null;
                    }
                }

                directoryEntries.RemoveAll(e => e == null);
            }
            public override string ToString()
            {
                return string.Join(Environment.NewLine, (object[])m_List.ToArray());
            }
            public override bool Equals(object obj)
            {
                if (!(obj is DirectoryEntryCollection))
                    return false;
                return this.ToString() == obj.ToString();
            }
            public override int GetHashCode()
            {
                return this.ToString().GetHashCode();
            }

            public IEnumerator<DirectoryEntry> GetEnumerator()
            {
                return m_List.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return m_List.GetEnumerator();
            }
        }

        public void AssertEqual(string[] content, string message = null)
        {
            var shouldBeContent = new DirectoryEntryCollection(content);
            var directoryContent = GetContent();

            message = message ?? "Directory contents not equal";

            foreach (var shouldBeItem in shouldBeContent)
            {
                var directoryItem = directoryContent.FirstOrDefault(d => d.FileName == shouldBeItem.FileName);
                if (directoryItem == null)
                    throw new DirectoryMismatchException($"{message} (missing file {shouldBeItem})");
                if (shouldBeItem.Time != null && directoryItem.Time != shouldBeItem.Time)
                    throw new DirectoryMismatchException($"{message} (time stamp mismatch {shouldBeItem} != {directoryItem})");
                if (shouldBeItem.Content != null && directoryItem.Content != shouldBeItem.Content)
                    throw new DirectoryMismatchException($"{message} (content mismatch {shouldBeItem} != {directoryItem})");
                if (shouldBeItem.IsDirectory  != directoryItem.IsDirectory)
                    throw new DirectoryMismatchException($"{message} (isdirectory mismatch {shouldBeItem} != {directoryItem})");
            }

            foreach(var directoryItem in directoryContent)
            {
                if (!shouldBeContent.Any(e => e.FileName == directoryItem.FileName))
                {
                    throw new DirectoryMismatchException($"{message} (extra file {directoryItem})");
                }
            }
        }

        public class DirectoryMismatchException : Exception
        {
            public DirectoryMismatchException(string message) : base(message)
            {
            }
        }

        public DirectoryTester(string path, Regex excludedItems = null, bool cleanupOnDispose = true)
        {
            this.DirectoryPath = path;
            this.CleanupOnDispose = cleanupOnDispose;
            this.ExcludedItems = excludedItems;
        }

        public Regex ExcludedItems { get; private set; }
        public string DirectoryPath { get; private set; }
        public bool CleanupOnDispose { get; private set; }

        /// <summary>
        /// Updates the directory to match the files and content provided in the content string
        /// 
        /// </summary>
        /// <param name="content">fileName [&gt; content]</param>
        public void UpdateTo(params string[] content)
        {
            DirectoryEntryCollection directoryEntries = new DirectoryEntryCollection(content);
            
            Directory.CreateDirectory(DirectoryPath);

            Clear(false);


            foreach (DirectoryEntry entry in directoryEntries)
            {
                string filePath = Path.Combine(DirectoryPath, entry.FileName).Replace('/', Path.DirectorySeparatorChar);
                DateTime writeTime = (new DateTime(2010, 1, 1)).AddDays(entry.Time ?? 0);
                if (entry.IsDirectory)
                {
                    Directory.CreateDirectory(filePath);
                    Directory.SetLastWriteTimeUtc(filePath, writeTime);
                }
                else
                {
                    if (!string.IsNullOrEmpty(Path.GetDirectoryName(filePath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    File.WriteAllText(filePath, entry.Content);
                    File.SetLastWriteTimeUtc(filePath, writeTime);
                }
            }

            //Directory timestamps change if a child record is also changed
            foreach (DirectoryEntry entry in directoryEntries.Where(e => e.IsDirectory))
            {
                string filePath = Path.Combine(DirectoryPath, entry.FileName).Replace('/', Path.DirectorySeparatorChar);
                DateTime writeTime = (new DateTime(2010, 1, 1)).AddDays(entry.Time ?? 0);
                Directory.SetLastWriteTimeUtc(filePath, writeTime);
            }
        }

        public DirectoryTester Subdirectory(string path)
        {
            return new DirectoryTester(Path.Combine(this.DirectoryPath, path));
        }

        public DirectoryEntryCollection GetContent()
        {
            List<DirectoryEntry> entries = new List<DirectoryEntry>();

            if (!Directory.Exists(DirectoryPath))
                return new DirectoryEntryCollection("");

            var dirInfo = new DirectoryInfo(DirectoryPath);
            foreach (FileSystemInfo entry in dirInfo.GetFileSystemInfos("*", SearchOption.AllDirectories))
            {
                int time = (int)(entry.LastWriteTimeUtc - (new DateTime(2010, 1, 1))).TotalDays;
                bool isDirectory = entry is DirectoryInfo;
                string content;
                if (isDirectory)
                    content = "";
                else
                    content = File.ReadAllText(entry.FullName);

                string fileName = entry.FullName.Substring((dirInfo.FullName + "/").Length);

                if (ExcludedItems == null || !ExcludedItems.IsMatch(fileName))
                    entries.Add(new DirectoryEntry(fileName, isDirectory, time, content));
            }

            DirectoryEntryCollection entryCollection = new DirectoryEntryCollection(entries.ToArray());
            return entryCollection;
        }

        public void Clear(bool removeAll)
        {
            if (removeAll)
                Clear(null);
            else
                Clear(ExcludedItems);
        }

        public void Clear(Regex excludedItems)
        {
            if (Directory.Exists(DirectoryPath))
            {
                if (excludedItems == null)
                    Directory.Delete(DirectoryPath, true);
                else {
                    var dirInfo = new DirectoryInfo(DirectoryPath);
                    foreach (FileSystemInfo entry in dirInfo.GetFileSystemInfos("*", SearchOption.AllDirectories).Reverse())
                    {
                        string fileName = entry.FullName.Substring((dirInfo.FullName + "/").Length);

                        if (excludedItems == null || !excludedItems.IsMatch(fileName))
                            entry.Delete();
                    }
                }
            }
        }
        public bool EqualTo(params string[] content)
        {
            DirectoryEntryCollection entryCollection = new DirectoryEntryCollection(content);
            DirectoryEntryCollection contents = GetContent();

            return entryCollection.Equals(contents);
        }

        public void Dispose()
        {
            if (CleanupOnDispose)
            {
                if (Directory.Exists(DirectoryPath))
                    Directory.Delete(DirectoryPath, true);
            }
        }
    }
}
