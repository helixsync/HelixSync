using System;
using System.Runtime.Serialization;

namespace HelixSync
{
#if !NET_CORE
    [Serializable]
#endif
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

#if !NET_CORE
        protected UnsupportedFileTypeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif
    }
}