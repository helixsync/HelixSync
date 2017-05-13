using System;
using System.Runtime.Serialization;

namespace HelixSync
{
#if !NET_CORE
    [Serializable]
#endif
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

#if !NET_CORE
        protected ArgumentParseException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif
    }
}