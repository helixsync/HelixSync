using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace HelixSync.FileSystem
{
    public class FSEntryCollection : KeyedCollection<string, FSEntry>
    {
        public FSEntryCollection(bool caseSensitive)
            : base(caseSensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
        {

        }
        protected override string GetKeyForItem(FSEntry item)
        {
            return item.Name;
        }
    }
}
