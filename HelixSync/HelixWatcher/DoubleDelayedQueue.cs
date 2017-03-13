// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    class DoubleDelayedQueue : IDisposable
    {
        object DoubleDelayedQueueSyncObj = new object();
        HashSet<DirectoryChange> initialBuffer;
        BlockingCollection<DirectoryChange> Queue = new BlockingCollection<DirectoryChange>();

        public int FirstDelay { get; set; } = 3000;
        public int SecondDelay { get; set; } = 2000;
        private async Task DoubleDelayedTask()
        {
            await Task.Delay(FirstDelay); //collects tasks for 3 seconds
            IEnumerable<DirectoryChange> doubleDelayedQueue;
            lock (DoubleDelayedQueueSyncObj)
            {
                doubleDelayedQueue = initialBuffer;
                initialBuffer = null;
            }

            await Task.Delay(SecondDelay); //wait 2 seconds (to enable the any applications to release their locks)
            foreach (var item in doubleDelayedQueue)
                Queue.Add(item);
        }

        private void Enqueue(DirectoryChange item)
        {
            lock (DoubleDelayedQueueSyncObj)
            {
                if (initialBuffer == null)
                {
                    initialBuffer = new HashSet<DirectoryChange>();
                    Task.Run(DoubleDelayedTask);
                }

                initialBuffer.Add(item);
            }
        }
        private void EnqueueImediately(DirectoryChange item)
        {
            Queue.Add(item);
        }

        public IEnumerable<DirectoryChange> GetConsumingEnumerable()
        {
            return Queue.GetConsumingEnumerable();
        }

        public void Dispose()
        {
            Queue.Dispose();
        }
    }
}
