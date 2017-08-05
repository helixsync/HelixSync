// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Text;

namespace HelixSync
{
    public class SyncResults
    {
        private SyncResults()
        {

        }

        public SyncStatus SyncStatus { get; private set; }
        public Exception Exception { get; private set; }

        public static SyncResults Failure(Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            return new SyncResults()
            {
                SyncStatus = SyncStatus.Failure,
                Exception = exception,
            };
        }

        public static SyncResults Success()
        {
            return new SyncResults()
            {
                SyncStatus = SyncStatus.Success,
            };
        }

        public static SyncResults Skipped()
        {
            return new SyncResults()
            {
                SyncStatus = SyncStatus.Skipped
            };
        }
    }
}
