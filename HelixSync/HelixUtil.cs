// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace HelixSync
{
    public static class HelixUtil
    {
        public static bool FileSystemCaseSensitive
        {
            get
            {
#if NET_CORE
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return false;
                else
                    return true;
#else
                if (Environment.OSVersion.Platform == PlatformID.Unix)
                    return true;
                else
                    return false;
#endif
            }
        }

        public static readonly char UniversalDirectorySeparatorChar = '/';


        /// <summary>
        /// Returns a path switching the directory seperator into the current 
        /// platforms native seperator
        /// 
        /// Windows 'a/b' becomes 'a\b'
        /// Linux 'a\b' becomes 'a/b'
        /// </summary>
        public static string PathNative(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            return (path ?? "")
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Returns a path with the standard '/' (not platform specific) 
        /// directory seperator
        /// </summary>
        public static string PathUniversal(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            if (UniversalDirectorySeparatorChar == '/')
            {
                return (path ?? "")
                    .Replace('\\', '/');
            }
            else if (UniversalDirectorySeparatorChar == '\\')
            {
                return (path ?? "")
                    .Replace('/', '\\');
            }
            else
                throw new NotSupportedException();
        }

        /// <summary>
        /// Returns the path with the correct caseing. Switches to a native path
        /// if needed.
        /// </summary>
        public static string GetExactPathName(string path, int maxDepth = int.MaxValue)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            if (maxDepth <= 0)
                return path;

            if (string.IsNullOrEmpty(path))
                return "";

            if (!(File.Exists(path) || Directory.Exists(path)))
                return path;

            var di = new DirectoryInfo(path);

            
            var properCaseName = di.Parent.GetFileSystemInfos(di.Name).FirstOrDefault();

            if (properCaseName == null)
            {
                //When using Linux with a cases insensitive file system Linux still does a case sensitive search
                //So we have to do a full search to make this work correctly
                properCaseName = di.Parent.GetFileSystemInfos()
                    .Where(i => string.Equals(i.Name, di.Name, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            }

            if (properCaseName == null)
                properCaseName = di;

            var childNodeName = properCaseName.Name;


            return Path.Combine(
                GetExactPathName(Path.GetDirectoryName(path), maxDepth -1),
                childNodeName);
        }

        public static string RemoveRootFromPath(string path, string root)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            if (string.IsNullOrEmpty(root))
                return path;

            if (path == root)
                return "";

            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                root = root + Path.DirectorySeparatorChar;

            if (!path.StartsWith(root, StringComparison.Ordinal))
                throw new ArgumentOutOfRangeException(nameof(path), "path must start with the root directory (path:" + path + ", root: " + root + ")");

            return path.Substring(root.Length);
        }

        /// <summary>
        /// Used to determine if the path has illegal characters or invalid format
        /// </summary>
        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            //to support linux the path/filename restrictions is extreamly minimal
            foreach (char char1 in new char[] { '\0', '\n', '\r' })
            {
                if (path.Contains(char1))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Truncates the DateTime down to the closest millisecond (10000 Ticks)
        /// </summary>
        public static DateTime TruncateTicks(DateTime date)
        {
            return new DateTime(date.Ticks - (date.Ticks % 10000));
        }


        /// <summary>
        /// Returns the Regex Pattern to be used to find a quoted string. 
        /// </summary>
        public static string QuotedPattern
        {
            get
            {
                return @"""(?:[^""\\]|\\.)*""";
            }
        }

        /// <summary>
        /// Returns a quoted and escaped string
        /// </summary>
        public static string Quote(string value)
        {
            return "\""
                + (value ?? "").Replace("\\", "\\\\")
                        .Replace("\n", "\\n")
                        .Replace("\r", "\\r")
                        .Replace("\t", "\\t")
                        .Replace("\f", "\\f")
                        .Replace("\"", "\\\"")
                        .Replace("'", "\\'")
                + "\"";
        }

        /// <summary>
        /// Removes the quote and unescapes a string
        /// </summary>
        public static string Unquote(string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new FormatException("value in invalid format");
            if (value.Length < 2)
                throw new FormatException("value in invalid format");

            if (value.Substring(0, 1) != "\"" ||
                value.Substring(value.Length - 1, 1) != "\"")
                throw new FormatException("value must start and end with a quote (\")");

            value = value.Substring(1, value.Length - 2);

            return Regex.Replace(value, "\\\\.", (m) =>
            {
                if (m.Value == "\\n")
                    return "\n";
                else if (m.Value == "\\r")
                    return "\r";
                else if (m.Value == "\\t")
                    return "\t";
                else if (m.Value == "\\f")
                    return "\f";
                return m.Value.Substring(1);
            });
        }
    }
}
