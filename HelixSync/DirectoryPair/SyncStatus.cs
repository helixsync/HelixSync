// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    public enum SyncStatus
    {
        Success = 0,
        Skipped = 1,
        Failure = 2,
        Repaired = 3,
    }
}
