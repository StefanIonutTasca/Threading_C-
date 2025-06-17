using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TransportTracker.App.Core.Diagnostics
{
    /// <summary>
    /// Central performance monitoring and tracking system
    /// </summary>
    public class PerformanceMonitor : IDisposable
    {
        private static readonly Lazy<PerformanceMonitor> _instance = new Lazy<PerformanceMonitor>(() => new PerformanceMonitor());
        
        /// <summary>
        /// Gets the singleton instance
        /// </summary>
        public static PerformanceMonitor Instance => _instance.Value;
        
        private readonly ConcurrentDictionary<string, PerformanceMetric> _metrics = new();
        private readonly ConcurrentDictionary<int, ThreadInfo> _threadInfo = new();
        private readonly ConcurrentDictionary<string, OperationTimer> _activeTimers = new();
        private readonly Stopwatch _uptime = new Stopwatch();
        
        private Timer _cleanupTimer;
        private Timer _sampleTimer;
        private bool _disposed;
        
        /// <summary>
        /// Gets the application uptime
        /// </summary>
        public TimeSpan Uptime => _uptime.Elapsed;
        
        /// <summary>
        /// Gets the current active thread count
        /// </summary>
        public int ThreadCount => _threadInfo.Count;
        
        /// <summary>
        /// Gets the number of operations being tracked
        /// </summary>
        public int OperationCount => _metrics.Count;
        
        /// <summary>
        /// Event triggered when metrics are updated
        /// </summary>
        public event EventHandler<MetricsUpdatedEventArgs> MetricsUpdated;
        
        /// <summary>
        /// Gets a snapshot of the current metrics
        /// </summary>
        public IReadOnlyList<PerformanceMetric> Metrics => _metrics.Values.ToList();
        
        /// <summary>
        /// Gets a snapshot of the current thread information
        /// </summary>
        public IReadOnlyList<ThreadInfo> ThreadDetails => _threadInfo.Values.ToList();
        
        /// <summary>
        /// Private constructor for singleton pattern
        /// </summary>
        private PerformanceMonitor()
        {
            _uptime.Start();
            
            // Schedule periodic cleanup of stale metrics
            _cleanupTimer = new Timer(CleanupStaleMetrics, null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5));
            
            // Schedule periodic sample collection for active threads
            _sampleTimer = new Timer(SampleThreads, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Starts timing an operation with the given name
        /// </summary>
        /// <param name="operationName">Unique name for this operation</param>
        /// <returns>Timer object for this operation</returns>
        public IDisposable StartOperation(string operationName)
        {
            if (string.IsNullOrEmpty(operationName))
                throw new ArgumentNullException(nameof(operationName));
                
            // Create a unique ID for this instance of the operation
            string timerId = $"{operationName}_{Guid.NewGuid()}";
            
            // Create and start the timer
            var timer = new OperationTimer(operationName, timerId, this);
            _activeTimers[timerId] = timer;
            
            return timer;
        }
        
        /// <summary>
        /// Records a completed operation
        /// </summary>
        internal void RecordOperation(string operationName, string timerId, TimeSpan duration)
        {
            // Remove from active timers
            _activeTimers.TryRemove(timerId, out _);
            
            // Get or create the metric
            var metric = _metrics.GetOrAdd(operationName, _ => new PerformanceMetric(operationName));
            
            // Update the metric
            metric.RecordExecution(duration);
            
            // Raise event
            MetricsUpdated?.Invoke(this, new MetricsUpdatedEventArgs(metric));
        }
        
        /// <summary>
        /// Records a failed operation
        /// </summary>
        public void RecordFailure(string operationName, Exception error = null)
        {
            var metric = _metrics.GetOrAdd(operationName, _ => new PerformanceMetric(operationName));
            metric.RecordFailure(error);
            
            MetricsUpdated?.Invoke(this, new MetricsUpdatedEventArgs(metric));
        }
        
        /// <summary>
        /// Registers a thread for monitoring
        /// </summary>
        public void RegisterThread(int threadId, string name, ThreadCategory category)
        {
            var info = _threadInfo.GetOrAdd(threadId, _ => new ThreadInfo
            {
                ThreadId = threadId,
                Name = name,
                Category = category,
                StartTime = DateTime.Now
            });
            
            info.LastActive = DateTime.Now;
        }
        
        /// <summary>
        /// Registers the current thread for monitoring
        /// </summary>
        public void RegisterCurrentThread(string name, ThreadCategory category)
        {
            RegisterThread(Thread.CurrentThread.ManagedThreadId, name, category);
        }
        
        /// <summary>
        /// Updates thread status to indicate it is active
        /// </summary>
        public void UpdateThreadStatus(int threadId, ThreadStatus status, string currentOperation = null)
        {
            if (_threadInfo.TryGetValue(threadId, out var info))
            {
                info.Status = status;
                info.LastActive = DateTime.Now;
                
                if (currentOperation != null)
                {
                    info.CurrentOperation = currentOperation;
                }
            }
        }
        
        /// <summary>
        /// Updates current thread status
        /// </summary>
        public void UpdateCurrentThreadStatus(ThreadStatus status, string currentOperation = null)
        {
            UpdateThreadStatus(Thread.CurrentThread.ManagedThreadId, status, currentOperation);
        }
        
        /// <summary>
        /// Records when a thread is no longer in use
        /// </summary>
        public void UnregisterThread(int threadId)
        {
            if (_threadInfo.TryGetValue(threadId, out var info))
            {
                info.Status = ThreadStatus.Completed;
                info.EndTime = DateTime.Now;
                // We keep the thread info for history but mark it as completed
            }
        }
        
        /// <summary>
        /// Gets metrics for a specific operation
        /// </summary>
        public PerformanceMetric GetMetric(string operationName)
        {
            if (_metrics.TryGetValue(operationName, out var metric))
                return metric;
                
            return null;
        }
        
        /// <summary>
        /// Gets all metrics filtered by category
        /// </summary>
        public IEnumerable<PerformanceMetric> GetMetricsByCategory(MetricCategory category)
        {
            return _metrics.Values.Where(m => m.Category == category);
        }
        
        /// <summary>
        /// Gets the slowest operations
        /// </summary>
        public IEnumerable<PerformanceMetric> GetSlowestOperations(int count = 5)
        {
            return _metrics.Values
                .Where(m => m.ExecutionCount > 0)
                .OrderByDescending(m => m.AverageExecutionTime)
                .Take(count);
        }
        
        /// <summary>
        /// Gets the operations with the most failures
        /// </summary>
        public IEnumerable<PerformanceMetric> GetMostFailedOperations(int count = 5)
        {
            return _metrics.Values
                .Where(m => m.FailureCount > 0)
                .OrderByDescending(m => m.FailureCount)
                .Take(count);
        }
        
        /// <summary>
        /// Gets active threads by category
        /// </summary>
        public IEnumerable<ThreadInfo> GetThreadsByCategory(ThreadCategory category)
        {
            return _threadInfo.Values.Where(t => t.Category == category && t.Status != ThreadStatus.Completed);
        }
        
        /// <summary>
        /// Resets all metrics
        /// </summary>
        public void ResetMetrics()
        {
            _metrics.Clear();
        }
        
        /// <summary>
        /// Periodically clean up stale metrics
        /// </summary>
        private void CleanupStaleMetrics(object state)
        {
            // Find metrics that haven't been updated in the past hour
            var staleMetricKeys = _metrics.Where(m => 
                    m.Value.LastExecutionTime < DateTime.Now.AddHours(-1) &&
                    m.Value.ExecutionCount == 0)
                .Select(m => m.Key)
                .ToList();
                
            // Remove stale metrics
            foreach (var key in staleMetricKeys)
            {
                _metrics.TryRemove(key, out _);
            }
            
            // Find threads that are completed and inactive for a long time
            var staleThreadKeys = _threadInfo.Where(t => 
                    t.Value.Status == ThreadStatus.Completed && 
                    t.Value.EndTime < DateTime.Now.AddHours(-1))
                .Select(t => t.Key)
                .ToList();
                
            // Remove stale thread info
            foreach (var key in staleThreadKeys)
            {
                _threadInfo.TryRemove(key, out _);
            }
        }
        
        /// <summary>
        /// Periodically sample thread information
        /// </summary>
        private void SampleThreads(object state)
        {
            foreach (var thread in _threadInfo.Values)
            {
                if (thread.Status != ThreadStatus.Completed)
                {
                    // Consider threads inactive after 1 minute without updates
                    if (thread.LastActive < DateTime.Now.AddMinutes(-1))
                    {
                        thread.Status = ThreadStatus.Idle;
                    }
                    
                    // Sample CPU time if possible
                    // Note: In a real app, we might use a platform-specific approach
                    // to get actual CPU usage percentages
                }
            }
        }
        
        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Disposes resources
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
                
            if (disposing)
            {
                _cleanupTimer?.Dispose();
                _sampleTimer?.Dispose();
                
                _cleanupTimer = null;
                _sampleTimer = null;
            }
            
            _disposed = true;
        }
    }
    
    /// <summary>
    /// Tracks performance metrics for a specific operation
    /// </summary>
    public class PerformanceMetric
    {
        private readonly object _lock = new object();
        
        /// <summary>
        /// Name of the operation
        /// </summary>
        public string OperationName { get; }
        
        /// <summary>
        /// Category of the metric
        /// </summary>
        public MetricCategory Category { get; set; }
        
        /// <summary>
        /// Number of times this operation has been executed
        /// </summary>
        public int ExecutionCount { get; private set; }
        
        /// <summary>
        /// Number of times this operation has failed
        /// </summary>
        public int FailureCount { get; private set; }
        
        /// <summary>
        /// Total execution time across all calls
        /// </summary>
        public TimeSpan TotalExecutionTime { get; private set; }
        
        /// <summary>
        /// Minimum execution time observed
        /// </summary>
        public TimeSpan MinExecutionTime { get; private set; }
        
        /// <summary>
        /// Maximum execution time observed
        /// </summary>
        public TimeSpan MaxExecutionTime { get; private set; }
        
        /// <summary>
        /// Average execution time
        /// </summary>
        public TimeSpan AverageExecutionTime => 
            ExecutionCount > 0 ? TimeSpan.FromTicks(TotalExecutionTime.Ticks / ExecutionCount) : TimeSpan.Zero;
        
        /// <summary>
        /// Time of the most recent execution
        /// </summary>
        public DateTime LastExecutionTime { get; private set; }
        
        /// <summary>
        /// The most recent failure if any
        /// </summary>
        public Exception LastError { get; private set; }
        
        /// <summary>
        /// Creates a new performance metric
        /// </summary>
        public PerformanceMetric(string operationName)
        {
            OperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
            
            // Determine category based on operation name
            if (operationName.Contains("API", StringComparison.OrdinalIgnoreCase))
                Category = MetricCategory.NetworkIO;
            else if (operationName.Contains("Map", StringComparison.OrdinalIgnoreCase))
                Category = MetricCategory.MapOperations;
            else if (operationName.Contains("Data", StringComparison.OrdinalIgnoreCase) ||
                     operationName.Contains("Load", StringComparison.OrdinalIgnoreCase) ||
                     operationName.Contains("Save", StringComparison.OrdinalIgnoreCase))
                Category = MetricCategory.DataOperations;
            else if (operationName.Contains("UI", StringComparison.OrdinalIgnoreCase) ||
                     operationName.Contains("Render", StringComparison.OrdinalIgnoreCase))
                Category = MetricCategory.UserInterface;
            else
                Category = MetricCategory.Other;
                
            // Initialize times
            MinExecutionTime = TimeSpan.MaxValue;
            LastExecutionTime = DateTime.Now;
        }
        
        /// <summary>
        /// Records an execution of this operation
        /// </summary>
        public void RecordExecution(TimeSpan duration)
        {
            lock (_lock)
            {
                ExecutionCount++;
                TotalExecutionTime += duration;
                
                if (duration < MinExecutionTime)
                    MinExecutionTime = duration;
                    
                if (duration > MaxExecutionTime)
                    MaxExecutionTime = duration;
                    
                LastExecutionTime = DateTime.Now;
            }
        }
        
        /// <summary>
        /// Records a failure of this operation
        /// </summary>
        public void RecordFailure(Exception error = null)
        {
            lock (_lock)
            {
                FailureCount++;
                LastError = error;
                LastExecutionTime = DateTime.Now;
            }
        }
    }
    
    /// <summary>
    /// Timer for a specific operation instance
    /// </summary>
    internal class OperationTimer : IDisposable
    {
        private readonly string _operationName;
        private readonly string _timerId;
        private readonly PerformanceMonitor _monitor;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;
        
        public OperationTimer(string operationName, string timerId, PerformanceMonitor monitor)
        {
            _operationName = operationName;
            _timerId = timerId;
            _monitor = monitor;
            _stopwatch = Stopwatch.StartNew();
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _stopwatch.Stop();
                _monitor.RecordOperation(_operationName, _timerId, _stopwatch.Elapsed);
                _disposed = true;
            }
        }
    }
    
    /// <summary>
    /// Information about a thread being monitored
    /// </summary>
    public class ThreadInfo
    {
        /// <summary>
        /// Thread identifier
        /// </summary>
        public int ThreadId { get; set; }
        
        /// <summary>
        /// User-friendly name of the thread
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Thread category
        /// </summary>
        public ThreadCategory Category { get; set; }
        
        /// <summary>
        /// Current thread status
        /// </summary>
        public ThreadStatus Status { get; set; }
        
        /// <summary>
        /// When the thread started
        /// </summary>
        public DateTime StartTime { get; set; }
        
        /// <summary>
        /// When the thread ended (if completed)
        /// </summary>
        public DateTime? EndTime { get; set; }
        
        /// <summary>
        /// Last time the thread was active
        /// </summary>
        public DateTime LastActive { get; set; }
        
        /// <summary>
        /// Current operation being performed
        /// </summary>
        public string CurrentOperation { get; set; }
    }
    
    /// <summary>
    /// General category of performance metrics
    /// </summary>
    public enum MetricCategory
    {
        NetworkIO,
        DataOperations,
        MapOperations,
        UserInterface,
        ThreadManagement,
        Other
    }
    
    /// <summary>
    /// Categories of threads
    /// </summary>
    public enum ThreadCategory
    {
        UI,
        NetworkIO,
        BackgroundProcessing,
        DataProcessing,
        Other
    }
    
    /// <summary>
    /// Current status of a thread
    /// </summary>
    public enum ThreadStatus
    {
        Running,
        Waiting,
        Idle,
        Blocked,
        Completed
    }
    
    /// <summary>
    /// Event arguments for metrics updates
    /// </summary>
    public class MetricsUpdatedEventArgs : EventArgs
    {
        /// <summary>
        /// The metric that was updated
        /// </summary>
        public PerformanceMetric UpdatedMetric { get; }
        
        public MetricsUpdatedEventArgs(PerformanceMetric metric)
        {
            UpdatedMetric = metric;
        }
    }
}
