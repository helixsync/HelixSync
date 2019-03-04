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
            return typeof(Program).GetTypeInfo().Assembly
                .GetCustomAttributes(typeof(AssemblyCopyrightAttribute))
                .OfType<AssemblyCopyrightAttribute>()
                .FirstOrDefault()?.Copyright;
        }

        private static string GetTitle()
        {
            return typeof(Program).GetTypeInfo().Assembly
                .GetCustomAttributes(typeof(AssemblyTitleAttribute))
                .OfType<AssemblyTitleAttribute>()
                .FirstOrDefault()?.Title;
        }
        private static string GetVersion()
        {
            return typeof(Program).GetTypeInfo()
                .Assembly
                .GetName()
                .Version.ToString();
        }
        static int Main(string[] args)
        {
            //                 -----------------------------------------------------------------------------80
            Console.WriteLine("");
            Console.WriteLine("{0} v{1}         ::  {2}", 
                GetTitle(), 
                GetVersion(),
                (GetCopyright() ?? "").Replace("©", "(c)"));
            Console.WriteLine();
            Console.WriteLine("This program comes with ABSOLUTELY NO WARRANTY; Read LICENCE for details");
            Console.WriteLine("This is free software and can redistribute it under GPLv3 terms");
            Console.WriteLine();
            Console.WriteLine("** You are running and ALPHA version of this software ** ");
            Console.WriteLine("** Extreme caution should be taken when using this software **");
            Console.WriteLine("** ALWAYS backup your data before using **");
            Console.WriteLine();

            return CommandProvider.Invoke(args);
        }
   }
}
