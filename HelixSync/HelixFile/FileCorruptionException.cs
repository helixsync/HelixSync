using System;
using System.Runtime.Serialization;

namespace HelixSync
{
#if !NET_CORE
    [Serializable]
#endif
    public class FileCorruptionException : HelixException
    {
        public FileCorruptionException()
        {
        }

        public FileCorruptionException(string message) : base(message)
        {
        }

        public FileCorruptionException(string message, Exception innerException) : base(message, innerException)
        {
        }

#if !NET_CORE
        protected FileCorruptionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif
    }
}