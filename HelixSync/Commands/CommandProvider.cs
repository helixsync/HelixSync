// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Linq;

namespace HelixSync
{
    class CommandProvider
    {
        public static List<(string name, Type optionType, Func<object, ConsoleEx, int> invoker)> Commands = new List<(string name, Type optionType, Func<object, ConsoleEx, int> invoker)> {
            ("help", typeof(HelpOptions), (o, c) => HelpCommand.Help((HelpOptions)o, c)),
            ("sync", typeof(SyncOptions),  (o, c) => SyncCommand.Sync((SyncOptions)o, c)),
            ("inspect", typeof(InspectOptions),  (o, c) => InspectCommand.Inspect((InspectOptions)o, c)),
        };

        public static int Invoke(string[] args, ConsoleEx consoleEx = null)
        {
            consoleEx = consoleEx ?? new ConsoleEx();

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
            var command = Commands.FirstOrDefault(c => string.Equals(commandName,  c.name, StringComparison.OrdinalIgnoreCase));

            if (command.optionType == null) 
            {
                consoleEx.WriteLine($"Invalid command '{commandName}'");
                consoleEx.WriteLine();
                consoleEx.WriteLine("For information on commands run");
                consoleEx.WriteLine("helixsync help");
                consoleEx.WriteLine();
                return -1;
            }

            var options = Activator.CreateInstance(command.optionType);
            ArgumentParser.ParseCommandLineArguments(options, args, 1);
            return command.invoker(options, consoleEx);
        }        

    }
}