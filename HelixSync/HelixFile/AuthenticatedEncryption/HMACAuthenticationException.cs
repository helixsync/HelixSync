// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Runtime.Serialization;

namespace HelixSync
{
    internal class HMACAuthenticationException : AuthenticatedEncryptionException
    {
        public HMACAuthenticationException()
        {
        }

        public HMACAuthenticationException(string message) : base(message)
        {
        }

        public HMACAuthenticationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}