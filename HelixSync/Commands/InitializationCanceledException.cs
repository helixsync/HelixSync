// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;

namespace HelixSync
{
    public class InitializationCanceledException : Exception
    {
        public InitializationCanceledException() : 
            base ("Initialization Canceled")
        {
        }
    }
}