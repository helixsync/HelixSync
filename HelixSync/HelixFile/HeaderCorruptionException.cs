using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    public class HeaderCorruptionException : FileCorruptionException
    {
        public HeaderCorruptionException(string message) : base(message)
        {
        }
    }
}
