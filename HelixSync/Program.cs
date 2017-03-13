// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Reflection;
using System.Linq;

namespace HelixSync
{
    static class Program
    {
        private static string GetCopyright()
        {
            return Assembly.GetExecutingAssembly()
                .GetCustomAttributes(false)
                .OfType<AssemblyCopyrightAttribute>()
                .FirstOrDefault()
                ?.Copyright;
        }

        private static string GetTitle()
        {
            return Assembly.GetExecutingAssembly()
                .GetCustomAttributes(false)
                .OfType<AssemblyTitleAttribute>()
                .FirstOrDefault()
                ?.Title;
        }
        private static string GetVersion()
        {
            return Assembly.GetExecutingAssembly()
                .GetCustomAttributes(false)
                .OfType<AssemblyVersionAttribute>()
                .FirstOrDefault()
                ?.Version;
        }
        static int Main(string[] args)
        {
            //                 -----------------------------------------------------------------------------80
            Console.WriteLine("{0} v{1}         ::  {2}", 
                GetTitle(), 
                typeof(Program).Assembly.GetName().Version,
                GetCopyright().Replace("©", "(c)"));
            Console.WriteLine();
            Console.WriteLine("This program comes with ABSOLUTELY NO WARRANTY; Read LICENCE for details");
            Console.WriteLine("This is free software and can redistribute it under GPLv3 terms");
            Console.WriteLine();
            Console.WriteLine("** You are running and ALPHA version of this software ** ");
            Console.WriteLine("** Extreme caution should be taken when using this software **");
            Console.WriteLine("** ALWAYS backup your data before using **");
            Console.WriteLine();

            if (string.Equals(args[0], "inspect", StringComparison.OrdinalIgnoreCase))
            {
                var options = new InspectOptions();
                ArgumentParser.ParseCommandLineArguments(options, args, 1);
                InspectCommand.Inspect(options);
                return 0;
            }
            else if (string.Equals(args[0], "sync", StringComparison.OrdinalIgnoreCase))
            {
                SyncOptions options = new SyncOptions();
                ArgumentParser.ParseCommandLineArguments(options, args, 1);
                SyncCommand.Sync(options);
                return 0;
            }
            else
            {
                Console.WriteLine("Invalid arguments");
                return -1;
            }
        }
   }
}
