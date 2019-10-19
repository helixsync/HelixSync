using System;
using System.Runtime.Serialization;

namespace HelixSync
{
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
    }
}