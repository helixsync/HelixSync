using System.ComponentModel;

namespace HelixSync.Commands
{
    public class CleanupOptions
    {

        [Argument(Recommended = true)]
        [Description("When set will display changes without making any")]
        public bool WhatIf { get; set; }

        [Argument(Recommended = true)]
        public VerbosityLevel Verbosity { get; set; }

        [Argument(Required = false, ShortName = "d", PreferShortName = true, ValuePlaceholder = "DecryptDirPath")]
        [Description("The path to the decrypted directory")]
        public string DecrDirectory { get; set; }

        [Argument(Required = false, ShortName = "e", PreferShortName = true, ValuePlaceholder = "EncryptDirPath")]
        [Description("The path to the encrypted directory")]
        public string EncrDirectory { get; set; }
    }
}