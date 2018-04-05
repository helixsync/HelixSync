using System;
using System.Collections.Generic;
using System.Text;

namespace HelixSync.FileSystem
{
    public interface IFSDirectoryCore
    {
        void Remove(FSEntry entry);
        void Add(FSEntry entry);
    }
}
