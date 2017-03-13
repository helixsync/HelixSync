// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    public class HelixException : Exception
    {
        public HelixException()
        {
        }

        public HelixException(string message) : base(message)
        {
        }

        public HelixException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected HelixException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
