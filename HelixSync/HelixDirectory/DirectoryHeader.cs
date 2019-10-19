// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using HelixSync;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HelixSync
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class DirectoryHeader
    {
        private DirectoryHeader()
        { }

        public FileEntry Header { get; private set; }
        public HelixFileVersion FileVersion { get; private set; }

        [JsonProperty]
        public byte[] FileNameKey { get; private set; }
        
        [JsonProperty]
        public string DirectoryId { get; private set; }

        public static DirectoryHeader Load(string filePath, DerivedBytesProvider derivedBytesProvider)
        {
            DirectoryHeader directoryHeader = new DirectoryHeader();
            using Stream streamIn = File.OpenRead(filePath);
            using HelixFileDecryptor decryptor = new HelixFileDecryptor(streamIn);
            decryptor.Initialize(derivedBytesProvider);
            directoryHeader.FileVersion = decryptor.FileVersion;

            directoryHeader.Header = decryptor.ReadHeader();
            directoryHeader.FileVersion = decryptor.FileVersion;

            var contentSerialized = decryptor.GetContentString();
            //var contentDeserialized = JsonConvert.DeserializeAnonymousType(contentSerialized, new { EncryptedFileNameSalt = new byte[] { } });
            JsonConvert.PopulateObject(contentSerialized, directoryHeader);

            if (!Regex.IsMatch(directoryHeader.DirectoryId, "^[0-9A-F]{32}$"))
                throw new HelixException("Data curruption directory header, DirectoryId malformed");
            if (directoryHeader.FileNameKey == null || directoryHeader.FileNameKey.Length != 32)
                throw new HelixException("Data curruption directory header, FileNameKey missing or insufficient length");

            return directoryHeader;
        }

        public static DirectoryHeader New(RandomNumberGenerator rng = null)
        {
            rng ??= RandomNumberGenerator.Create();

            DirectoryHeader directoryHeader = 
                new DirectoryHeader
            {
                Header = new FileEntry { FileName = "helix.hx" },

                FileNameKey = new byte[256 / 8]
            };
            rng.GetBytes(directoryHeader.FileNameKey);

            directoryHeader.DirectoryId = NewDirectoryId(rng);

            return directoryHeader;
        }


        /// <summary>
        /// Returns an empty (all zeros) directory id
        /// </summary>
        public static string EmptyDirectoryId()
        {
            byte[] idBytes = new byte[128 / 8];
            return BitConverter.ToString(idBytes).Replace("-", string.Empty);
        }

        /// <summary>
        /// Returns a formated directory
        /// </summary>
        /// <param name="rng">Optional random number generator</param>
        public static string NewDirectoryId(RandomNumberGenerator rng = null)
        {
            rng ??= RandomNumberGenerator.Create();
            byte[] idBytes = new byte[128 / 8];
            rng.GetBytes(idBytes);
            return BitConverter.ToString(idBytes).Replace("-", string.Empty);
        }

        public void Save(string filePath, DerivedBytesProvider derivedBytesProvider, HelixFileVersion fileVersion = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));
            if (derivedBytesProvider == null)
                throw new ArgumentNullException(nameof(derivedBytesProvider));

            FileVersion = fileVersion ?? HelixFileVersion.Default;

            using Stream streamOut = File.Open(filePath, FileMode.Create, FileAccess.Write);
            using HelixFileEncryptor encryptor = new HelixFileEncryptor(streamOut, derivedBytesProvider, fileVersion);
            encryptor.WriteHeader(Header);

            var contentSerialized = JsonConvert.SerializeObject(this);

            encryptor.WriteContent(contentSerialized);
        }
    }
}
