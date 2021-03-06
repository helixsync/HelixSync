// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Linq;
using HelixSync.Commands;

namespace HelixSync
{
    class CommandProvider
    {
        public static List<(string name, Type optionType, Func<object, ConsoleEx, int> invoker)> Commands = new List<(string name, Type optionType, Func<object, ConsoleEx, int> invoker)> {
            ("help", typeof(HelpOptions), (o, c) => HelpCommand.Help((HelpOptions)o, c)),
            ("sync", typeof(SyncOptions),  (o, c) => SyncCommand.Sync((SyncOptions)o, c).Error != null ? -1 : 0),
            ("inspect", typeof(InspectOptions),  (o, c) => InspectCommand.Inspect((InspectOptions)o, c)),
            ("cleanup", typeof(CleanupOptions), (o, c) => CleanupCommand.Cleanup((CleanupOptions)o, c)),
        };

        public static int Invoke(string[] args, ConsoleEx consoleEx = null)
        {
            consoleEx ??= new ConsoleEx();

            if (args.Length <= 0) 
            {
                consoleEx.WriteLine("No command specified");
                consoleEx.WriteLine();
                consoleEx.WriteLine("For information on commands run");
                consoleEx.WriteLine("helixsync help");
                consoleEx.WriteLine();
                return -1;
            }

            var commandName = args[0];
            var (name, optionType, invoker) = Commands.FirstOrDefault(c => string.Equals(commandName,  c.name, StringComparison.OrdinalIgnoreCase));

            if (optionType == null) 
            {
                consoleEx.WriteLine($"Invalid command '{commandName}'");
                consoleEx.WriteLine();
                consoleEx.WriteLine("For information on commands run");
                consoleEx.WriteLine("helixsync help");
                consoleEx.WriteLine();
                return -1;
            }

            var options = Activator.CreateInstance(optionType);
            ArgumentParser.ParseCommandLineArguments(options, args, 1);
            return invoker(options, consoleEx);
        }        

    }
}