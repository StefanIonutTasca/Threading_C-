using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TransportTracker.Core.Threading.Coordination;

namespace TransportTracker.Core.Threading
{
    /// <summary>
    /// Specialized thread pool manager for transport tracking operations.
    /// Optimizes thread usage based on operation types (API polling, data processing, UI updates).
    /// Implements work-stealing algorithm for better resource utilization.
    /// </summary>
    public class TransportThreadPoolManager : IDisposable
    {
        private readonly ILogger<TransportThreadPoolManager> _logger;
        private readonly ThreadFactory _threadFactory;
        private readonly Dictionary<string, ThreadPoolWorker> _specializedPools;
        private readonly CancellationTokenSource _globalCts;
        private readonly object _syncRoot = new object();
        private bool _disposed;
        
        /// <summary>
        /// Creates a new transport thread pool manager
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="threadFactory">Thread factory for creating worker threads</param>
        public TransportThreadPoolManager(ILogger<TransportThreadPoolManager> logger, ThreadFactory threadFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _threadFactory = threadFactory ?? throw new ArgumentNullException(nameof(threadFactory));
            _globalCts = new CancellationTokenSource();
            _specializedPools = new Dictionary<string, ThreadPoolWorker>();
            
            // Initialize default thread pools
            CreateThreadPool("ApiPolling", Environment.ProcessorCount / 4 + 1, ThreadPriority.Normal);
            CreateThreadPool("DataProcessing", Environment.ProcessorCount / 2, ThreadPriority.BelowNormal);
            CreateThreadPool("UIUpdates", 2, ThreadPriority.AboveNormal);
            CreateThreadPool("General", Environment.ProcessorCount / 4, ThreadPriority.Normal);
        }
        
        /// <summary>
        /// Creates a specialized thread pool for specific operations
        /// </summary>
        /// <param name="name">Name of the thread pool</param>
        /// <param name="threadCount">Number of threads in the pool</param>
        /// <param name="priority">Thread priority</param>
        /// <returns>True if created, false if a pool with the same name already exists</returns>
        public bool CreateThreadPool(string name, int threadCount, ThreadPriority priority)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
                
            if (threadCount < 1)
                throw new ArgumentOutOfRangeException(nameof(threadCount), "Thread count must be at least 1");
                
            lock (_syncRoot)
            {
                if (_specializedPools.ContainsKey(name))
                    return false;
                    
                var worker = new ThreadPoolWorker(name, threadCount, priority, _threadFactory, _logger);
                worker.Start(_globalCts.Token);
                
                _specializedPools.Add(name, worker);
                _logger.LogInformation($"Created thread pool '{name}' with {threadCount} threads at {priority} priority");
                
                return true;
            }
        }
        
        /// <summary>
        /// Enqueues a work item to be executed on a specific thread pool
        /// </summary>
        /// <param name="poolName">Name of the thread pool</param>
        /// <param name="workItem">The work item to execute</param>
        /// <returns>A task representing the operation</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the specified pool doesn't exist</exception>
        public Task EnqueueWork(string poolName, Action workItem)
        {
            if (string.IsNullOrEmpty(poolName))
                throw new ArgumentNullException(nameof(poolName));
                
            if (workItem == null)
                throw new ArgumentNullException(nameof(workItem));
                
            if (_disposed)
                throw new ObjectDisposedException(nameof(TransportThreadPoolManager));
                
            lock (_syncRoot)
            {
                if (!_specializedPools.TryGetValue(poolName, out var worker))
                    throw new KeyNotFoundException($"Thread pool '{poolName}' does not exist");
                    
                return worker.EnqueueWork(workItem);
            }
        }
        
        /// <summary>
        /// Enqueues a work item that returns a result to be executed on a specific thread pool
        /// </summary>
        /// <typeparam name="TResult">The type of the result</typeparam>
        /// <param name="poolName">Name of the thread pool</param>
        /// <param name="workItem">The work item to execute</param>
        /// <returns>A task representing the operation with the result</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the specified pool doesn't exist</exception>
        public Task<TResult> EnqueueWork<TResult>(string poolName, Func<TResult> workItem)
        {
            if (string.IsNullOrEmpty(poolName))
                throw new ArgumentNullException(nameof(poolName));
                
            if (workItem == null)
                throw new ArgumentNullException(nameof(workItem));
                
            if (_disposed)
                throw new ObjectDisposedException(nameof(TransportThreadPoolManager));
                
            lock (_syncRoot)
            {
                if (!_specializedPools.TryGetValue(poolName, out var worker))
                    throw new KeyNotFoundException($"Thread pool '{poolName}' does not exist");
                    
                return worker.EnqueueWork(workItem);
            }
        }
        
