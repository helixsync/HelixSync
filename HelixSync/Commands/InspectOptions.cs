// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.ComponentModel;

namespace HelixSync
{

    public class InspectOptions
    {
        public string Password { get; set; }
        public bool NonInteractive { get; set; }
        //public bool WhatIf { get; set; }

        //todo: implement the various content formats
        [DefaultValue("auto")]
        [Description(@"Describes how to format the content for inspection
  auto:   attempts to detect type automatically
  text:   text only
  binary: displays the hex values
  json:   adds indentation to json string
  none:   does not output any content")]
        public string ContentFormat { get; set; } = "auto";

        [Category("Position 0")]
        [Description("The file to be inspected")]
        public string File { get; set; }


        public string[] KeyFile { get; set; }
    }
}
