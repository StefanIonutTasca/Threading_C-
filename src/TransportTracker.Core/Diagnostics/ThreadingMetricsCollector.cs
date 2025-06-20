using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Diagnostics
{
    /// <summary>
    /// Collects and analyzes metrics related to threading and performance
    /// </summary>
    public class ThreadingMetricsCollector : IDisposable
    {
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<string, ThreadMetricsEntry> _threadMetrics = new();
        private readonly ConcurrentDictionary<string, OperationMetricsEntry> _operationMetrics = new();
        private readonly ConcurrentDictionary<string, HistoricalMetricsEntry> _historicalMetrics = new();
        private Task _collectionTask;
        private bool _disposed;

        /// <summary>
        /// Gets or sets the collection interval in milliseconds
        /// </summary>
        public int CollectionIntervalMs { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the maximum history length for metrics collection
        /// </summary>
        public int MaxHistoryLength { get; set; } = 100;

        /// <summary>
        /// Creates a new instance of ThreadingMetricsCollector
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public ThreadingMetricsCollector(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts collecting threading metrics
        /// </summary>
        public void Start()
        {
            if (_collectionTask != null) return;

            _logger.LogInformation("Starting threading metrics collection");

            // Start background collection task
            _collectionTask = Task.Run(CollectMetricsAsync);
        }

        /// <summary>
        /// Stops collecting threading metrics
        /// </summary>
        public void Stop()
        {
            if (_collectionTask == null) return;

            _logger.LogInformation("Stopping threading metrics collection");

            // Cancel collection task
            _cts.Cancel();
            _collectionTask = null;
        }

        /// <summary>
        /// Tracks the execution of an operation and records its performance metrics
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="action">Action to execute</param>
        public void TrackOperation(string operationName, Action action)
        {
            if (string.IsNullOrEmpty(operationName)) throw new ArgumentNullException(nameof(operationName));
            if (action == null) throw new ArgumentNullException(nameof(action));

            var stopwatch = Stopwatch.StartNew();
            string threadName = Thread.CurrentThread.Name ?? $"Thread_{Thread.CurrentThread.ManagedThreadId}";

            try
            {
                action();
            }
            finally
            {
                stopwatch.Stop();
                RecordOperationMetrics(operationName, threadName, stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Tracks the execution of an async operation and records its performance metrics
        /// </summary>
        /// <typeparam name="T">Type of result</typeparam>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="func">Async function to execute</param>
        /// <returns>Result of the operation</returns>
        public async Task<T> TrackOperationAsync<T>(string operationName, Func<Task<T>> func)
        {
            if (string.IsNullOrEmpty(operationName)) throw new ArgumentNullException(nameof(operationName));
            if (func == null) throw new ArgumentNullException(nameof(func));

            var stopwatch = Stopwatch.StartNew();
            string initialThreadName = Thread.CurrentThread.Name ?? $"Thread_{Thread.CurrentThread.ManagedThreadId}";

            try
            {
                return await func();
            }
            finally
            {
                stopwatch.Stop();
                string finalThreadName = Thread.CurrentThread.Name ?? $"Thread_{Thread.CurrentThread.ManagedThreadId}";
                RecordOperationMetrics(operationName, $"{initialThreadName}->{finalThreadName}", stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Gets current threading metrics
        /// </summary>
        /// <returns>Collection of thread metrics</returns>
        public IEnumerable<ThreadMetricsSnapshot> GetThreadMetrics()
        {
            return _threadMetrics.Values.Select(m => m.CreateSnapshot()).ToList();
        }

        /// <summary>
        /// Gets current operation performance metrics
        /// </summary>
        /// <returns>Collection of operation metrics</returns>
        public IEnumerable<OperationMetricsSnapshot> GetOperationMetrics()
        {
            return _operationMetrics.Values.Select(m => m.CreateSnapshot()).ToList();
        }

        /// <summary>
        /// Gets historical metrics for a specific metric name
        /// </summary>
        /// <param name="metricName">Name of the metric</param>
        /// <returns>Historical data for the metric</returns>
        public IEnumerable<MetricDataPoint> GetHistoricalMetrics(string metricName)
        {
            if (_historicalMetrics.TryGetValue(metricName, out var entry))
            {
                return entry.DataPoints.ToList();
            }

            return Enumerable.Empty<MetricDataPoint>();
        }

        /// <summary>
        /// Records metrics for a completed operation
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="threadName">Name of the thread that executed the operation</param>
        /// <param name="durationMs">Duration in milliseconds</param>
        private void RecordOperationMetrics(string operationName, string threadName, long durationMs)
        {
            var entry = _operationMetrics.GetOrAdd(operationName, _ => new OperationMetricsEntry(operationName));
            entry.RecordExecution(threadName, durationMs);

            // Add to historical metrics
            string metricName = $"Operation_{operationName}_Duration";
            AddHistoricalDataPoint(metricName, durationMs);

            _logger.LogDebug(
                $"Operation {operationName} completed on thread {threadName} in {durationMs}ms " +
                $"(avg: {entry.AverageDurationMs:F2}ms, min: {entry.MinDurationMs}ms, max: {entry.MaxDurationMs}ms)");
        }

        /// <summary>
        /// Adds a data point to the historical metrics
        /// </summary>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="value">Value of the metric</param>
        private void AddHistoricalDataPoint(string metricName, long value)
        {
            var entry = _historicalMetrics.GetOrAdd(metricName, _ => new HistoricalMetricsEntry(metricName, MaxHistoryLength));
            entry.AddDataPoint(value);
        }

        /// <summary>
        /// Background task to collect system-wide threading metrics
        /// </summary>
        private async Task CollectMetricsAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    CollectThreadPoolMetrics();
                    CollectActiveThreadsMetrics();

                    await Task.Delay(CollectionIntervalMs, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in threading metrics collection");
            }
        }

        /// <summary>
        /// Collects ThreadPool metrics
        /// </summary>
        private void CollectThreadPoolMetrics()
        {
            ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);

            int busyWorkerThreads = maxWorkerThreads - workerThreads;
            int busyCompletionPortThreads = maxCompletionPortThreads - completionPortThreads;

            AddHistoricalDataPoint("ThreadPool_WorkerThreads_Active", busyWorkerThreads);
            AddHistoricalDataPoint("ThreadPool_CompletionPortThreads_Active", busyCompletionPortThreads);
            AddHistoricalDataPoint("ThreadPool_WorkerThreads_Available", workerThreads);
            AddHistoricalDataPoint("ThreadPool_CompletionPortThreads_Available", completionPortThreads);

            // Calculate utilization percentages
            double workerUtilization = (double)busyWorkerThreads / maxWorkerThreads * 100;
            double ioUtilization = (double)busyCompletionPortThreads / maxCompletionPortThreads * 100;

            AddHistoricalDataPoint("ThreadPool_WorkerThreads_Utilization", (long)workerUtilization);
            AddHistoricalDataPoint("ThreadPool_CompletionPortThreads_Utilization", (long)ioUtilization);

            _logger.LogDebug(
                $"Thread pool metrics: Worker threads {busyWorkerThreads}/{maxWorkerThreads} " +
                $"({workerUtilization:F1}%), IO threads {busyCompletionPortThreads}/{maxCompletionPortThreads} " +
                $"({ioUtilization:F1}%)");
        }

        /// <summary>
        /// Collects metrics about active threads
        /// </summary>
        private void CollectActiveThreadsMetrics()
        {
            // Get current process
            var process = Process.GetCurrentProcess();

            // Get total thread count
            int totalThreads = process.Threads.Count;
            AddHistoricalDataPoint("Process_TotalThreads", totalThreads);

            _logger.LogDebug($"Process has {totalThreads} total threads");
        }

        /// <summary>
        /// Disposes resources used by the collector
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources used by the collector
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

        #region Metrics Entry Classes

        /// <summary>
        /// Represents historical data for a specific metric
        /// </summary>
        private class HistoricalMetricsEntry
        {
            private readonly Queue<MetricDataPoint> _dataPoints;
            private readonly int _maxLength;

            public string MetricName { get; }
            public IEnumerable<MetricDataPoint> DataPoints => _dataPoints.ToArray();

            public HistoricalMetricsEntry(string metricName, int maxLength)
            {
                MetricName = metricName;
                _maxLength = maxLength;
                _dataPoints = new Queue<MetricDataPoint>(maxLength);
            }

            public void AddDataPoint(long value)
            {
                var dataPoint = new MetricDataPoint(DateTime.Now, value);

                // Add new data point
                lock (_dataPoints)
                {
                    _dataPoints.Enqueue(dataPoint);

                    // Remove oldest if exceeding max length
                    if (_dataPoints.Count > _maxLength)
                    {
                        _dataPoints.Dequeue();
                    }
                }
            }
        }

        /// <summary>
        /// Represents metrics for a specific thread
        /// </summary>
        private class ThreadMetricsEntry
        {
            private long _totalOperations;
            private long _totalDurationMs;

            public string ThreadName { get; }
            public DateTime FirstSeen { get; }
            public DateTime LastActive { get; private set; }
            public long TotalOperations => _totalOperations;
            public long TotalDurationMs => _totalDurationMs;
            
            public double AverageOperationTimeMs => 
                _totalOperations > 0 ? (double)_totalDurationMs / _totalOperations : 0;

            public ThreadMetricsEntry(string threadName)
            {
                ThreadName = threadName;
                FirstSeen = DateTime.Now;
                LastActive = DateTime.Now;
            }

            public void RecordOperation(long durationMs)
            {
                Interlocked.Increment(ref _totalOperations);
                Interlocked.Add(ref _totalDurationMs, durationMs);
                LastActive = DateTime.Now;
            }

            public ThreadMetricsSnapshot CreateSnapshot()
            {
                return new ThreadMetricsSnapshot(
                    ThreadName,
                    FirstSeen,
                    LastActive,
                    _totalOperations,
                    _totalDurationMs,
                    AverageOperationTimeMs);
            }
        }

        /// <summary>
        /// Represents metrics for a specific operation type
        /// </summary>
        private class OperationMetricsEntry
        {
            private long _totalExecutions;
            private long _totalDurationMs;
            private long _minDurationMs = long.MaxValue;
            private long _maxDurationMs;
            private readonly object _lock = new();
            private readonly Dictionary<string, long> _executionsByThread = new();

            public string OperationName { get; }
            public long TotalExecutions => _totalExecutions;
            public long TotalDurationMs => _totalDurationMs;
            public double AverageDurationMs => 
                _totalExecutions > 0 ? (double)_totalDurationMs / _totalExecutions : 0;
            public long MinDurationMs => _minDurationMs;
            public long MaxDurationMs => _maxDurationMs;
            public IReadOnlyDictionary<string, long> ExecutionsByThread 
            {
                get
                {
                    lock (_lock)
                    {
                        return new Dictionary<string, long>(_executionsByThread);
                    }
                }
            }

            public OperationMetricsEntry(string operationName)
            {
                OperationName = operationName;
            }

            public void RecordExecution(string threadName, long durationMs)
            {
                Interlocked.Increment(ref _totalExecutions);
                Interlocked.Add(ref _totalDurationMs, durationMs);

                lock (_lock)
                {
                    // Update min duration
                    if (durationMs < _minDurationMs)
                    {
                        _minDurationMs = durationMs;
                    }

                    // Update max duration
                    if (durationMs > _maxDurationMs)
                    {
                        _maxDurationMs = durationMs;
                    }

                    // Record execution by thread
                    if (_executionsByThread.ContainsKey(threadName))
                    {
                        _executionsByThread[threadName]++;
                    }
                    else
                    {
                        _executionsByThread[threadName] = 1;
                    }
                }
            }

            public OperationMetricsSnapshot CreateSnapshot()
            {
                return new OperationMetricsSnapshot(
                    OperationName,
                    _totalExecutions,
                    _totalDurationMs,
                    AverageDurationMs,
                    _minDurationMs == long.MaxValue ? 0 : _minDurationMs,
                    _maxDurationMs,
                    ExecutionsByThread);
            }
        }

        #endregion
    }

    #region Metrics DTOs

    /// <summary>
    /// Represents a snapshot of thread metrics
    /// </summary>
    public class ThreadMetricsSnapshot
    {
        /// <summary>
        /// Gets the thread name or ID
        /// </summary>
        public string ThreadName { get; }
        
        /// <summary>
        /// Gets when the thread was first seen
        /// </summary>
        public DateTime FirstSeen { get; }
        
        /// <summary>
        /// Gets when the thread was last active
        /// </summary>
        public DateTime LastActive { get; }
        
        /// <summary>
        /// Gets the total operations performed by the thread
        /// </summary>
        public long TotalOperations { get; }
        
        /// <summary>
        /// Gets the total duration of all operations in milliseconds
        /// </summary>
        public long TotalDurationMs { get; }
        
        /// <summary>
        /// Gets the average operation time in milliseconds
        /// </summary>
        public double AverageOperationTimeMs { get; }

        /// <summary>
        /// Creates a new ThreadMetricsSnapshot
        /// </summary>
        public ThreadMetricsSnapshot(
            string threadName,
            DateTime firstSeen,
            DateTime lastActive,
            long totalOperations,
            long totalDurationMs,
            double averageOperationTimeMs)
        {
            ThreadName = threadName;
            FirstSeen = firstSeen;
            LastActive = lastActive;
            TotalOperations = totalOperations;
            TotalDurationMs = totalDurationMs;
            AverageOperationTimeMs = averageOperationTimeMs;
        }
    }

    /// <summary>
    /// Represents a snapshot of operation metrics
    /// </summary>
    public class OperationMetricsSnapshot
    {
        /// <summary>
        /// Gets the operation name
        /// </summary>
        public string OperationName { get; }
        
        /// <summary>
        /// Gets the total number of executions
        /// </summary>
        public long TotalExecutions { get; }
        
        /// <summary>
        /// Gets the total duration of all executions in milliseconds
        /// </summary>
        public long TotalDurationMs { get; }
        
        /// <summary>
        /// Gets the average duration per execution in milliseconds
        /// </summary>
        public double AverageDurationMs { get; }
        
        /// <summary>
        /// Gets the minimum duration of any execution in milliseconds
        /// </summary>
        public long MinDurationMs { get; }
        
        /// <summary>
        /// Gets the maximum duration of any execution in milliseconds
        /// </summary>
        public long MaxDurationMs { get; }
        
        /// <summary>
        /// Gets the count of executions by thread name
        /// </summary>
        public IReadOnlyDictionary<string, long> ExecutionsByThread { get; }

        /// <summary>
        /// Creates a new OperationMetricsSnapshot
        /// </summary>
        public OperationMetricsSnapshot(
            string operationName,
            long totalExecutions,
            long totalDurationMs,
            double averageDurationMs,
            long minDurationMs,
            long maxDurationMs,
            IReadOnlyDictionary<string, long> executionsByThread)
        {
            OperationName = operationName;
            TotalExecutions = totalExecutions;
            TotalDurationMs = totalDurationMs;
            AverageDurationMs = averageDurationMs;
            MinDurationMs = minDurationMs;
            MaxDurationMs = maxDurationMs;
            ExecutionsByThread = executionsByThread;
        }
    }

    /// <summary>
    /// Represents a single data point for a metric over time
    /// </summary>
    public class MetricDataPoint
    {
        /// <summary>
        /// Gets the timestamp when the metric was recorded
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// Gets the value of the metric
        /// </summary>
        public long Value { get; }

        /// <summary>
        /// Creates a new MetricDataPoint
        /// </summary>
        public MetricDataPoint(DateTime timestamp, long value)
        {
            Timestamp = timestamp;
            Value = value;
        }
    }

    #endregion
}
