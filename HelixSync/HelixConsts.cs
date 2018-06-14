// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HelixSync
{
    public static class HelixConsts
    {
        public const int FileDesignatorSize = 8;
        public const int IVSize = 16;
        public const int HMACSaltSize = 16;
        
        public const string HeaderFileName = "helix.hx";
        
        public const string HxExtention = ".hx";
        public const string BackupExtention = ".~hx-bk";
        public const string StagedHxExtention = ".~hx-st";


        public const string SyncLogDirectory = ".helix";
        public const string SyncLogExtention = ".hx-decr";

        /// <summary>
        /// The begining text of the sync log
        /// </summary>
        public const string SyncLogHeader = "# HelixSync Log";
    }
}