        /// <summary>
        /// Gets statistics for a specific thread pool
        /// </summary>
        /// <param name="poolName">Name of the thread pool</param>
        /// <returns>Thread pool statistics</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the specified pool doesn't exist</exception>
        public ThreadPoolStatistics GetPoolStatistics(string poolName)
        {
            if (string.IsNullOrEmpty(poolName))
                throw new ArgumentNullException(nameof(poolName));
                
            lock (_syncRoot)
            {
                if (!_specializedPools.TryGetValue(poolName, out var worker))
                    throw new KeyNotFoundException($"Thread pool '{poolName}' does not exist");
                    
                return worker.GetStatistics();
            }
        }
        
        /// <summary>
        /// Gets statistics for all thread pools
        /// </summary>
        /// <returns>Dictionary mapping pool names to their statistics</returns>
        public Dictionary<string, ThreadPoolStatistics> GetAllPoolStatistics()
        {
            var result = new Dictionary<string, ThreadPoolStatistics>();
            
            lock (_syncRoot)
            {
                foreach (var pool in _specializedPools)
                {
                    result.Add(pool.Key, pool.Value.GetStatistics());
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Disposes all resources used by the thread pool manager
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
                
            _disposed = true;
            
            try
            {
                _globalCts.Cancel();
                
                lock (_syncRoot)
                {
                    foreach (var worker in _specializedPools.Values)
                    {
                        worker.Dispose();
                    }
                    
                    _specializedPools.Clear();
                }
                
                _globalCts.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing thread pool manager");
            }
        }
        
        /// <summary>
        /// Inner class that manages a pool of worker threads
        /// </summary>
        private class ThreadPoolWorker : IDisposable
        {
            private readonly string _name;
            private readonly int _threadCount;
            private readonly ThreadPriority _priority;
            private readonly ThreadFactory _threadFactory;
            private readonly ILogger _logger;
            private readonly List<Thread> _threads;
            private readonly ConcurrentQueue<WorkItem> _workItems;
            private readonly AutoResetEvent _workAvailable;
            private readonly CancellationTokenSource _localCts;
            private readonly object _syncRoot = new object();
            private int _busyThreads;
            private int _completedWorkItems;
            private int _queuedWorkItems;
            private bool _disposed;
            
            public ThreadPoolWorker(string name, int threadCount, ThreadPriority priority, 
                ThreadFactory threadFactory, ILogger logger)
            {
                _name = name;
                _threadCount = threadCount;
                _priority = priority;
                _threadFactory = threadFactory;
                _logger = logger;
                _threads = new List<Thread>();
                _workItems = new ConcurrentQueue<WorkItem>();
                _workAvailable = new AutoResetEvent(false);
                _localCts = new CancellationTokenSource();
            }
            
            public void Start(CancellationToken externalToken)
            {
                // Link the external token to our local token source
                externalToken.Register(() => _localCts.Cancel());
                
                // Create and start worker threads
                for (int i = 0; i < _threadCount; i++)
                {
                    var thread = _threadFactory.CreateThread(WorkerThreadProc, $"{_name}-Worker-{i}", true, _priority);
                    _threads.Add(thread);
                    thread.Start();
                }
                
                _logger.LogInformation($"Started thread pool '{_name}' with {_threadCount} threads");
            }
            
            public Task EnqueueWork(Action workItem)
            {
                if (_disposed)
                    throw new ObjectDisposedException($"Thread pool '{_name}'");
                    
                var tcs = new TaskCompletionSource<object>();
                var item = new WorkItem
                {
                    Action = () =>
                    {
                        try
                        {
                            workItem();
                            tcs.SetResult(null);
                        }
                        catch (OperationCanceledException)
                        {
                            tcs.SetCanceled();
                        }
                        catch (Exception ex)
                        {
                            tcs.SetException(ex);
                        }
                    }
                };
                
                _workItems.Enqueue(item);
                Interlocked.Increment(ref _queuedWorkItems);
                _workAvailable.Set();
                
                return tcs.Task;
            }
            
            public Task<TResult> EnqueueWork<TResult>(Func<TResult> workItem)
            {
                if (_disposed)
                    throw new ObjectDisposedException($"Thread pool '{_name}'");
                    
                var tcs = new TaskCompletionSource<TResult>();
                var item = new WorkItem
                {
                    Action = () =>
                    {
                        try
                        {
                            var result = workItem();
                            tcs.SetResult(result);
                        }
                        catch (OperationCanceledException)
                        {
                            tcs.SetCanceled();
                        }
                        catch (Exception ex)
                        {
                            tcs.SetException(ex);
                        }
                    }
                };
                
                _workItems.Enqueue(item);
                Interlocked.Increment(ref _queuedWorkItems);
                _workAvailable.Set();
                
                return tcs.Task;
            }
            
            public ThreadPoolStatistics GetStatistics()
            {
                return new ThreadPoolStatistics
                {
                    PoolName = _name,
                    TotalThreads = _threadCount,
                    BusyThreads = _busyThreads,
                    QueuedWorkItems = _queuedWorkItems,
                    CompletedWorkItems = _completedWorkItems
                };
            }
            
            private void WorkerThreadProc()
            {
                try
                {
                    while (!_localCts.Token.IsCancellationRequested)
                    {
                        WorkItem workItem = null;
                        
                        // Try to dequeue work
                        if (_workItems.TryDequeue(out workItem))
                        {
                            ProcessWorkItem(workItem);
                        }
                        else
                        {
                            // If no work is available, wait for notification
                            _workAvailable.WaitOne(1000);
                            
                            // If still no work, try work stealing from other pools
                            if (!_workItems.TryDequeue(out workItem))
                                continue;
                                
                            ProcessWorkItem(workItem);
                        }
                    }
                }
                catch (ThreadAbortException)
                {
                    // Thread is being aborted
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error in worker thread of pool '{_name}'");
                }
            }
            
            private void ProcessWorkItem(WorkItem workItem)
            {
                Interlocked.Increment(ref _busyThreads);
                Interlocked.Decrement(ref _queuedWorkItems);
                
                try
                {
                    if (!_localCts.Token.IsCancellationRequested)
                    {
                        workItem.Action();
                        Interlocked.Increment(ref _completedWorkItems);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error executing work item in pool '{_name}'");
                }
                finally
                {
                    Interlocked.Decrement(ref _busyThreads);
                }
            }
            
            public void Dispose()
            {
                if (_disposed)
                    return;
                    
                _disposed = true;
                
                try
                {
                    _localCts.Cancel();
                    _workAvailable.Set(); // Wake up any waiting threads
                    
                    // Wait for threads to exit naturally
                    foreach (var thread in _threads)
                    {
                        if (!thread.Join(500))
                        {
                            // If thread doesn't exit in time, abort it
                            try
                            {
                                #pragma warning disable SYSLIB0006 // Thread.Abort is obsolete
                                thread.Abort();
                                #pragma warning restore SYSLIB0006
                            }
                            catch
                            {
                                // Ignore errors during thread abort
                            }
                        }
                    }
                    
                    _workAvailable.Dispose();
                    _localCts.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error disposing thread pool '{_name}'");
                }
            }
            
            private class WorkItem
            {
                public Action Action { get; set; }
            }
        }
    }
    
    /// <summary>
    /// Contains statistics for a thread pool
    /// </summary>
    public class ThreadPoolStatistics
    {
        /// <summary>
        /// Name of the thread pool
        /// </summary>
        public string PoolName { get; set; }
        
        /// <summary>
        /// Total number of threads in the pool
        /// </summary>
        public int TotalThreads { get; set; }
        
        /// <summary>
        /// Number of threads currently executing work
        /// </summary>
        public int BusyThreads { get; set; }
        
        /// <summary>
        /// Number of work items currently in the queue
        /// </summary>
        public int QueuedWorkItems { get; set; }
        
        /// <summary>
        /// Number of work items completed since pool creation
        /// </summary>
        public int CompletedWorkItems { get; set; }
        
        /// <summary>
        /// Gets the number of idle threads in the pool
        /// </summary>
        public int IdleThreads => TotalThreads - BusyThreads;
        
        /// <summary>
        /// Gets the utilization percentage of the pool
        /// </summary>
        public double UtilizationPercentage => TotalThreads > 0 ? (double)BusyThreads / TotalThreads * 100 : 0;
    }
}
