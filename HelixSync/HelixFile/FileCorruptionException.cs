using System;
using System.Runtime.Serialization;

namespace HelixSync
{
    [Serializable]
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

        protected FileCorruptionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}