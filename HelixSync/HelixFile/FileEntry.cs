using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace HelixSync
{
    public class FileEntry
    {
        public string FileName { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public FileEntryType EntryType { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        public long Length { get; set; }

        public bool IsValid(out HeaderCorruptionException exception)
        {
            var header = this;

            if (string.IsNullOrEmpty(header.FileName))
            {
                exception = new HeaderCorruptionException("FileName is blank");
                return false;
            }
            string correctedFileName = header.FileName.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            foreach (var invalidChar in Path.GetInvalidPathChars())
            {
                if (correctedFileName.Contains(invalidChar))
                {
                    exception = new HeaderCorruptionException("FileName contains an invalid character");
                    return false;
                }
            }

            if (Path.IsPathRooted(correctedFileName))
            {
                exception = new HeaderCorruptionException("FileName is rooted (potential path traversal)");
                return false;
            }

            if (correctedFileName.StartsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.InvariantCulture))
            {
                exception = new HeaderCorruptionException("FileName starts with a directory separator  (potential path traversal)");
                return false;
            }

            if (correctedFileName.Split(Path.DirectorySeparatorChar).Contains(".") ||
                correctedFileName.Split(Path.DirectorySeparatorChar).Contains(".."))
            {
                exception = new HeaderCorruptionException("FileName contains . or .. (potential path traversal)");
                return false;
            }

            exception = null;
            return true;
        }

        public static FileEntry FromFile(string fullPath, string root)
        {
            root = HelixUtil.PathNative(root);
            fullPath = HelixUtil.PathNative(fullPath);

            string casedFullPath = HelixUtil.GetExactPathName(fullPath);
            FileEntry fileInfo = new FileEntry();
            fileInfo.FileName = HelixUtil.PathUniversal(HelixUtil.RemoveRootFromPath(fullPath, root));

            var fi = new FileInfo(casedFullPath);

            //Check to ensure ends with to address case changes
            if (fi.Exists && fi.FullName.EndsWith(fullPath, StringComparison.InvariantCulture))
            {
                fileInfo.LastWriteTimeUtc = HelixUtil.TruncateTicks(fi.LastWriteTimeUtc);
                fileInfo.Length = fi.Length;
                fileInfo.EntryType = FileEntryType.File;
                return fileInfo;
            }

            var di = new DirectoryInfo(casedFullPath);
            if (di.Exists && di.FullName.EndsWith(fullPath, StringComparison.InvariantCulture))
            {
                fileInfo.LastWriteTimeUtc = DateTime.MinValue;
                fileInfo.EntryType = FileEntryType.Directory;
                return fileInfo;
            }

            fileInfo.LastWriteTimeUtc = DateTime.MinValue;
            fileInfo.EntryType = FileEntryType.Removed;
            return fileInfo;
        }

        public static IEnumerable<FileEntry> GetChildren(string fullPath, string root)
        {
            fullPath = HelixUtil.PathNative(fullPath);
            root = HelixUtil.PathNative(root);

            FileEntry fe = FromFile(fullPath, root);
            if (fe.EntryType != FileEntryType.Directory)
                return new List<FileEntry>();

            DirectoryInfo directoryInfo = new DirectoryInfo(Path.Combine(root, fe.FileName));

            List<FileEntry> results = new List<FileEntry>();
            foreach (FileSystemInfo fsInfo in directoryInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            {
                FileEntry fileInfo = new FileEntry();


                fileInfo.FileName = HelixUtil.RemoveRootFromPath(fsInfo.FullName, directoryInfo.FullName);
                if (Path.DirectorySeparatorChar != HelixUtil.UniversalDirectorySeparatorChar)
                    fileInfo.FileName = HelixUtil.PathUniversal(fileInfo.FileName);
                if (fsInfo is FileInfo)
                {
                    fileInfo.LastWriteTimeUtc = HelixUtil.TruncateTicks(fsInfo.LastWriteTimeUtc);
                    fileInfo.Length = ((FileInfo)fsInfo).Length;
                    fileInfo.EntryType = FileEntryType.File;
                    results.Add(fileInfo);
                }
                else if (fsInfo is DirectoryInfo)
                {
                    fileInfo.LastWriteTimeUtc = DateTime.MinValue;
                    fileInfo.EntryType = FileEntryType.Directory;
                    results.Add(fileInfo);
                }
            }

            return results;
        }



        public override string ToString()
        {
            if (EntryType == FileEntryType.Directory)
                return "<DIR> " + FileName;
            else if (EntryType == FileEntryType.File)
                return "<FIL> " + FileName;
            else if (EntryType == FileEntryType.Removed)
                return "<DEL> " + FileName;
            else if (EntryType == FileEntryType.Purged)
                return "<PUR> " + FileName;
            else
                return "<UNK> " + FileName;
        }
    }
}