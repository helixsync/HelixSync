# Security of HelixSync

The application was developed using the best practices and with a high focus on security and privacy. The sections below provide the technical details for the underlying encryption and security of the application.

***The software is still in alpha, extreme caution should be taken when using this software. ALWAYS backup your data before using this software***

## Encryption

Files are encrypted using the widely accepted algorithms and methodologies to provide utmost security. The weakness section covers information about possible attack vectors.

Property                    | Value
---                         | ---
Encryption Algorithm        | AES256
Authentication Composition  | Encrypt-then-MAC (EtM)
Authentication Function     | HMACSHA256
Password and Key File [Derivation Function](https://en.wikipedia.org/wiki/Key_derivation_function) | PBKDF2, 100 000 Iterations (Rfc2898DeriveBytes)
File Name Encoding Function | HMACSHA256

Encryption and random data generators are done using the native .NET Core libraries. A single directory shares a common derived password and associated salt. Each file received a unique 128 HMAC salt for HMAC and 128 IV for AES.

## Key Derivation Mixing

Directory can be encrypted by a mix of password and multiple files. When this is done the password and each file has the SHA256 function done on them then sorted and rehashed. They are done separately and resorted to allow differing order of files.

## File Name Encoding

File names are encoded using a hash function with a randomized salt. Because this is done no information is reveled from the file names themselves while having a consistent name for each file.

Having a consistent name for each file allows the benefits of cloud storage functionality such as conflict detection and versioning.

## Weaknesses

Bellow are some of the known weakness of this application. It is not exhaustive and attacks are always improving. Most of these weakness represent minimal to no impact to typical use of the application.

### Common To All Encryption

- Week Passwords/Brute Force: Use of a week password greatly reduces the protection provided by the application. An adversary who has access to the encrypted data can perform an offline brute force attack guessing a multitude of passwords
- Malware, Key loggers, Loss of Control: Any malware or hardware alterations can expose keystrokes or memory which can gain an adversary access to the decrypted data

### Theoretical Attacks

- Adversaries that have over time information or order information may gain insights about the types of files or the applications that modify them as they both may have patterns of use
- Adversaries may be able to correlate known directories by analyzing file sizes. (i.e. may be able to detect you have synchronized your windows directory because they know the file sizes of all the files in it)
