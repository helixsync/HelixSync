using System;
using System.Runtime.Serialization;

namespace HelixSync
{
    public class ArgumentParseException : Exception
    {
        public ArgumentParseException()
        {
        }

        public ArgumentParseException(string message) : base(message)
        {
        }

        public ArgumentParseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}