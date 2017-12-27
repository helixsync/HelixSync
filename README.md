# HelixSync

HelixSync is a tool that allows you to encrypt an entire directory and incrementally sync changes. This software has been developed out of a desire to add an additional layer of privacy and security to cloud storage providers (Dropbox, Google Drive, ...). It has been designed to work well with these providers by providing file by file incremental encryption.

**The software is still in alpha, extreme caution should be taken when using this software. ALWAYS backup your data before using this software**

## Getting Started

To run the project from the latest source install [.NET Core 2.0](https://www.microsoft.com/net/core) and [GIT](https://git-scm.com/) run the following commands.

```bash
git clone https://github.com/helixsync/HelixSync.git HelixSync
dotnet run --project "HelixSync/HelixSync" -- sync "DecryptedFolder" "EncryptedFolder" -password "secret"
```

## Language and Platforms

The software has been developed using C# .NET for [.NET Core 2.0](https://www.microsoft.com/net/core). It should be able to run on any platform supported by this framework including Windows, Linux and Mac.

## Roadmap

- Add continual sync through directory monitoring
- Add a GUI and tray icon (with continual sync)

## License

HelixSync is released as Open Source software using the [GPLv3](https://www.gnu.org/licenses/gpl.html)