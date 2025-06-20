using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Diagnostics
{
    /// <summary>
    /// Specialized logging tool for performance diagnostics and threading issues
    /// </summary>
    public class DiagnosticLogger : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _logFilePath;
        private readonly ConcurrentQueue<DiagnosticEvent> _logQueue = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Timer _flushTimer;
        private readonly int _maxQueueSize;
        private readonly SemaphoreSlim _logFileLock = new(1, 1);
        private Task _processingTask;
        private bool _disposed;

        /// <summary>
        /// Gets the path to the current log file
        /// </summary>
        public string LogFilePath => _logFilePath;
        
        /// <summary>
        /// Gets the current number of entries in the log queue
        /// </summary>
        public int QueueSize => _logQueue.Count;

        /// <summary>
        /// Creates a new instance of DiagnosticLogger
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="logDirectory">Directory to store diagnostic logs</param>
        /// <param name="maxQueueSize">Maximum size of the log queue before forcing a flush</param>
        /// <param name="flushIntervalMs">Interval for automatic flushing in milliseconds</param>
        public DiagnosticLogger(
            ILogger logger,
            string logDirectory = null,
            int maxQueueSize = 1000,
            int flushIntervalMs = 10000)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxQueueSize = maxQueueSize;
            
            // Set up log directory
            logDirectory ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TransportTracker",
                "Diagnostics");

            Directory.CreateDirectory(logDirectory);
            
            // Create log file name with timestamp
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            _logFilePath = Path.Combine(logDirectory, $"diagnostic-{timestamp}.jsonl");
            
            // Start background processing
            _processingTask = Task.Run(ProcessLogQueueAsync);
            
            // Set up flush timer
            _flushTimer = new Timer(
                _ => FlushAsync().ContinueWith(
                    t => _logger.LogError(t.Exception, "Error flushing diagnostic logs"), 
                    TaskContinuationOptions.OnlyOnFaulted),
                null,
                flushIntervalMs,
                flushIntervalMs);
            
            _logger.LogInformation($"Diagnostic logger initialized with log file: {_logFilePath}");
        }

        /// <summary>
        /// Logs a diagnostic event
        /// </summary>
        /// <param name="category">Event category</param>
        /// <param name="eventType">Event type</param>
        /// <param name="message">Event message</param>
        /// <param name="data">Additional event data</param>
        public void LogEvent(
            string category,
            string eventType,
            string message,
            object data = null)
        {
            if (_disposed) return;

            var diagnosticEvent = new DiagnosticEvent
            {
                Timestamp = DateTime.Now,
                Category = category,
                EventType = eventType,
                Message = message,
                Data = data,
                ThreadId = Thread.CurrentThread.ManagedThreadId,
                ThreadName = Thread.CurrentThread.Name
            };
            
            _logQueue.Enqueue(diagnosticEvent);
            
            // If queue size exceeds max, trigger a flush
            if (_logQueue.Count > _maxQueueSize)
            {
                Task.Run(FlushAsync);
            }
        }

        /// <summary>
        /// Logs a threading-related diagnostic event
        /// </summary>
        /// <param name="eventType">Threading event type</param>
        /// <param name="message">Event message</param>
        /// <param name="data">Additional event data</param>
        public void LogThreadingEvent(string eventType, string message, object data = null)
        {
            LogEvent("Threading", eventType, message, data);
        }

        /// <summary>
        /// Logs a performance-related diagnostic event
        /// </summary>
        /// <param name="eventType">Performance event type</param>
        /// <param name="message">Event message</param>
        /// <param name="metrics">Performance metrics data</param>
        public void LogPerformanceEvent(string eventType, string message, object metrics)
        {
            LogEvent("Performance", eventType, message, metrics);
        }

        /// <summary>
        /// Logs an API-related diagnostic event
        /// </summary>
        /// <param name="eventType">API event type</param>
        /// <param name="message">Event message</param>
        /// <param name="data">Additional event data</param>
        public void LogApiEvent(string eventType, string message, object data = null)
        {
            LogEvent("API", eventType, message, data);
        }

        /// <summary>
        /// Logs an error-related diagnostic event
        /// </summary>
        /// <param name="eventType">Error event type</param>
        /// <param name="message">Error message</param>
        /// <param name="exception">Exception object</param>
        public void LogErrorEvent(string eventType, string message, Exception exception)
        {
            LogEvent("Error", eventType, message, new 
            {
                ExceptionType = exception.GetType().Name,
                exception.Message,
                exception.StackTrace,
                InnerException = exception.InnerException?.Message
            });
        }

        /// <summary>
        /// Logs a thread deadlock detection event
        /// </summary>
        /// <param name="message">Deadlock description</param>
        /// <param name="involvedThreads">Information about threads involved</param>
        public void LogDeadlockDetection(string message, IEnumerable<object> involvedThreads)
        {
            LogThreadingEvent("Deadlock", message, new { InvolvedThreads = involvedThreads });
            _logger.LogCritical($"Thread deadlock detected: {message}");
        }

        /// <summary>
        /// Logs excessive thread pool queue length
        /// </summary>
        /// <param name="queueLength">Current queue length</param>
        /// <param name="threshold">Threshold for warning</param>
        public void LogThreadPoolQueueWarning(int queueLength, int threshold)
        {
            LogThreadingEvent("ThreadPoolQueueWarning", 
                $"Thread pool queue length ({queueLength}) exceeded threshold ({threshold})",
                new { QueueLength = queueLength, Threshold = threshold });
            _logger.LogWarning($"Thread pool queue length warning: {queueLength} items queued (threshold: {threshold})");
        }

        /// <summary>
        /// Flushes the log queue to disk
        /// </summary>
        public async Task FlushAsync()
        {
            if (_disposed) return;

            // Take a snapshot of the current queue
            var events = new List<DiagnosticEvent>(_logQueue.Count);
            while (_logQueue.TryDequeue(out var logEvent))
            {
                events.Add(logEvent);
            }
            
            if (events.Count == 0) return;
            
            try
            {
                // Acquire lock to write to file
                await _logFileLock.WaitAsync();
                
                try
                {
                    // Write events to file
                    using var fileStream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                    using var writer = new StreamWriter(fileStream, Encoding.UTF8);
                    
                    foreach (var logEvent in events)
                    {
                        string json = JsonSerializer.Serialize(logEvent);
                        await writer.WriteLineAsync(json);
                    }
                    
                    _logger.LogDebug($"Flushed {events.Count} diagnostic events to log file");
                }
                finally
                {
                    _logFileLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to flush {events.Count} diagnostic events to log");
                
                // Put events back in queue
                foreach (var logEvent in events)
                {
                    _logQueue.Enqueue(logEvent);
                }
            }
        }

        /// <summary>
        /// Background task to process and flush the log queue
        /// </summary>
        private async Task ProcessLogQueueAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    // Only flush if queue exceeds threshold or timer triggers
                    if (_logQueue.Count > _maxQueueSize / 2)
                    {
                        await FlushAsync();
                    }
                    
                    await Task.Delay(1000, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in diagnostic log processing task");
            }
        }

        /// <summary>
        /// Disposes resources used by the logger
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources used by the logger
        /// </summary>
        /// <param name="disposing">True if called from Dispose()</param>
        protected virtual async void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _cts.Cancel();
                _flushTimer.Dispose();
                
                // Final flush of any queued logs
                try
                {
                    await FlushAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during final flush of diagnostic logs");
                }
                
                _logFileLock.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// Represents a diagnostic event to be logged
        /// </summary>
        private class DiagnosticEvent
        {
            /// <summary>
            /// Gets or sets the timestamp of the event
            /// </summary>
            public DateTime Timestamp { get; set; }
            
            /// <summary>
            /// Gets or sets the category of the event
            /// </summary>
            public string Category { get; set; }
            
            /// <summary>
            /// Gets or sets the type of the event
            /// </summary>
            public string EventType { get; set; }
            
            /// <summary>
            /// Gets or sets the message of the event
            /// </summary>
            public string Message { get; set; }
            
            /// <summary>
            /// Gets or sets additional data for the event
            /// </summary>
            public object Data { get; set; }
            
            /// <summary>
            /// Gets or sets the ID of the thread that logged the event
            /// </summary>
            public int ThreadId { get; set; }
            
            /// <summary>
            /// Gets or sets the name of the thread that logged the event
            /// </summary>
            public string ThreadName { get; set; }
        }
    }
}
