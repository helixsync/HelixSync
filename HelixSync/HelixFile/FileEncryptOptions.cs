using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    public class FileEncryptOptions
    {
        public Action<FileEntry> BeforeWriteHeader { get; set; }
        public Logger Log { get; set; }
        public HelixFileVersion FileVersion { get; set; }
        public string StoredFileName { get; set; }
    }
}
