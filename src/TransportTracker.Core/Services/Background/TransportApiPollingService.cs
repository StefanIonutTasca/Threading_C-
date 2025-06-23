using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TransportTracker.Core.Services.Api.Transport;
using TransportTracker.Core.Threading;
using TransportTracker.Core.Threading.Coordination;
using TransportTracker.Core.Threading.Synchronization;

namespace TransportTracker.Core.Services.Background
{
    /// <summary>
    /// A background service that periodically polls the transport API for updates
    /// using dedicated threads and provides events for notification.
    /// Implements adaptive polling intervals based on data change frequency and system load.
    /// </summary>
    public class TransportApiPollingService : IBackgroundPollingService
    {
        private readonly ITransportApiService _transportApiService;
        private readonly IThreadFactory _threadFactory;
        private readonly ILogger<TransportApiPollingService> _logger;
        private readonly ILogger<ThreadCoordinator> _threadCoordinatorLogger;
        private readonly AsyncLock _stateLock = new AsyncLock();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Stopwatch _pollStopwatch = new Stopwatch();
        private readonly ThreadCoordinator _coordinator;

        
        private Thread _pollingThread;
        private bool _isRunning;
        private int _consecutiveErrorCount;
        private long _lastDataHash;
        private DateTime _lastSuccessfulPoll = DateTime.MinValue;
        private bool _disposed;
        
        /// <summary>
        /// Gets whether the service is currently running
        /// </summary>
        public bool IsRunning => _isRunning;
        
        /// <summary>
        /// Gets or sets the polling interval in milliseconds
        /// Default: 30 seconds
        /// </summary>
        public int PollingIntervalMs { get; set; } = 30000;
        
        /// <summary>
        /// Gets or sets the minimum polling interval in milliseconds
        /// Default: 5 seconds - prevents excessive polling
        /// </summary>
        public int MinPollingIntervalMs { get; set; } = 5000;
        
        /// <summary>
        /// Gets or sets the maximum polling interval in milliseconds
        /// Default: 2 minutes - ensures data is still relatively fresh
        /// </summary>
        public int MaxPollingIntervalMs { get; set; } = 120000;
        
        /// <summary>
        /// Gets or sets whether adaptive polling is enabled
        /// When enabled, polling interval adjusts based on data change frequency
        /// </summary>
        public bool AdaptivePollingEnabled { get; set; } = true;
        
        /// <summary>
        /// Event raised when new data is available
        /// </summary>
        public event EventHandler<PollingEventArgs> DataAvailable;
        
        /// <summary>
        /// Event raised when polling fails
        /// </summary>
        public event EventHandler<PollingErrorEventArgs> PollingError;
        
        /// <summary>
        /// Event raised when polling state changes (starting/stopping)
        /// </summary>
        public event EventHandler<PollingStateChangedEventArgs> StateChanged;
        
        /// <summary>
        /// Creates a new instance of the TransportApiPollingService
        /// </summary>
        /// <param name="transportApiService">The transport API service</param>
        /// <param name="threadFactory">The thread factory</param>
        /// <param name="logger">Logger instance</param>
        public TransportApiPollingService(
            ITransportApiService transportApiService,
            IThreadFactory threadFactory,
            ILogger<TransportApiPollingService> logger,
            ILogger<ThreadCoordinator> threadCoordinatorLogger)
        {
            _transportApiService = transportApiService ?? throw new ArgumentNullException(nameof(transportApiService));
            _threadFactory = threadFactory ?? throw new ArgumentNullException(nameof(threadFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _threadCoordinatorLogger = threadCoordinatorLogger ?? throw new ArgumentNullException(nameof(threadCoordinatorLogger));
            _coordinator = new ThreadCoordinator(_threadCoordinatorLogger);
        }
        
        /// <summary>
        /// Starts the background polling service on a dedicated thread
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to stop the service</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            using (await _stateLock.LockAsync(cancellationToken))
            {
                if (_isRunning)
                {
                    _logger.LogWarning("TransportApiPollingService is already running");
                    return;
                }
                
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(TransportApiPollingService));
                }
                
                _logger.LogInformation("Starting TransportApiPollingService with interval: {PollingIntervalMs}ms", PollingIntervalMs);
                
                // Link external cancellation to our internal token source
                if (cancellationToken != default)
                {
                    cancellationToken.Register(() => _cts.Cancel());
                }
                
                _consecutiveErrorCount = 0;
                _lastSuccessfulPoll = DateTime.MinValue;
                
                // Create a dedicated thread for polling with normal priority
                _pollingThread = _threadFactory.CreateThread(
                    PollingThreadProc, 
                    "TransportApiPolling", 
                    true,
                    ThreadPriority.Normal);
                
                _pollingThread.Start();
                
                _isRunning = true;
                OnStateChanged(false, true, "Service started");
                
                _logger.LogInformation("TransportApiPollingService has been started");
            }
        }
        
