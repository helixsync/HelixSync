// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.ComponentModel;

namespace HelixSync
{
    public class SyncOptions
    {
        public SyncOptions()
        {
        }

        [Category("Position 0")]
        public string DecrDirectory { get; set; }

        [Category("Position 1")]
        public string EncrDirectory { get; set; }

        [DefaultValue("")]
        public string Password { get; set; }

        [Description("When set will display changes without making any")]
        public bool WhatIf { get; set; }

        [Description("Initialize repository without prompt")]
        public bool Initialize { get; set;}

        public string[] KeyFile { get; set; }

        public override string ToString()
        {
            return string.Format("[SyncOptions: DecrDirectory={0}, EncrDirectory={1}, Password={2}]", DecrDirectory, EncrDirectory, Password);
        }
    }
}

