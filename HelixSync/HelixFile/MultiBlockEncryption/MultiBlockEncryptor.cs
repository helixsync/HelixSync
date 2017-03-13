using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    class MultiBlockEncryptor : MultiBlockStreamWriter
    {
        public MultiBlockEncryptor(Stream streamOut, DerivedBytes derivedBytes, HelixFileVersion fileVersion)
            : base(new AuthenticatedEncryptor(streamOut, derivedBytes, fileVersion))
        {

        }

        /// <summary>
        /// Updates the underlying data source or repository with the current state of the buffer including the final encryption block then clears the buffer.
        /// </summary>
        public void FlushFinalBlock()
        {
            ((AuthenticatedEncryptor)streamOut).FlushFinalBlock();
        }

    }
}
