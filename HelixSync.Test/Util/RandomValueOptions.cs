// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;

namespace HelixSync.Test
{
    [Flags]
    public enum RandomValueOptions
    {
        Default = 0,
        NotNull = 1,
        NotEmpty = 2,
        NotNullOrEmpty = NotNull | NotEmpty,
    }
}

