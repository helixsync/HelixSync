using System;

namespace HelixSync
{
    public class ArgumentAttribute : Attribute
    {
        public int OrdinalPosition { get; set; } = int.MaxValue;
        public string ShortName { get; set; }
        public bool Required { get; set; }

        /// <summary>
        /// Used in the help screen to determine if it will include on the one line summary help
        /// </summary>
        public bool Recommended { get; set; }

        /// <summary>
        /// Used in the help screen as the value placeholder 
        /// </summary>
        public string ValuePlaceholder { get; set; }

        /// <summary>
        /// Used in the help screen to determine if the short name is shown in the one line summary
        /// </summary>
        public bool PreferShortName { get; set; }

        /// <summary>
        /// Used in the help screen to determine if only displayed when the -Advanced switch is used
        /// </summary>
        public bool Advanced { get; set; } 
    }
}