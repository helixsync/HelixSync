// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    public class Logger
    {
        //Based of NLog log levels
        //https://github.com/NLog/NLog/wiki/Configuration-file#log-levels
        public enum LogLevel
        {
            Trace,
            Debug,
            Info,
            Warn,
            Error,
            Fatal,
        }

        public class LogArgs : EventArgs
        {
            LogLevel level { get; set; }
            string Message { get; set; }
            object Context { get; set; }

        }

        public void Log(LogLevel level, string message, object context = null)
        {

        }

        public void Trace(string message, object context = null)
        {
            Log(LogLevel.Trace, message, context);
        }
        public void Debug(string message, object context = null)
        {
            Log(LogLevel.Debug, message, context);
        }
        public void Info(string message, object context = null)
        {
            Log(LogLevel.Info, message, context);
        }
        public void Warn(string message, object context = null)
        {
            Log(LogLevel.Warn, message, context);
        }
        public void Error(string message, object context = null)
        {
            Log(LogLevel.Error, message, context);
        }
        public void Fatal(string message, object context = null)
        {
            Log(LogLevel.Fatal, message, context);
        }
    }
}
