using System.Threading;

namespace TransportTracker.Core.Threading
{
    public class DefaultThreadFactory : IThreadFactory
    {
        private readonly Dictionary<Guid, Thread> _threads = new();
        
        public Thread CreateThread(ThreadStart threadStart, string name = null, bool isBackground = true, ThreadPriority priority = ThreadPriority.Normal)
        {
            var thread = new Thread(threadStart)
            {
                IsBackground = isBackground,
                Priority = priority
            };
            if (!string.IsNullOrEmpty(name))
                thread.Name = name;
            return thread;
        }

        public Thread CreateThread(ParameterizedThreadStart paramThreadStart, string name = null, bool isBackground = true, ThreadPriority priority = ThreadPriority.Normal)
        {
            var thread = new Thread(paramThreadStart)
            {
                IsBackground = isBackground,
                Priority = priority
            };
            if (!string.IsNullOrEmpty(name))
                thread.Name = name;
            return thread;
        }

        public Task CreateDedicatedTask(Action action, CancellationToken cancellationToken = default)
        {
            return Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public Task<TResult> CreateDedicatedTask<TResult>(Func<TResult> function, CancellationToken cancellationToken = default)
        {
            return Task.Factory.StartNew(function, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public Guid RegisterThread(Thread thread, string category = null)
        {
            var id = Guid.NewGuid();
            _threads[id] = thread;
            // Optionally, store category if needed
            return id;
        }

        public void UnregisterThread(Guid threadId)
        {
            _threads.Remove(threadId);
        }
    }
}
