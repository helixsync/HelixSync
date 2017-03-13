using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    internal class HMACException : Exception
    {
        public HMACException()
            : base()
        { }
        public HMACException(string message)
            : base(message)
        { }
    }
}
