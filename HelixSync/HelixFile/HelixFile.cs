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

            CleanupFile(encrFilePath, options.Log);

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
                options.Log?.Debug("Writing: Header");
                options.BeforeWriteHeader?.Invoke(header);
                encryptor.WriteHeader(header);

                options.Log?.Debug("Writing: Content");
                encryptor.WriteContent(streamIn, streamIn.Length);
            }


            if (File.Exists(encrFilePath))
            {
                if (File.Exists(encrBackupFileName))
                {
                    options.Log?.Debug("Destaging: Removing incomplete backup");
                    File.Delete(encrBackupFileName);
                }

                options.Log?.Debug("Destaging: Moving staged to normal");
                
                if (File.Exists(encrFilePath))
                    File.Move(encrFilePath, encrBackupFileName);
                File.Move(encrStagedFileName, encrFilePath);
                
                options.Log?.Debug("Destaging: Removing backup");
                File.Delete(encrBackupFileName);
            }
            else
            {
                options.Log?.Debug("Destaging: Moving staged to normal");
                File.Move(encrStagedFileName, encrFilePath);
            }
        }

        public static string Decrypt(string encrPath, string decrPath, string password, FileDecryptOptions options = null)
        {
            options = (options ?? new FileDecryptOptions()).Clone();
            return Decrypt(encrPath, decrPath, DerivedBytesProvider.FromPassword(password),  options);
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

            CleanupFile(encrPath, options.Log);
            CleanupFile(decrPath, options.Log);

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
                        Directory.Move(decrFullFileName, decrBackupFileName);

                    File.Move(decrStagedFileName, decrFullFileName);

                    if (File.Exists(decrBackupFileName))
                        File.Delete(decrBackupFileName);
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
                            Directory.Move(decrFullFileName, decrBackupFileName);
                    }


                    Directory.CreateDirectory(decrFullFileName);
                }
                else //purge or delete
                {
                    if (File.Exists(decrFullFileName))
                        File.Move(decrFullFileName, decrBackupFileName);
                    else if (Directory.Exists(decrFullFileName))
                        Directory.Move(decrFullFileName, decrBackupFileName);
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

                try {
                    decryptor.Initialize(derivedBytesProvider);

                    FileEntry header = decryptor.ReadHeader();

                    HeaderCorruptionException ex;
                    if (!header.IsValid(out ex))
                        throw ex;

                    return header;
                } catch(FileCorruptionException ex){
                    throw new FileCorruptionException($"Failed to decrypt {encrFile}, {ex.Message}", ex);
                } catch(AuthenticatedEncryptionException ex){
                    throw new FileCorruptionException($"Failed to decrypt {encrFile}, {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Cleans up staging, backup files
        /// </summary>
        public static void CleanupFile(string filePath, Logger log = null)
        {
            if (filePath.EndsWith(HelixConsts.BackupExtention, StringComparison.OrdinalIgnoreCase))
                filePath = filePath.Substring(0, filePath.Length - HelixConsts.BackupExtention.Length);
            else if (filePath.EndsWith(HelixConsts.StagedHxExtention, StringComparison.OrdinalIgnoreCase))
                filePath = filePath.Substring(0, filePath.Length - HelixConsts.StagedHxExtention.Length);

            var stagedFileName = filePath + HelixConsts.StagedHxExtention;
            var backupFileName = filePath + HelixConsts.BackupExtention;

            if (File.Exists(stagedFileName))
            {
                log?.Debug("Cleanup: Removing incomplete staging file");
                File.Delete(stagedFileName);
            }

            if (File.Exists(filePath))
            {
                if (File.Exists(backupFileName))
                    File.Delete(backupFileName);
            }
            else if (File.Exists(backupFileName))
            {
                File.Move(backupFileName, filePath);
            }

        }
    }
}
