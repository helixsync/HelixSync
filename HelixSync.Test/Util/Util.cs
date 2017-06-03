// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using HelixSync;
using System;
using System.IO;

namespace HelixSync.Test
{
	public static class Util
	{
		/// <summary>
		/// Replaces path to platform specific seperators
		/// </summary>
		public static string Path(string path) {
			return path.Replace ('\\', System.IO.Path.DirectorySeparatorChar)
				.Replace ('/', System.IO.Path.DirectorySeparatorChar);
		}

        public static void SyncChanges(this DirectoryPair pair)
        {
            foreach (var change in pair.FindChanges())
            {
                Console.WriteLine(change);
                string message;
                bool retry;
                if (pair.TrySync(change, out retry, out message) != SyncStatus.Success)
                    throw new Exception(message);
            }
        }

        public static bool BytesEqual(byte[] bytes1, byte[] bytes2)
        {
            if (bytes1 == null && bytes2 != null)
                return false;
            else if (bytes2 == null && bytes1 != null)
                return false;
            else if (bytes1 == null && bytes2 == null)
                return true;
            else if (bytes1.Length != bytes2.Length)
                return false;
            else
            {
                for (int i = 0; i < bytes1.Length; i++)
                {
                    if (bytes1[i] != bytes2[i])
                        return false;
                }
            }

            return true;
        }

        public static void WriteTextFile(string path, string contents)
        {
            path = Path(path);

            //On Linux the timestamp precision is 1 second
            //To ensure the change is marked the time is moved forward a minimum of 1 second
            DateTime lastTime = new DateTime();

            if (File.Exists(path))
                lastTime = File.GetLastWriteTimeUtc(path);
            File.WriteAllText(path, contents);
            if (File.GetLastWriteTimeUtc(path) <= lastTime)
                File.SetLastWriteTimeUtc(path, lastTime.AddSeconds(1));
        }

        public static void Remove(params string[] paths)
        {
            foreach (var path in paths)
            {
                if (File.Exists(path))
                    File.Delete(path);
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
        }
    }
}

