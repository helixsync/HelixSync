using System;
using System.Runtime.Serialization;

namespace HelixSync
{
#if !NET_CORE
    [Serializable]
#endif
    public class InvalidPasswordException : HelixException
    {
        public InvalidPasswordException()
        {
        }

        public InvalidPasswordException(string message) : base(message)
        {
        }

        public InvalidPasswordException(string message, Exception innerException) : base(message, innerException)
        {
        }
#if !NET_CORE
        protected InvalidPasswordException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif
    }
}