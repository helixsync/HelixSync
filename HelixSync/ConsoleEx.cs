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
        public VerbosityLevel Verbosity { get; set; }

        public ConsoleEx(VerbosityLevel verbosity = VerbosityLevel.Normal)
        {
            this.Verbosity = verbosity;
        }

        /// <summary>
        /// Indicates if the user has the ability to respond to console prompts
        /// </summary>
        public bool Interactive => Environment.UserInteractive && !Console.IsInputRedirected;

        public void Write(object value)
        {
            Console.Write(value);
        }

        public void WriteLine()
        {
            BeforeWriteLine?.Invoke(null);
            Console.WriteLine();
        }
        public void WriteLine(object value)
        {
            BeforeWriteLine?.Invoke(value);
            Console.WriteLine(value);
        }

        public void WriteErrorLine()
        {
            BeforeWriteErrorLine?.Invoke(null);
            Console.Error.WriteLine();
        }

        public void WriteErrorLine(object value)
        {
            BeforeWriteErrorLine?.Invoke(value);
            Console.Error.WriteLine(value);
        }

        public void WriteLine(VerbosityLevel level, int indent, object value)
        {
            if ((int)this.Verbosity >= (int)level)
            {
                BeforeWriteLine?.Invoke(value);
                Console.WriteLine($"{new string(' ', indent*2)}{value}");
            }
        }

        public Action<object> BeforeWriteLine { get; set; }
        public Action<object> BeforeWriteErrorLine { get; set; }

        /// <summary>
        /// Using the console prompts for a boolean (yes/no) value
        /// </summary>
        public bool PromptBool(string prompt, bool? defaultValue = null)
        {
            if (!Interactive)
                throw new InvalidOperationException("Unable to prompt in non-interactive console");

            while (true)
            {
                Console.Write(prompt);
                string value = Console.ReadLine() ?? "";
                value = value.ToUpper().Trim();
                if (value == "Y" || value == "YES" || value == "T" || value == "TRUE")
                    return true;
                if (value == "N" || value == "NO" || value == "F" || value == "FALSE")
                    return false;
                if (string.IsNullOrEmpty(value) && defaultValue != null)
                    return (bool)defaultValue;
            }
        }
    }
}
