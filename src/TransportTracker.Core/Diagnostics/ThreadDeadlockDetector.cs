using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TransportTracker.Core.Diagnostics
{
    /// <summary>
    /// Detects potential thread deadlocks in the application
    /// </summary>
    public class ThreadDeadlockDetector : IDisposable
    {
        private readonly ILogger _logger;
        private readonly DiagnosticLogger _diagnosticLogger;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<int, ThreadResourceInfo> _threadResources = new();
        private readonly TimeSpan _detectionInterval;
        private readonly TimeSpan _suspectThreshold;
        private Task _monitoringTask;
        private bool _disposed;

        /// <summary>
        /// Creates a new instance of ThreadDeadlockDetector
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="diagnosticLogger">Diagnostic logger instance</param>
        /// <param name="detectionIntervalSeconds">Interval between deadlock detection runs in seconds</param>
        /// <param name="suspectThresholdSeconds">Threshold in seconds before a thread is considered deadlocked</param>
        public ThreadDeadlockDetector(
            ILogger logger,
            DiagnosticLogger diagnosticLogger = null,
            int detectionIntervalSeconds = 30,
            int suspectThresholdSeconds = 60)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _diagnosticLogger = diagnosticLogger;
            _detectionInterval = TimeSpan.FromSeconds(detectionIntervalSeconds);
            _suspectThreshold = TimeSpan.FromSeconds(suspectThresholdSeconds);
            
            _logger.LogInformation(
                $"Thread deadlock detector initialized with interval {_detectionInterval.TotalSeconds}s " +
                $"and threshold {_suspectThreshold.TotalSeconds}s");
        }

        /// <summary>
        /// Starts the deadlock detection monitoring
        /// </summary>
        public void Start()
        {
            if (_monitoringTask != null) return;

            _logger.LogInformation("Starting thread deadlock detector");
            _monitoringTask = Task.Run(MonitorForDeadlocksAsync);
        }

        /// <summary>
        /// Stops the deadlock detection monitoring
        /// </summary>
        public void Stop()
        {
            if (_monitoringTask == null) return;

            _logger.LogInformation("Stopping thread deadlock detector");
            _cts.Cancel();
            _monitoringTask = null;
        }

        /// <summary>
        /// Registers that a thread is waiting for a resource
        /// </summary>
        /// <param name="resourceId">Identifier for the resource being waited on</param>
        /// <param name="resourceType">Type of resource (e.g., "lock", "semaphore")</param>
        /// <param name="timeout">Expected maximum wait time</param>
        public void RegisterThreadWaiting(string resourceId, string resourceType, TimeSpan? timeout = null)
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            string threadName = Thread.CurrentThread.Name ?? $"Thread_{threadId}";
            
            var resourceInfo = new ThreadResourceInfo
            {
                ThreadId = threadId,
                ThreadName = threadName,
                WaitingForResource = resourceId,
                ResourceType = resourceType,
                WaitStart = DateTime.Now,
                ExpectedTimeout = timeout,
                StackTrace = Environment.StackTrace
            };
            
            _threadResources[threadId] = resourceInfo;
            
            _logger.LogDebug(
                $"Thread {threadName} (ID: {threadId}) waiting for {resourceType} '{resourceId}'" +
                (timeout.HasValue ? $" with timeout {timeout.Value.TotalSeconds}s" : ""));
        }

        /// <summary>
        /// Registers that a thread has acquired a resource
        /// </summary>
        /// <param name="resourceId">Identifier for the acquired resource</param>
        /// <param name="resourceType">Type of resource</param>
        public void RegisterThreadAcquiredResource(string resourceId, string resourceType)
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            string threadName = Thread.CurrentThread.Name ?? $"Thread_{threadId}";
            
            var resourceInfo = _threadResources.GetOrAdd(threadId, _ => new ThreadResourceInfo
            {
                ThreadId = threadId,
                ThreadName = threadName
            });
            
            resourceInfo.HeldResources ??= new List<ResourceInfo>();
            resourceInfo.HeldResources.Add(new ResourceInfo
            {
                ResourceId = resourceId,
                ResourceType = resourceType,
                AcquiredAt = DateTime.Now
            });
            
            // Clear waiting flag if this thread was waiting for this resource
            if (resourceId == resourceInfo.WaitingForResource && resourceType == resourceInfo.ResourceType)
            {
                resourceInfo.WaitingForResource = null;
                resourceInfo.ResourceType = null;
                resourceInfo.WaitStart = DateTime.MinValue;
                resourceInfo.ExpectedTimeout = null;
            }
            
            _logger.LogDebug(
                $"Thread {threadName} (ID: {threadId}) acquired {resourceType} '{resourceId}'");
        }

        /// <summary>
        /// Registers that a thread has released a resource
        /// </summary>
        /// <param name="resourceId">Identifier for the released resource</param>
        /// <param name="resourceType">Type of resource</param>
        public void RegisterThreadReleasedResource(string resourceId, string resourceType)
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            
            if (_threadResources.TryGetValue(threadId, out var resourceInfo) && 
                resourceInfo.HeldResources != null)
            {
                resourceInfo.HeldResources.RemoveAll(r => 
                    r.ResourceId == resourceId && r.ResourceType == resourceType);
                
                _logger.LogDebug(
                    $"Thread {resourceInfo.ThreadName} (ID: {threadId}) released {resourceType} '{resourceId}'");
            }
        }

        /// <summary>
        /// Gets information about currently suspected deadlocks
        /// </summary>
        /// <returns>Collection of deadlock information</returns>
        public IEnumerable<DeadlockInfo> GetSuspectedDeadlocks()
        {
            var currentTime = DateTime.Now;
            var resultList = new List<DeadlockInfo>();
            
            // Find threads that have been waiting too long
            var longWaitingThreads = _threadResources.Values
                .Where(t => !string.IsNullOrEmpty(t.WaitingForResource) &&
                           (currentTime - t.WaitStart) > _suspectThreshold)
                .ToList();
            
            foreach (var waitingThread in longWaitingThreads)
            {
                // Find threads holding the resource this thread is waiting for
                var resourceHolders = _threadResources.Values
                    .Where(t => t.HeldResources != null &&
                               t.HeldResources.Any(r => r.ResourceId == waitingThread.WaitingForResource &&
                                                      r.ResourceType == waitingThread.ResourceType))
                    .ToList();
                
                if (resourceHolders.Any())
                {
                    // Check if any of these threads is waiting for a resource held by the waiting thread
                    foreach (var holder in resourceHolders)
                    {
                        // Check if holder is waiting for any resource
                        if (string.IsNullOrEmpty(holder.WaitingForResource))
                            continue;
                        
                        // Check if waiting thread holds the resource the holder is waiting for
                        if (waitingThread.HeldResources != null &&
                            waitingThread.HeldResources.Any(r => r.ResourceId == holder.WaitingForResource &&
                                                              r.ResourceType == holder.ResourceType))
                        {
                            // We have a potential deadlock
                            resultList.Add(new DeadlockInfo
                            {
                                DetectedAt = currentTime,
                                InvolvedThreads = new[] { waitingThread, holder },
                                DeadlockDescription = $"Thread {waitingThread.ThreadName} (ID: {waitingThread.ThreadId}) " +
                                                     $"is waiting for {waitingThread.ResourceType} '{waitingThread.WaitingForResource}' " +
                                                     $"held by thread {holder.ThreadName} (ID: {holder.ThreadId}) which " +
                                                     $"is waiting for {holder.ResourceType} '{holder.WaitingForResource}' " +
                                                     $"held by first thread"
                            });
                        }
                    }
                }
                else
                {
                    // No holder found, but thread has been waiting too long
                    resultList.Add(new DeadlockInfo
                    {
                        DetectedAt = currentTime,
                        InvolvedThreads = new[] { waitingThread },
                        DeadlockDescription = $"Thread {waitingThread.ThreadName} (ID: {waitingThread.ThreadId}) " +
                                             $"has been waiting for {waitingThread.ResourceType} '{waitingThread.WaitingForResource}' " +
                                             $"for {(currentTime - waitingThread.WaitStart).TotalSeconds:F1} seconds, " +
                                             $"but no thread appears to be holding it"
                    });
                }
            }
            
            return resultList;
        }

        /// <summary>
        /// Background task to monitor for thread deadlocks
        /// </summary>
        private async Task MonitorForDeadlocksAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    CheckForDeadlocks();
                    await Task.Delay(_detectionInterval, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in thread deadlock detection");
            }
        }

        /// <summary>
        /// Checks for potential thread deadlocks
        /// </summary>
        private void CheckForDeadlocks()
        {
            try
            {
                var suspectedDeadlocks = GetSuspectedDeadlocks().ToList();
                
                if (suspectedDeadlocks.Count > 0)
                {
                    foreach (var deadlock in suspectedDeadlocks)
                    {
                        _logger.LogWarning($"Potential deadlock detected: {deadlock.DeadlockDescription}");
                        
                        // Log detailed diagnostic information
                        _diagnosticLogger?.LogDeadlockDetection(
                            deadlock.DeadlockDescription,
                            deadlock.InvolvedThreads);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for thread deadlocks");
            }
        }

        /// <summary>
        /// Disposes resources used by the detector
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources used by the detector
        /// </summary>
        /// <param name="disposing">True if called from Dispose()</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                Stop();
                _cts.Dispose();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Information about a thread's resource usage
    /// </summary>
    public class ThreadResourceInfo
    {
        /// <summary>
        /// Gets or sets the thread ID
        /// </summary>
        public int ThreadId { get; set; }
        
        /// <summary>
        /// Gets or sets the thread name
        /// </summary>
        public string ThreadName { get; set; }
        
        /// <summary>
        /// Gets or sets the resource the thread is waiting for
        /// </summary>
        public string WaitingForResource { get; set; }
        
        /// <summary>
        /// Gets or sets the type of resource the thread is waiting for
        /// </summary>
        public string ResourceType { get; set; }
        
        /// <summary>
        /// Gets or sets when the thread started waiting
        /// </summary>
        public DateTime WaitStart { get; set; }
        
        /// <summary>
        /// Gets or sets the expected timeout for the wait
        /// </summary>
        public TimeSpan? ExpectedTimeout { get; set; }
        
        /// <summary>
        /// Gets or sets the stack trace when wait was registered
        /// </summary>
        public string StackTrace { get; set; }
        
        /// <summary>
        /// Gets or sets the resources held by the thread
        /// </summary>
        public List<ResourceInfo> HeldResources { get; set; }
    }

    /// <summary>
    /// Information about a resource held by a thread
    /// </summary>
    public class ResourceInfo
    {
        /// <summary>
        /// Gets or sets the resource ID
        /// </summary>
        public string ResourceId { get; set; }
        
        /// <summary>
        /// Gets or sets the resource type
        /// </summary>
        public string ResourceType { get; set; }
        
        /// <summary>
        /// Gets or sets when the resource was acquired
        /// </summary>
        public DateTime AcquiredAt { get; set; }
    }

    /// <summary>
    /// Information about a detected deadlock
    /// </summary>
    public class DeadlockInfo
    {
        /// <summary>
        /// Gets or sets when the deadlock was detected
        /// </summary>
        public DateTime DetectedAt { get; set; }
        
        /// <summary>
        /// Gets or sets the threads involved in the deadlock
        /// </summary>
        public IEnumerable<ThreadResourceInfo> InvolvedThreads { get; set; }
        
        /// <summary>
        /// Gets or sets a description of the deadlock
        /// </summary>
        public string DeadlockDescription { get; set; }
    }
}
