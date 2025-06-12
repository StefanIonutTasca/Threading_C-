using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Threading
{
    /// <summary>
    /// Implementation of IThreadFactory that creates and manages threads with proper configuration and tracking
    /// </summary>
    public class ThreadFactory : IThreadFactory, IDisposable
    {
        private readonly ILogger<ThreadFactory> _logger;
        private readonly ConcurrentDictionary<Guid, ThreadInfo> _managedThreads;
        private readonly object _syncRoot = new object();
        private bool _disposed = false;
        
        /// <summary>
        /// Creates a new instance of ThreadFactory
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public ThreadFactory(ILogger<ThreadFactory> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _managedThreads = new ConcurrentDictionary<Guid, ThreadInfo>();
        }
        
        /// <inheritdoc />
        public Thread CreateThread(ThreadStart threadStart, string name = null, bool isBackground = true, ThreadPriority priority = ThreadPriority.Normal)
        {
            if (threadStart == null)
                throw new ArgumentNullException(nameof(threadStart));
                
            var thread = new Thread(threadStart)
            {
                IsBackground = isBackground,
                Priority = priority
            };
            
            if (!string.IsNullOrWhiteSpace(name))
                thread.Name = name;
            
            _logger.LogDebug($"Thread created: {thread.ManagedThreadId} - {name ?? "Unnamed"}");
            return thread;
        }
        
        /// <inheritdoc />
        public Thread CreateThread(ParameterizedThreadStart paramThreadStart, string name = null, bool isBackground = true, ThreadPriority priority = ThreadPriority.Normal)
        {
            if (paramThreadStart == null)
                throw new ArgumentNullException(nameof(paramThreadStart));
                
            var thread = new Thread(paramThreadStart)
            {
                IsBackground = isBackground,
                Priority = priority
            };
            
            if (!string.IsNullOrWhiteSpace(name))
                thread.Name = name;
            
            _logger.LogDebug($"Thread created: {thread.ManagedThreadId} - {name ?? "Unnamed"}");
            return thread;
        }
        
        /// <inheritdoc />
        public Task CreateDedicatedTask(Action action, CancellationToken cancellationToken = default)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            
            var tcs = new TaskCompletionSource<object>();
            
            var thread = CreateThread(() =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.SetCanceled(cancellationToken);
                        return;
                    }
                    
                    action();
                    tcs.SetResult(null);
                }
                catch (OperationCanceledException oce) when (oce.CancellationToken == cancellationToken)
                {
                    tcs.SetCanceled(cancellationToken);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            
            thread.Start();
            
            return tcs.Task;
        }
        
        /// <inheritdoc />
        public Task<TResult> CreateDedicatedTask<TResult>(Func<TResult> function, CancellationToken cancellationToken = default)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));
            
            var tcs = new TaskCompletionSource<TResult>();
            
            var thread = CreateThread(() =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.SetCanceled(cancellationToken);
                        return;
                    }
                    
                    var result = function();
                    tcs.SetResult(result);
                }
                catch (OperationCanceledException oce) when (oce.CancellationToken == cancellationToken)
                {
                    tcs.SetCanceled(cancellationToken);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            
            thread.Start();
            
            return tcs.Task;
        }
        
        /// <inheritdoc />
        public Guid RegisterThread(Thread thread, string category = null)
        {
            if (thread == null)
                throw new ArgumentNullException(nameof(thread));
            
            var threadInfo = new ThreadInfo
            {
                Thread = thread,
                Category = category,
                CreationTime = DateTime.UtcNow
            };
            
            var id = Guid.NewGuid();
            if (_managedThreads.TryAdd(id, threadInfo))
            {
                _logger.LogInformation($"Thread registered: {thread.ManagedThreadId} - {thread.Name ?? "Unnamed"} ({category ?? "Uncategorized"})");
                return id;
            }
            else
            {
                _logger.LogWarning($"Failed to register thread: {thread.ManagedThreadId}");
                return Guid.Empty;
            }
        }
        
        /// <inheritdoc />
        public void UnregisterThread(Guid threadId)
        {
            if (threadId == Guid.Empty)
                return;
                
            if (_managedThreads.TryRemove(threadId, out var threadInfo))
            {
                _logger.LogInformation($"Thread unregistered: {threadInfo.Thread.ManagedThreadId} - {threadInfo.Thread.Name ?? "Unnamed"} ({threadInfo.Category ?? "Uncategorized"})");
            }
            else
            {
                _logger.LogWarning($"Attempted to unregister unknown thread ID: {threadId}");
            }
        }
        
        /// <summary>
        /// Gets information about all currently managed threads
        /// </summary>
        /// <returns>A dictionary of thread IDs to thread information</returns>
        public IReadOnlyDictionary<Guid, ThreadInfo> GetManagedThreads()
        {
            return _managedThreads;
        }
        
        /// <summary>
        /// Gets information about all threads in a specific category
        /// </summary>
        /// <param name="category">The category to filter by</param>
        /// <returns>A dictionary of thread IDs to thread information</returns>
        public IReadOnlyDictionary<Guid, ThreadInfo> GetThreadsByCategory(string category)
        {
            var result = new Dictionary<Guid, ThreadInfo>();
            
            foreach (var pair in _managedThreads)
            {
                if (string.Equals(pair.Value.Category, category, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(pair.Key, pair.Value);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Terminates and unregisters all managed threads
        /// </summary>
        public void TerminateAllThreads()
        {
            lock (_syncRoot)
            {
                foreach (var threadId in _managedThreads.Keys)
                {
                    if (_managedThreads.TryRemove(threadId, out var threadInfo))
                    {
                        try
                        {
                            if (threadInfo.Thread.IsAlive)
                            {
                                // Use Abort only as a last resort
                                #pragma warning disable SYSLIB0006 // Thread.Abort is obsolete
                                threadInfo.Thread.Abort();
                                #pragma warning restore SYSLIB0006
                                
                                _logger.LogWarning($"Thread forcefully terminated: {threadInfo.Thread.ManagedThreadId}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error terminating thread: {threadInfo.Thread.ManagedThreadId}");
                        }
                    }
                }
                
                _managedThreads.Clear();
                _logger.LogInformation("All threads terminated and unregistered");
            }
        }
        
        /// <summary>
        /// Disposes the thread factory and terminates all managed threads
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
                
            _disposed = true;
            
            TerminateAllThreads();
            
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Represents information about a managed thread
        /// </summary>
        public class ThreadInfo
        {
            /// <summary>
            /// The thread instance
            /// </summary>
            public Thread Thread { get; set; }
            
            /// <summary>
            /// Optional category or group the thread belongs to
            /// </summary>
            public string Category { get; set; }
            
            /// <summary>
            /// When the thread was created/registered
            /// </summary>
            public DateTime CreationTime { get; set; }
            
            /// <summary>
            /// Custom state data associated with the thread (if any)
            /// </summary>
            public object State { get; set; }
        }
    }
}
