// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Runtime.Serialization;

namespace HelixSync
{
#if !NET_CORE
    [Serializable]
#endif
    internal class FileDesignatorException : AuthenticatedEncryptionException
    {
        public FileDesignatorException()
        {
        }

        public FileDesignatorException(string message) : base(message)
        {
        }

        public FileDesignatorException(string message, Exception innerException) : base(message, innerException)
        {
        }

#if !NET_CORE
        protected FileDesignatorException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif
    }
}