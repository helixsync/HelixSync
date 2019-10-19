// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Runtime.Serialization;

namespace HelixSync
{
    class AuthenticatedEncryptionException : Exception
    {
        public AuthenticatedEncryptionException()
        {
        }

        public AuthenticatedEncryptionException(string message) : base(message)
        {
        }

        public AuthenticatedEncryptionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}