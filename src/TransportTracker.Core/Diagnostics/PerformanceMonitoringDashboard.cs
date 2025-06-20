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
    /// Dashboard for monitoring application performance and threading metrics
    /// </summary>
    public class PerformanceMonitoringDashboard : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ThreadingMetricsCollector _metricsCollector;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<string, PerformanceCounter> _counters = new();
        private Task _monitoringTask;
        private bool _disposed;

        /// <summary>
        /// Gets or sets the monitoring interval in milliseconds
        /// </summary>
        public int MonitoringIntervalMs { get; set; } = 5000;

        /// <summary>
        /// Gets the list of available performance metrics
        /// </summary>
        public IEnumerable<string> AvailableMetrics => _counters.Keys.ToList();

        /// <summary>
        /// Event raised when performance metrics are updated
        /// </summary>
        public event EventHandler<PerformanceMetricsEventArgs> MetricsUpdated;

        /// <summary>
        /// Creates a new instance of PerformanceMonitoringDashboard
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="metricsCollector">Threading metrics collector</param>
        public PerformanceMonitoringDashboard(ILogger logger, ThreadingMetricsCollector metricsCollector)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
        }

        /// <summary>
        /// Starts the performance monitoring dashboard
        /// </summary>
        public void Start()
        {
            if (_monitoringTask != null) return;

            _logger.LogInformation("Starting performance monitoring dashboard");

            try
            {
                // Initialize performance counters
                InitializePerformanceCounters();

                // Start metrics collector if not already running
                _metricsCollector.Start();

                // Start monitoring task
                _monitoringTask = Task.Run(MonitorPerformanceAsync);

                _logger.LogInformation("Performance monitoring dashboard started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start performance monitoring dashboard");
            }
        }

        /// <summary>
        /// Stops the performance monitoring dashboard
        /// </summary>
        public void Stop()
        {
            if (_monitoringTask == null) return;

            _logger.LogInformation("Stopping performance monitoring dashboard");

            // Cancel monitoring task
            _cts.Cancel();
            _monitoringTask = null;

            _logger.LogInformation("Performance monitoring dashboard stopped");
        }

        /// <summary>
        /// Gets the current value of a performance metric
        /// </summary>
        /// <param name="metricName">Name of the metric</param>
        /// <returns>Current value of the metric or null if not found</returns>
        public float? GetMetricValue(string metricName)
        {
            if (_counters.TryGetValue(metricName, out var counter))
            {
                try
                {
                    return counter.NextValue();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to get value for metric '{metricName}'");
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the historical values for a specific metric
        /// </summary>
        /// <param name="metricName">Name of the metric</param>
        /// <returns>Historical data points for the metric</returns>
        public IEnumerable<MetricDataPoint> GetMetricHistory(string metricName)
        {
            return _metricsCollector.GetHistoricalMetrics(metricName);
        }

        /// <summary>
        /// Gets the current thread utilization metrics
        /// </summary>
        /// <returns>Thread utilization snapshot</returns>
        public ThreadUtilizationSnapshot GetThreadUtilization()
        {
            // Get thread pool metrics
            ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);

            int busyWorkerThreads = maxWorkerThreads - workerThreads;
            int busyCompletionPortThreads = maxCompletionPortThreads - completionPortThreads;

            // Get process threads
            int totalProcessThreads = Process.GetCurrentProcess().Threads.Count;

            // Get CPU usage if available
            float cpuUsage = 0;
            if (_counters.TryGetValue("Processor_TotalProcessorTime", out var cpuCounter))
            {
                try
                {
                    cpuUsage = cpuCounter.NextValue();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get CPU usage");
                }
            }

            return new ThreadUtilizationSnapshot(
                DateTime.Now,
                busyWorkerThreads,
                maxWorkerThreads,
                busyCompletionPortThreads,
                maxCompletionPortThreads,
                totalProcessThreads,
                cpuUsage);
        }

        /// <summary>
        /// Gets detailed operation metrics
        /// </summary>
        /// <returns>Collection of operation metrics</returns>
        public IEnumerable<OperationMetricsSnapshot> GetOperationMetrics()
        {
            return _metricsCollector.GetOperationMetrics();
        }

        /// <summary>
        /// Gets detailed thread metrics
        /// </summary>
        /// <returns>Collection of thread metrics</returns>
        public IEnumerable<ThreadMetricsSnapshot> GetThreadMetrics()
        {
            return _metricsCollector.GetThreadMetrics();
        }

        /// <summary>
        /// Initializes the performance counters for monitoring
        /// </summary>
        private void InitializePerformanceCounters()
        {
            try
            {
                // Process performance counters
                AddCounter("Process_CPUUsage", "Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
                AddCounter("Process_WorkingSet", "Process", "Working Set", Process.GetCurrentProcess().ProcessName);
                AddCounter("Process_PrivateBytes", "Process", "Private Bytes", Process.GetCurrentProcess().ProcessName);
                AddCounter("Process_ThreadCount", "Process", "Thread Count", Process.GetCurrentProcess().ProcessName);

                // Memory counters
                AddCounter("Memory_AvailableBytes", "Memory", "Available Bytes");
                AddCounter("Memory_CommittedBytes", "Memory", "Committed Bytes");
                AddCounter("Memory_PageFaults", "Memory", "Page Faults/sec");

                // Processor counters
                AddCounter("Processor_TotalProcessorTime", "Processor", "% Processor Time", "_Total");
                AddCounter("Processor_IdleTime", "Processor", "% Idle Time", "_Total");

                // .NET CLR counters
                AddCounter(".NET_TotalCommittedBytes", ".NET CLR Memory", "# Total committed Bytes");
                AddCounter(".NET_GCTime", ".NET CLR Memory", "% Time in GC");
                AddCounter(".NET_ExceptionsPerSec", ".NET CLR Exceptions", "# of Exceps Thrown / sec");
                AddCounter(".NET_ThreadsInUse", ".NET CLR LocksAndThreads", "# of current logical Threads");

                _logger.LogInformation($"Initialized {_counters.Count} performance counters for monitoring");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize some performance counters. Limited metrics will be available.");
            }
        }

        /// <summary>
        /// Adds a performance counter if available
        /// </summary>
        /// <param name="name">Metric name</param>
        /// <param name="category">Counter category</param>
        /// <param name="counter">Counter name</param>
        /// <param name="instance">Counter instance (optional)</param>
        private void AddCounter(string name, string category, string counter, string instance = null)
        {
            try
            {
                PerformanceCounter perfCounter;

                if (string.IsNullOrEmpty(instance))
                {
                    perfCounter = new PerformanceCounter(category, counter, readOnly: true);
                }
                else
                {
                    perfCounter = new PerformanceCounter(category, counter, instance, readOnly: true);
                }

                // Get initial value to ensure counter is valid
                perfCounter.NextValue();

                _counters[name] = perfCounter;
                _logger.LogDebug($"Added performance counter: {name} ({category}/{counter}/{instance ?? "n/a"})");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to add performance counter: {name} ({category}/{counter}/{instance ?? "n/a"})");
            }
        }

        /// <summary>
        /// Background task to monitor performance metrics
        /// </summary>
        private async Task MonitorPerformanceAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    CollectAndReportMetrics();
                    await Task.Delay(MonitoringIntervalMs, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in performance monitoring");
            }
        }

        /// <summary>
        /// Collects and reports the current performance metrics
        /// </summary>
        private void CollectAndReportMetrics()
        {
            try
            {
                // Get thread utilization
                var threadUtilization = GetThreadUtilization();

                // Get performance counter values
                var metrics = new Dictionary<string, float>();
                foreach (var counter in _counters)
                {
                    try
                    {
                        metrics[counter.Key] = counter.Value.NextValue();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex, $"Failed to get value for metric '{counter.Key}'");
                    }
                }

                // Get operation metrics
                var operationMetrics = GetOperationMetrics().ToList();

                // Log summary of metrics
                _logger.LogDebug(
                    $"Performance metrics: CPU {metrics.GetValueOrDefault("Processor_TotalProcessorTime"):F1}%, " +
                    $"Memory {metrics.GetValueOrDefault("Process_WorkingSet") / 1024 / 1024:F1}MB, " +
                    $"Threads {threadUtilization.TotalProcessThreads} " +
                    $"(Pool: {threadUtilization.BusyWorkerThreads}/{threadUtilization.MaxWorkerThreads} worker, " +
                    $"{threadUtilization.BusyIOThreads}/{threadUtilization.MaxIOThreads} IO)");

                // Raise event with collected metrics
                MetricsUpdated?.Invoke(this, new PerformanceMetricsEventArgs(
                    threadUtilization,
                    metrics,
                    operationMetrics));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect and report metrics");
            }
        }

        /// <summary>
        /// Disposes resources used by the dashboard
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources used by the dashboard
        /// </summary>
        /// <param name="disposing">True if called from Dispose()</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                Stop();
                _cts.Dispose();

                // Dispose performance counters
                foreach (var counter in _counters.Values)
                {
                    counter.Dispose();
                }
                _counters.Clear();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Event arguments for performance metrics updates
    /// </summary>
    public class PerformanceMetricsEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the thread utilization snapshot
        /// </summary>
        public ThreadUtilizationSnapshot ThreadUtilization { get; }
        
        /// <summary>
        /// Gets the performance metrics values
        /// </summary>
        public IReadOnlyDictionary<string, float> Metrics { get; }
        
        /// <summary>
        /// Gets the operation metrics snapshots
        /// </summary>
        public IReadOnlyList<OperationMetricsSnapshot> OperationMetrics { get; }

        /// <summary>
        /// Creates a new instance of PerformanceMetricsEventArgs
        /// </summary>
        public PerformanceMetricsEventArgs(
            ThreadUtilizationSnapshot threadUtilization,
            IReadOnlyDictionary<string, float> metrics,
            IReadOnlyList<OperationMetricsSnapshot> operationMetrics)
        {
            ThreadUtilization = threadUtilization;
            Metrics = metrics;
            OperationMetrics = operationMetrics;
        }
    }

    /// <summary>
    /// Snapshot of thread utilization metrics
    /// </summary>
    public class ThreadUtilizationSnapshot
    {
        /// <summary>
        /// Gets the timestamp of the snapshot
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// Gets the number of busy worker threads in the thread pool
        /// </summary>
        public int BusyWorkerThreads { get; }
        
        /// <summary>
        /// Gets the maximum number of worker threads in the thread pool
        /// </summary>
        public int MaxWorkerThreads { get; }
        
        /// <summary>
        /// Gets the number of busy I/O threads in the thread pool
        /// </summary>
        public int BusyIOThreads { get; }
        
        /// <summary>
        /// Gets the maximum number of I/O threads in the thread pool
        /// </summary>
        public int MaxIOThreads { get; }
        
        /// <summary>
        /// Gets the total number of threads in the process
        /// </summary>
        public int TotalProcessThreads { get; }
        
        /// <summary>
        /// Gets the CPU usage percentage
        /// </summary>
        public float CpuUsagePercentage { get; }
        
        /// <summary>
        /// Gets the thread pool worker utilization percentage
        /// </summary>
        public float WorkerThreadUtilizationPercentage => 
            MaxWorkerThreads > 0 ? (float)BusyWorkerThreads / MaxWorkerThreads * 100 : 0;
            
        /// <summary>
        /// Gets the thread pool I/O utilization percentage
        /// </summary>
        public float IOThreadUtilizationPercentage => 
            MaxIOThreads > 0 ? (float)BusyIOThreads / MaxIOThreads * 100 : 0;

        /// <summary>
        /// Creates a new instance of ThreadUtilizationSnapshot
        /// </summary>
        public ThreadUtilizationSnapshot(
            DateTime timestamp,
            int busyWorkerThreads,
            int maxWorkerThreads,
            int busyIOThreads,
            int maxIOThreads,
            int totalProcessThreads,
            float cpuUsagePercentage)
        {
            Timestamp = timestamp;
            BusyWorkerThreads = busyWorkerThreads;
            MaxWorkerThreads = maxWorkerThreads;
            BusyIOThreads = busyIOThreads;
            MaxIOThreads = maxIOThreads;
            TotalProcessThreads = totalProcessThreads;
            CpuUsagePercentage = cpuUsagePercentage;
        }
    }
}
