
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.Threading
{
    public class TaskQueue
    {
        //----- params -----

        //----- field -----

        private readonly ConcurrentQueue<Func<Task>> processingQueue = new ConcurrentQueue<Func<Task>>();
        private readonly ConcurrentDictionary<int, Task> runningTasks = new ConcurrentDictionary<int, Task>();
        private readonly int maxParallelizationCount = 0;
        private readonly int maxQueueLength = 0;

        private TaskCompletionSource<bool> tscQueue = new TaskCompletionSource<bool>();

        //----- property -----

        //----- method -----

        public TaskQueue(int? maxParallelizationCount = null, int? maxQueueLength = null)
        {
            this.maxParallelizationCount = maxParallelizationCount ?? int.MaxValue;
            this.maxQueueLength = maxQueueLength ?? int.MaxValue;
        }

        public bool Queue(Func<Task> futureTask)
        {
            if (processingQueue.Count < maxQueueLength)
            {
                processingQueue.Enqueue(futureTask);
                return true;
            }
            return false;
        }

        public int GetQueueCount()
        {
            return processingQueue.Count;
        }

        public int GetRunningCount()
        {
            return runningTasks.Count;
        }

        public async Task Process()
        {
            var t = tscQueue.Task;
            StartTasks();
            await t;
        }

        public void ProcessBackground(Action<Exception> exception = null)
        {
            Task.Run(Process).ContinueWith(t => {
                exception?.Invoke(t.Exception);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private void StartTasks()
        {
            var startMaxCount = maxParallelizationCount - runningTasks.Count;

            for (var i = 0; i < startMaxCount; i++)
            {
                if (!processingQueue.TryDequeue(out var futureTask))
                {
                    // Queue is most likely empty
                    break;
                }

                var t = Task.Run(futureTask);

                if (!runningTasks.TryAdd(t.GetHashCode(), t))
                {
                    throw new Exception("Should not happen, hash codes are unique");
                }

                t.ContinueWith((t2) =>
                {
                    if (!runningTasks.TryRemove(t2.GetHashCode(), out _))
                    {
                        throw new Exception("Should not happen, hash codes are unique");
                    }

                    // Continue the queue processing
                    StartTasks();
                });
            }

            if (processingQueue.IsEmpty && runningTasks.IsEmpty)
            {
                // Interlocked.Exchange might not be necessary
                var oldQueue = Interlocked.Exchange(ref tscQueue, new TaskCompletionSource<bool>());

                oldQueue.TrySetResult(true);
            }
        }
    }
}
