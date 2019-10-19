using System;
using System.Runtime.Serialization;

namespace HelixSync
{
    public class UnsupportedFileTypeException : HelixException
    {
        public UnsupportedFileTypeException()
        {
        }

        public UnsupportedFileTypeException(string message) : base(message)
        {
        }

        public UnsupportedFileTypeException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}