using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    public enum PreSyncMode
    {
        Unknown,

        Unchanged, //No change
        Match, //One or more sides changed however they now match
        Conflict, //Change from both sides

        EncryptedSide,
        DecryptedSide,
    }
}
