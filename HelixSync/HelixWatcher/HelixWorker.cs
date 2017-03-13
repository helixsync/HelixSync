// This file is part of HelixSync, which is released under GPL-3.0 see
// the included LICENSE file for full details

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelixSync
{
    class HelixWorker
    {
        object syncObject = new object();
        List<Task> retryingTasks = new List<Task>();
        HashSet<DirectoryChange> retryingDirectories = new HashSet<DirectoryChange>();

        public void Start()
        {
            DoubleDelayedQueue queue = new DoubleDelayedQueue();
            

            foreach(var task in queue.GetConsumingEnumerable())
            {
                lock (syncObject)
                {
                    var retryTask = Retry(task);
                    retryingDirectories.Add(task);
                    retryingTasks.Add(retryTask);
                }
            }
        }

        public async Task Retry(DirectoryChange retryingDirectory)
        {
            TimeSpan delay = TimeSpan.FromSeconds(10);
            TimeSpan maxDelay = TimeSpan.FromMinutes(10);
            while(true)
            {
                await Task.Delay(delay);

                lock (syncObject)
                {
                    if (TryProcess(retryingDirectory))
                    {
                        retryingDirectories.Remove(retryingDirectory);
                        return;
                    }
                }

                delay = delay + delay;
                if (delay > maxDelay)
                    delay = maxDelay;
            }
        }

        public bool TryProcess(DirectoryChange task)
        {
            return false;
        }
    }
}