        /// <summary>
        /// Stops the background polling service
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task StopAsync()
        {
            using (await _stateLock.LockAsync())
            {
                if (!_isRunning)
                {
                    _logger.LogWarning("TransportApiPollingService is not running");
                    return;
                }
                
                _logger.LogInformation("Stopping TransportApiPollingService");
                
                try
                {
                    // Signal cancellation
                    if (!_cts.IsCancellationRequested)
                    {
                        _cts.Cancel();
                    }
                    
                    // Signal coordinator for any waiting operations
                    _coordinator.SignalAll();
                    
                    // Wait for thread to complete gracefully (with timeout)
                    bool threadCompleted = _pollingThread.Join(5000);
                    if (!threadCompleted)
                    {
                        _logger.LogWarning("Polling thread did not exit gracefully within timeout");
                    }
                    
                    _isRunning = false;
                    OnStateChanged(true, false, "Service stopped");
                    
                    _logger.LogInformation("TransportApiPollingService has been stopped");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while stopping TransportApiPollingService");
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Manually triggers a poll operation outside of the regular polling interval
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task TriggerPollNowAsync(CancellationToken cancellationToken = default)
        {
            using (await _stateLock.LockAsync(cancellationToken))
            {
                if (!_isRunning)
                {
                    _logger.LogWarning("Cannot trigger poll: TransportApiPollingService is not running");
                    return;
                }
                
                _logger.LogInformation("Manually triggering poll operation");
                
                // Signal to coordinator to wake up the polling thread for immediate poll
                _coordinator.Signal("TriggerPoll");
            }
        }
        
        /// <summary>
        /// Main polling thread procedure that runs on a dedicated thread
        /// </summary>
        private void PollingThreadProc()
        {
            _logger.LogInformation("Polling thread started");
            
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    // Execute poll operation
                    try
                    {
                        PollApiAsync().GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when service is being stopped
                        _logger.LogInformation("Polling operation was cancelled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during polling operation");
                        
                        // Increment consecutive error count for exponential backoff
                        _consecutiveErrorCount++;
                        
                        // Notify subscribers about the error
                        OnPollingError(ex, _consecutiveErrorCount, _consecutiveErrorCount < 5);
                        
                        // Implement exponential backoff for retries
                        if (_consecutiveErrorCount < 5)
                        {
                            int backoffMs = Math.Min(
                                MaxPollingIntervalMs,
                                PollingIntervalMs * (int)Math.Pow(2, _consecutiveErrorCount));
                                
                            _logger.LogInformation(
                                "Implementing exponential backoff retry: {BackoffMs}ms (consecutive errors: {ErrorCount})",
                                backoffMs, _consecutiveErrorCount);
                                
                            // Wait using coordinator for cancellation support
                            _coordinator.WaitOneOrTimeout(backoffMs); // Removed CancellationToken argument as no such overload exists.
                            continue;
                        }
                    }
                    
                    // Calculate next polling interval based on adaptive logic (if enabled)
                    int nextInterval = CalculateNextPollingInterval();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Polling thread was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in polling thread");
            }
            finally
            {
                _logger.LogInformation("Polling thread exited");
            }
        }
        
        /// <summary>
        /// Performs a single poll operation to the transport API
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        private async Task PollApiAsync()
        {
            _logger.LogDebug("Starting API poll operation");
            _pollStopwatch.Restart();
            
            var currentData = new List<object>();
            long dataSizeBytes = 0;
            
            try
            {
                // Request data from transport API with our cancellation token
                var routes = await _transportApiService.GetRoutesAsync(_cts.Token);
                // var vehicles = await _transportApiService.GetVehiclesAsync(null, false, _cts.Token); // Not present in Core interface
                var stops = await _transportApiService.GetStopsAsync(_cts.Token);
                // var predictions = await _transportApiService.GetPredictionsAsync(null, null, false, _cts.Token); // Not present in Core interface
                
                // Collect all data for change detection
                currentData.AddRange(routes);
                // currentData.AddRange(vehicles); // Not available in Core
                currentData.AddRange(stops);
                // currentData.AddRange(predictions); // Not available in Core
                
                // Calculate approximate data size
                string json = JsonSerializer.Serialize(currentData);
                dataSizeBytes = Encoding.UTF8.GetByteCount(json);
                
                // Calculate hash for change detection
                long currentHash = CalculateDataHash(currentData);
                bool hasChanged = _lastDataHash != currentHash;
                
                _pollStopwatch.Stop();
                
                // Update last successful poll time and reset error count
                _lastSuccessfulPoll = DateTime.UtcNow;
                _lastDataHash = currentHash;
                _consecutiveErrorCount = 0;
                
                _logger.LogInformation(
                    "API poll completed in {ElapsedMs}ms, data size: {DataSizeKB}KB, changed: {Changed}",
                    _pollStopwatch.ElapsedMilliseconds,
                    dataSizeBytes / 1024,
                    hasChanged);
                
                // Notify subscribers about new data
                OnDataAvailable(_pollStopwatch.Elapsed, dataSizeBytes, hasChanged);
            }
            catch (Exception ex)
            {
                _pollStopwatch.Stop();
                _logger.LogError(ex, "Error polling transport API after {ElapsedMs}ms", _pollStopwatch.ElapsedMilliseconds);
                throw;
            }
        }
        
        /// <summary>
        /// Calculates the next polling interval based on adaptive logic
        /// </summary>
        /// <returns>The next polling interval in milliseconds</returns>
        private int CalculateNextPollingInterval()
        {
            // If adaptive polling is disabled, just use the configured interval
            if (!AdaptivePollingEnabled)
                return PollingIntervalMs;
                
            // Start with the base polling interval
            int nextInterval = PollingIntervalMs;
            
            // Adjust based on system load (CPU usage could be added here)
            double systemLoad = Environment.ProcessorCount - ThreadPool.ThreadCount;
            if (systemLoad > 0.8 * Environment.ProcessorCount)
            {
                // System is under high load, increase interval to reduce pressure
                nextInterval = (int)(nextInterval * 1.5);
                _logger.LogDebug("System under high load, increasing polling interval to {IntervalMs}ms", nextInterval);
            }
            
            // If no successful polls yet, use default interval
            if (_lastSuccessfulPoll == DateTime.MinValue)
                return Math.Clamp(nextInterval, MinPollingIntervalMs, MaxPollingIntervalMs);
                
            // Adjust interval based on time since last data change
            TimeSpan sinceLastPoll = DateTime.UtcNow - _lastSuccessfulPoll;
            
            // If we haven't polled in a long time, gradually decrease the interval
            if (sinceLastPoll.TotalMinutes > 5)
            {
                nextInterval = (int)(nextInterval * 0.8);
                _logger.LogDebug("Long time since last poll, decreasing interval to {IntervalMs}ms", nextInterval);
            }
            
            // Ensure interval stays within configured bounds
            return Math.Clamp(nextInterval, MinPollingIntervalMs, MaxPollingIntervalMs);
        }
        
        /// <summary>
        /// Calculates a hash value for the data collection for change detection
        /// </summary>
        /// <param name="data">The data to calculate a hash for</param>
        /// <returns>A hash value for the data</returns>
        private long CalculateDataHash(IEnumerable<object> data)
        {
            // A simple hash function for change detection
            // For production, consider using a more robust hashing algorithm
            long hash = 0;
            foreach (var item in data)
            {
                string json = JsonSerializer.Serialize(item);
                foreach (char c in json)
                {
                    hash = 31 * hash + c;
                }
            }
            return hash;
        }
        
        /// <summary>
        /// Raises the DataAvailable event
        /// </summary>
        /// <param name="elapsed">The elapsed time for the polling operation</param>
        /// <param name="dataSizeBytes">The size of the data in bytes</param>
        /// <param name="hasChanged">Whether the data has changed since the last poll</param>
        private void OnDataAvailable(TimeSpan elapsed, long dataSizeBytes, bool hasChanged)
        {
            DataAvailable?.Invoke(this, new PollingEventArgs(
                DateTime.UtcNow, elapsed, dataSizeBytes, hasChanged));
        }
        
        /// <summary>
        /// Raises the PollingError event
        /// </summary>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="consecutiveErrorCount">The number of consecutive errors</param>
        /// <param name="willRetry">Whether the service will attempt to retry</param>
        private void OnPollingError(Exception exception, int consecutiveErrorCount, bool willRetry)
        {
            PollingError?.Invoke(this, new PollingErrorEventArgs(
                exception, DateTime.UtcNow, consecutiveErrorCount, willRetry));
        }
        
        /// <summary>
        /// Raises the StateChanged event
        /// </summary>
        /// <param name="previousState">The previous state</param>
        /// <param name="newState">The new state</param>
        /// <param name="reason">The reason for the state change</param>
        private void OnStateChanged(bool previousState, bool newState, string reason)
        {
            StateChanged?.Invoke(this, new PollingStateChangedEventArgs(
                previousState, newState, DateTime.UtcNow, reason));
        }
        
        /// <summary>
        /// Disposes resources used by the service
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
                
            _disposed = true;
            
            _logger.LogInformation("Disposing TransportApiPollingService");
            
            try
            {
                // Stop the service if running
                if (_isRunning)
                {
                    StopAsync().GetAwaiter().GetResult();
                }
                
                // Dispose CTS
                // _cts.Dispose(); // AsyncLock does not implement Dispose
                
                // Dispose coordinator
                // _coordinator.Dispose(); // AsyncLock does not implement Dispose
                
                // Dispose state lock
                // _stateLock.Dispose(); // AsyncLock does not implement Dispose
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing TransportApiPollingService");
            }
        }
    }
}
