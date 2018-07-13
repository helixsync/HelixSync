using System;
using System.IO;

namespace HelixSync
{
    public static class HelixFile
    {
        public static void Encrypt(string decrFile, string encrFile, string password, FileEncryptOptions options = null)
        {
            Encrypt(decrFile, encrFile, DerivedBytesProvider.FromPassword(password), options);
        }
        /// <summary>
        /// Encrypts a file and returns the name of the encrypted file
        /// </summary>
        /// <param name="decrFilePath">The file name and path of the file to be encrypted</param>
        /// <param name="encrFilePath">The file name and path for th encrypted file to be saved</param>
        public static void Encrypt(string decrFilePath, string encrFilePath, DerivedBytesProvider derivedBytesProvider, FileEncryptOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(decrFilePath))
                throw new ArgumentNullException(nameof(decrFilePath));
            if (string.IsNullOrEmpty(encrFilePath))
                throw new ArgumentNullException(nameof(encrFilePath));
            if (derivedBytesProvider == null)
                throw new ArgumentNullException(nameof(derivedBytesProvider));

            options = options ?? new FileEncryptOptions();

            var encrStagedFileName = encrFilePath + HelixConsts.StagedHxExtention;
            var encrBackupFileName = encrFilePath + HelixConsts.BackupExtention;

            if (!string.IsNullOrEmpty(Path.GetDirectoryName(encrStagedFileName)))
                Directory.CreateDirectory(Path.GetDirectoryName(encrStagedFileName));

            FileEntry header = FileEntry.FromFile(decrFilePath, Path.GetDirectoryName(decrFilePath));
            if (!string.IsNullOrWhiteSpace(options.StoredFileName))
                header.FileName = options.StoredFileName;

            using (Stream streamIn = (header.EntryType != FileEntryType.File)
                                       ? (Stream)(new MemoryStream())
                                       : File.Open(decrFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var streamOut = File.Open(encrStagedFileName, FileMode.CreateNew, FileAccess.Write))
            using (var encryptor = new HelixFileEncryptor(streamOut, derivedBytesProvider, options.FileVersion))
            {
                options.Log?.Invoke("Writing: Header");
                options.BeforeWriteHeader?.Invoke(header);
                encryptor.WriteHeader(header);

                options.Log?.Invoke("Writing: Content");
                encryptor.WriteContent(streamIn, streamIn.Length);
            }


            if (File.Exists(encrFilePath))
            {
                if (File.Exists(encrBackupFileName))
                {
                    options.Log?.Invoke("Destaging: Removing incomplete backup");
                    File.Delete(encrBackupFileName);
                }

                options.Log?.Invoke("Destaging: Moving staged to normal");

                if (File.Exists(encrFilePath))
                    File.Move(encrFilePath, encrBackupFileName);
                File.Move(encrStagedFileName, encrFilePath);

                options.Log?.Invoke("Destaging: Removing backup");
                File.Delete(encrBackupFileName);
            }
            else
            {
                options.Log?.Invoke("Destaging: Moving staged to normal");
                File.Move(encrStagedFileName, encrFilePath);
            }
        }

        public static string Decrypt(string encrPath, string decrPath, string password, FileDecryptOptions options = null)
        {
            options = (options ?? new FileDecryptOptions()).Clone();
            return Decrypt(encrPath, decrPath, DerivedBytesProvider.FromPassword(password), options);
        }
        public static string Decrypt(string encrPath, string decrPath, DerivedBytesProvider derivedBytesProvider, FileDecryptOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(encrPath))
                throw new ArgumentNullException(nameof(encrPath));
            if (string.IsNullOrWhiteSpace(decrPath))
                throw new ArgumentNullException(nameof(decrPath));
            if (derivedBytesProvider == null)
                throw new ArgumentNullException(nameof(derivedBytesProvider));

            options = options ?? new FileDecryptOptions();

