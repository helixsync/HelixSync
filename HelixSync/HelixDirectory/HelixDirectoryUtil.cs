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

            console.WriteLine(VerbosityLevel.Normal, 0, $"Performing cleanup on {directory.FullName}...");

            //Staged Files
            console.WriteLine(VerbosityLevel.Detailed, 1, directory.FullName);
            console.WriteLine(VerbosityLevel.Detailed, 1, $"Removing staged files ({Path.ChangeExtension("*.*", HelixConsts.StagedHxExtention)})");
            bool foundFile = false;
            foreach (FSFile file in directory.GetEntries(SearchOption.AllDirectories)
                                        .ToArray()
                                        .OfType<FSFile>()
                                        .Where(f => string.Equals(Path.GetExtension(f.FullName), HelixConsts.StagedHxExtention)))
            {
                foundFile = true;
                console.WriteLine(VerbosityLevel.Detailed, 2, $"Removing staged file {file.RelativePath}");
                file.Delete();
            }
            if (!foundFile)
                console.WriteLine(VerbosityLevel.Detailed, 2, $"No files to cleanup");

            //Backup Files
            foundFile = false;
            console.WriteLine(VerbosityLevel.Detailed, 1, $"Reverting incomplete ({Path.ChangeExtension("*.*", HelixConsts.BackupExtention)})");
            foreach (FSFile file in directory.GetEntries(SearchOption.AllDirectories)
                                        .ToArray()
                                        .OfType<FSFile>()
                                        .Where(f => string.Equals(Path.GetExtension(f.FullName), HelixConsts.BackupExtention)))
            {
                var destinationRelativePath = Path.ChangeExtension(file.RelativePath, null);

                console.WriteLine(VerbosityLevel.Detailed, 3, $"Incomplete file, restoring backup {file.RelativePath} => {destinationRelativePath}");
                if (directory.ChildExists(destinationRelativePath))
                {
                    console.WriteLine(VerbosityLevel.Diagnostic, 4, $"Removing {destinationRelativePath}");
                    (directory.TryGetEntry(destinationRelativePath) as FSFile).Delete();
                }

                console.WriteLine(VerbosityLevel.Diagnostic, 4, $"Renaming {Path.GetFileName(file.RelativePath)} to {Path.GetFileName(destinationRelativePath)}");
                file.MoveTo(destinationRelativePath);
            }
            if (!foundFile)
                console.WriteLine(VerbosityLevel.Detailed, 2, $"No files to cleanup");


            console.WriteLine(VerbosityLevel.Detailed, 1, "Cleanup Complete");
        }
    }
}
