using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HelixSync.FileSystem;

namespace HelixSync.HelixDirectory
{
    static class HelixDirectoryUtil
    {
        public static void Cleanup(this FSDirectory directory, ConsoleEx console)
        {
            console = console ?? new ConsoleEx();

            console.WriteLine(VerbosityLevel.Normal, 0, "Performing Cleanup on Encr Directory...");

            console.WriteLine(VerbosityLevel.Detailed, 1, directory.FullName);
            console.WriteLine(VerbosityLevel.Detailed, 1, $"Removing staged files ({Path.ChangeExtension("*.*", HelixConsts.StagedHxExtention)})");
            foreach (FSFile file in directory.GetEntries(SearchOption.AllDirectories)
                                        .ToArray()
                                        .OfType<FSFile>()
                                        .Where(f => string.Equals(Path.GetExtension(f.FullName), HelixConsts.StagedHxExtention)))
            {
                console.WriteLine(VerbosityLevel.Detailed, 2, $"Removing staged file {file.FullName}");
                file.Delete();
            }


            console.WriteLine(VerbosityLevel.Detailed, 1, $"Reverting incomplete ({Path.ChangeExtension("*.*", HelixConsts.BackupExtention)})");
            foreach (FSFile file in directory.GetEntries(SearchOption.AllDirectories)
                                        .ToArray()
                                        .OfType<FSFile>()
                                        .Where(f => string.Equals(Path.GetExtension(f.FullName), HelixConsts.BackupExtention)))
            {
                var destination = Path.ChangeExtension(file.FullName, "");
                console.WriteLine(VerbosityLevel.Detailed, 3, $"Incomplete file, restoring backup {file.FullName} => {destination}");
                if (directory.Exists(destination))
                {
                    console.WriteLine(VerbosityLevel.Diagnostic, 4, $"Removing {destination}");
                    (directory.TryGetEntry(destination) as FSFile).Delete();
                }

                console.WriteLine(VerbosityLevel.Diagnostic, 4, $"Renaming {Path.GetFileName(file.FullName)} to {Path.GetFileName(destination)}");
                file.MoveTo(destination);
            }

            console.WriteLine(VerbosityLevel.Detailed, 1, "Cleanup Complete");
        }
    }
}
