using System;
using System.Runtime.Serialization;

namespace HelixSync
{
    [Serializable]
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

        protected UnsupportedFileTypeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}