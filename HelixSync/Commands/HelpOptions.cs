// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.ComponentModel;

namespace HelixSync
{

    public class HelpOptions
    {
        [Argument(OrdinalPosition = 0)]
        [Description("Command to get help with, leave blank for a summery of all commands")]
        public string Command {get; set;}

        [Argument(Recommended = true, ShortName = "a")]
        [Description("Provides detailed usage information, includes all available arguments")]
        public bool Advanced {get; set;}
        public bool InvalidArguments;
    }
}
