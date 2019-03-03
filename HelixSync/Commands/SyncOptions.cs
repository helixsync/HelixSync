// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using HelixSync.Commands;

namespace HelixSync
{
    public class SyncOptions
    {
        public SyncOptions()
        {
        }

        [Argument(Required = true, ShortName="d", PreferShortName = true, ValuePlaceholder="DecryptDirPath")]
        [Description("The path to the decrypted directory")]
        public string DecrDirectory { get; set; }

        [Argument(Required = true, ShortName = "e", PreferShortName = true, ValuePlaceholder="EncryptDirPath")]
        [Description("The path to the encrypted directory")]
        public string EncrDirectory { get; set; }

        [DefaultValue("")]
        public string Password { get; set; }

        [Argument(Recommended = true)]
        [Description("When set will display changes without making any")]
        public bool WhatIf { get; set; }

        [Description("Initialize repository without prompt")]
        public bool Initialize { get; set;}

        [Description("Path to a key file. Allowed multiple values")]
        public string[] KeyFile { get; set; }

        [Argument(Recommended=true)]
        [Description("Sets the detail level for logging messages")]
        public VerbosityLevel Verbosity {get; set;}

        //todo: implement Direction
        //[Argument(Recommended = true)]
        //[Description("Bidirectionaly syncs two ways, otherwise forces a one way sync")]
        //public SyncDirection Direction { get; set; } = SyncDirection.Bidirectional;

        public override string ToString()
        {
            return string.Format("[SyncOptions: DecrDirectory={0}, EncrDirectory={1}, Password={2}]", DecrDirectory, EncrDirectory, Password);
        }
    }
}

