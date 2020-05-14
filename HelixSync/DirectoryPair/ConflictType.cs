using System;
using System.Collections.Generic;
using System.Text;

namespace HelixSync
{
    public enum ConflictType
    {
        BothSidesChanged,
        NonEmptyFolder,
        UnexpectedPurge
    }
}