            using (FileStream inputStream = File.OpenRead(encrPath))
            using (HelixFileDecryptor decryptor = new HelixFileDecryptor(inputStream))
            {

                decryptor.Initialize(derivedBytesProvider);

                FileEntry header = decryptor.ReadHeader();

                options?.AfterMetadataRead?.Invoke(header, options);

                HeaderCorruptionException ex;
                if (!header.IsValid(out ex))
                    throw ex;

                string decrFullFileName = decrPath;
                string decrStagedFileName = decrPath + HelixConsts.StagedHxExtention;
                string decrBackupFileName = decrPath + HelixConsts.BackupExtention;

                if (header.EntryType == FileEntryType.File)
                {
                    if (!string.IsNullOrEmpty(Path.GetDirectoryName(decrStagedFileName)))
                        Directory.CreateDirectory(Path.GetDirectoryName(decrStagedFileName));

                    using (var contentStream = decryptor.GetContentStream())
                    using (var stagedFile = File.OpenWrite(decrStagedFileName))
                    {
                        contentStream.CopyTo(stagedFile);
                    }
                    File.SetLastWriteTimeUtc(decrStagedFileName, header.LastWriteTimeUtc);


                    if (File.Exists(decrFullFileName))
                        File.Move(decrFullFileName, decrBackupFileName);
                    else if (Directory.Exists(decrFullFileName))
                    {
                        if (Directory.GetFiles(decrFullFileName).Length > 0)
                                throw new IOException($"Unable to process entry, directory is not empty ({decrFullFileName})");
                        Directory.Move(decrFullFileName, decrBackupFileName);
                    }

                    File.Move(decrStagedFileName, decrFullFileName);

                    if (File.Exists(decrBackupFileName))
                    {
                        File.SetAttributes(decrBackupFileName, FileAttributes.Normal); //incase it was read only
                        File.Delete(decrBackupFileName);
                    }
                    else if (Directory.Exists(decrBackupFileName))
                        Directory.Delete(decrBackupFileName);
                }
                else if (header.EntryType == FileEntryType.Directory)
                {
                    if (File.Exists(decrFullFileName))
                        File.Move(decrFullFileName, decrBackupFileName);

                    if (Directory.Exists(decrFullFileName))
                    {
                        //If there is a case difference need to delete the directory
                        if (Path.GetFileName(decrFullFileName) != Path.GetFileName(new DirectoryInfo(decrFullFileName).Name))
                        {
                            if (Directory.GetFiles(decrFullFileName).Length > 0)
                                throw new IOException($"Unable to process entry, directory is not empty ({decrFullFileName})");
                            Directory.Move(decrFullFileName, decrBackupFileName);
                        }
                    }


                    Directory.CreateDirectory(decrFullFileName);
                }
                else //purge or delete
                {
                    if (File.Exists(decrFullFileName))
                        File.Move(decrFullFileName, decrBackupFileName);
                    else if (Directory.Exists(decrFullFileName)) 
                    {
                        if (Directory.GetFiles(decrFullFileName).Length > 0)
                            throw new IOException($"Unable to process entry, directory is not empty ({decrFullFileName})");
                        Directory.Move(decrFullFileName, decrBackupFileName);
                    }
                }

                if (File.Exists(decrBackupFileName))
                    File.Delete(decrBackupFileName);
                else if (Directory.Exists(decrBackupFileName))
                    Directory.Delete(decrBackupFileName);
            }
            return decrPath;
        }

        /// <summary>
        /// Returns the header for an encrypted file
        /// </summary>
        public static FileEntry DecryptHeader(string encrFile, DerivedBytesProvider derivedBytesProvider)
        {
            using (var inputStream = File.OpenRead(encrFile))
            using (var decryptor = new HelixFileDecryptor(inputStream))
            {

                try
                {
                    decryptor.Initialize(derivedBytesProvider);

                    FileEntry header = decryptor.ReadHeader();

                    HeaderCorruptionException ex;
                    if (!header.IsValid(out ex))
                        throw ex;

                    return header;
                }
                catch (FileCorruptionException ex)
                {
                    throw new FileCorruptionException($"Failed to decrypt {encrFile}, {ex.Message}", ex);
                }
                catch (AuthenticatedEncryptionException ex)
                {
                    throw new FileCorruptionException($"Failed to decrypt {encrFile}, {ex.Message}", ex);
                }
            }
        }
    }
}
