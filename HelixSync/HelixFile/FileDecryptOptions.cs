using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    public class FileDecryptOptions
    {
        public Action<FileEntry, FileDecryptOptions> AfterMetadataRead { get; set; }

        public FileDecryptOptions Clone()
        {
            return this.MemberwiseClone() as FileDecryptOptions;
        }
    }
}
