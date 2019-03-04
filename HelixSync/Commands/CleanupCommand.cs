using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HelixSync.FileSystem;
using HelixSync.HelixDirectory;

namespace HelixSync.Commands
{
    public static class CleanupCommand
    {
        public static int Cleanup(CleanupOptions options, ConsoleEx consoleEx = null, HelixFileVersion fileVersion = null)
        {
            consoleEx = consoleEx ?? new ConsoleEx(options.Verbosity);
            consoleEx.WriteLine("------------------------");
            consoleEx.WriteLine("-- HelixSync " + typeof(SyncCommand).GetTypeInfo().Assembly.GetName().Version.ToString());
            consoleEx.WriteLine("------------------------");
            consoleEx.WriteLine();

            consoleEx.WriteLine("Cleanup");
            if (options.DecrDirectory != null)
                consoleEx.WriteLine("..DecrDir: " + options.DecrDirectory);
            if (options.EncrDirectory != null)
                consoleEx.WriteLine("..EncrDir: " + options.EncrDirectory);
            if (options.WhatIf)
                consoleEx.WriteLine("..Options: WhatIf");
            consoleEx.WriteLine();

            if (options.WhatIf)
                consoleEx.WriteLine("** WhatIf Mode - No Changes Made **");


            if (options.EncrDirectory != null)
            {
                FSDirectory encrDirectory = new FSDirectory(options.EncrDirectory, options.WhatIf);
                encrDirectory.Cleanup(consoleEx);
            }

            if (options.DecrDirectory != null)
            {
                FSDirectory encrDirectory = new FSDirectory(options.EncrDirectory, options.WhatIf);
                encrDirectory.Cleanup(consoleEx);
            }

            return 0;
        }
    }
}
