// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    public class ConsoleEx
    {
        public void WriteLine()
        {
            Console.WriteLine();
        }
        public void WriteLine(object value)
        {
            BeforeWriteLine?.Invoke(value);
            Console.WriteLine(value);
        }

        public Action<object> BeforeWriteLine { get; set; }

        /// <summary>
        /// Using the console prompts for a boolean (yes/no) value
        /// </summary>
        public bool PromptBool(string prompt, bool? defaultValue = null)
        {
            while (true)
            {
                Console.Write(prompt);
                string value = Console.ReadLine() ?? "";
                value = value.ToUpper().Trim();
                if (value == "Y" || value == "YES" || value=="T" || value == "TRUE")
                    return true;
                if (value == "N" || value == "NO" || value == "F" || value == "FALSE")
                    return false;
                if (string.IsNullOrEmpty(value) && defaultValue != null)
                    return (bool)defaultValue;
            }
        }
    }
}
