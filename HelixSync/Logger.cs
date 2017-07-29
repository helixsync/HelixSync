// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    public class Logger : ILogger
    {
        public void Log(LogLevel level, string message, object context = null)
        {

        }

        public void Trace(string message, object context = null)
        {
            Log(LogLevel.Critical, new EventId(), message, null, (s, e) => s);
        }
        public void Debug(string message, object context = null)
        {
            Log(LogLevel.Critical, new EventId(), message, null, (s, e) => s);
        }
        public void Information(string message, object context = null)
        {
            Log(LogLevel.Critical, new EventId(), message, null, (s, e) => s);
        }
        public void Warning(string message, object context = null)
        {
            Log(LogLevel.Critical, new EventId(), message, null, (s, e) => s);
        }
        public void Error(string message, object context = null)
        {
            Log(LogLevel.Critical, new EventId(), message, null, (s, e) => s);
        }
        public void Critical(string message)
        {
            Log(LogLevel.Critical, new EventId(), message, null, (s, e) => s);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            //todo: write to console?
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }
    }
}
