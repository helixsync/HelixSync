using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    class MultiBlockDecryptor : MultiBlockStreamReader
    {


        private MultiBlockDecryptor(AuthenticatedDecryptor decryptor)
            : base(decryptor)
        {
            this.Decryptor = decryptor;
        }

        public MultiBlockDecryptor(Stream streamIn)
            : this(new AuthenticatedDecryptor(streamIn))
        { }
        

        public DerivedBytes DerivedBytes
        {
            get { return Decryptor.DerivedBytes; }
        }
        public HelixFileVersion FileVersion
        {
            get { return Decryptor.FileVersion; }
        }

        AuthenticatedDecryptor Decryptor;


        public void Initialize(DerivedBytesProvider derivedBytesProvider, Action<Dictionary<string, byte[]>> afterHeaderRead = null)
        {
            Decryptor.Initialize(derivedBytesProvider, afterHeaderRead);
        }

    }
}
