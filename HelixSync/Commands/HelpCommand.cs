// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Linq;

namespace HelixSync
{
    class HelpCommand
    {
        public static int Help(HelpOptions options, ConsoleEx consoleEx = null)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            consoleEx = consoleEx ?? new ConsoleEx();

            if (string.IsNullOrEmpty(options.Command)) 
            {
                consoleEx.WriteLine("= Commands =");
                foreach(var command in CommandProvider.Commands)
                {
                    consoleEx.WriteLine(ArgumentParser.UsageLine(command.optionType, "helixsync " + command.name));
                }
            }
            else 
            {
                var command = CommandProvider.Commands.FirstOrDefault(c => string.Equals(options.Command, c.name, StringComparison.OrdinalIgnoreCase));
                if (command.name == null)
                    throw new Exception($"Invalid command {options.Command}");

                consoleEx.WriteLine(ArgumentParser.UsageString(command.optionType, "helixsync " + command.name, options.Advanced));

                if (!options.Advanced) 
                {
                    consoleEx.WriteLine();
                    consoleEx.WriteLine("*Additional options are available using the -Advanced switch");
                }
            }

            consoleEx.WriteLine();

            if (options.InvalidArguments)
                return -1;
            else 
                return 0;
        }
    }
}