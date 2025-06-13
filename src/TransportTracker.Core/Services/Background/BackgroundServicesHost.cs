using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Services.Background
{
    /// <summary>
    /// Manages the lifecycle of all background services in the application.
    /// Ensures orderly startup and shutdown of background processes.
    /// </summary>
    public class BackgroundServicesHost : IDisposable
    {
        private readonly ILogger<BackgroundServicesHost> _logger;
        private readonly IEnumerable<IBackgroundPollingService> _pollingServices;
        private readonly SemaphoreSlim _controlLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isRunning;
        private bool _disposed;
        
        /// <summary>
        /// Gets whether the background services host is running
        /// </summary>
        public bool IsRunning => _isRunning;
        
        /// <summary>
        /// Creates a new instance of the BackgroundServicesHost
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="pollingServices">All registered background polling services</param>
        public BackgroundServicesHost(
            ILogger<BackgroundServicesHost> logger,
            IEnumerable<IBackgroundPollingService> pollingServices)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pollingServices = pollingServices ?? throw new ArgumentNullException(nameof(pollingServices));
        }
        
        /// <summary>
        /// Starts all registered background services
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task StartAllServicesAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BackgroundServicesHost));
                
            await _controlLock.WaitAsync(cancellationToken);
            
            try
            {
                if (_isRunning)
                {
                    _logger.LogWarning("Background services are already running");
                    return;
                }
                
                _logger.LogInformation("Starting all background services");
                
                // Link external token to our cancellation source
                if (cancellationToken != default)
                {
                    cancellationToken.Register(() => _cts.Cancel());
                }
                
                var startTasks = new List<Task>();
                int serviceCount = 0;
                
                // Start each polling service with our cancellation token
                foreach (var service in _pollingServices)
                {
                    startTasks.Add(StartServiceWithRetryAsync(service, _cts.Token));
                    serviceCount++;
                }
                
                // Wait for all services to start (or fail)
                await Task.WhenAll(startTasks);
                
                _isRunning = true;
                _logger.LogInformation("Started {Count} background services", serviceCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start all background services");
                
                // Try to stop any services that may have started
                await StopAllServicesAsync();
                throw;
            }
            finally
            {
                _controlLock.Release();
            }
        }
        
        /// <summary>
        /// Stops all registered background services
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task StopAllServicesAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BackgroundServicesHost));
                
            await _controlLock.WaitAsync();
            
            try
            {
                if (!_isRunning)
                {
                    _logger.LogWarning("Background services are not running");
                    return;
                }
                
                _logger.LogInformation("Stopping all background services");
                
                // Request cancellation for all services
                if (!_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }
                
                var stopTasks = new List<Task>();
                
                // Stop each polling service
                foreach (var service in _pollingServices)
                {
                    stopTasks.Add(StopServiceWithTimeout(service));
                }
                
                // Wait for all services to stop (with timeout)
                await Task.WhenAll(stopTasks);
                
                _isRunning = false;
                _logger.LogInformation("All background services stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while stopping background services");
                throw;
            }
            finally
            {
                _controlLock.Release();
            }
        }
        
        /// <summary>
        /// Disposes all resources used by the host
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
                
            _disposed = true;
            
            _logger.LogInformation("Disposing background services host");
            
            try
            {
                // Stop all services if running
                if (_isRunning)
                {
                    StopAllServicesAsync().GetAwaiter().GetResult();
                }
                
                // Dispose cancellation token source
                _cts.Dispose();
                
                // Dispose semaphore
                _controlLock.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing background services host");
            }
        }
        
        /// <summary>
        /// Starts a background service with retry logic
        /// </summary>
        /// <param name="service">The service to start</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        private async Task StartServiceWithRetryAsync(IBackgroundPollingService service, CancellationToken cancellationToken)
        {
            const int maxRetries = 3;
            int retryCount = 0;
            bool success = false;
            
            while (!success && retryCount < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting background service (attempt {Attempt})", retryCount + 1);
                    await service.StartAsync(cancellationToken);
                    success = true;
                }
                catch (Exception ex) when (retryCount < maxRetries - 1 && !cancellationToken.IsCancellationRequested)
                {
                    retryCount++;
                    _logger.LogWarning(ex, "Failed to start background service, retrying ({Attempt}/{MaxRetries})", 
                        retryCount, maxRetries);
                        
                    // Wait before retry (with exponential backoff)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), cancellationToken);
                }
            }
            
            if (!success && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogError("Failed to start background service after {MaxRetries} attempts", maxRetries);
                throw new InvalidOperationException($"Failed to start background service after {maxRetries} attempts");
            }
        }
        
        /// <summary>
        /// Stops a background service with timeout
        /// </summary>
        /// <param name="service">The service to stop</param>
        /// <returns>A task representing the asynchronous operation</returns>
        private async Task StopServiceWithTimeout(IBackgroundPollingService service)
        {
            try
            {
                // Create a task that will complete when the service stops
                var stopTask = service.StopAsync();
                
                // Create a task that will complete after the timeout
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
                
                // Wait for either the service to stop or the timeout
                var completedTask = await Task.WhenAny(stopTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    _logger.LogWarning("Background service stop operation timed out");
                }
                else
                {
                    // Ensure the stop task completes without exception
                    await stopTask;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping background service");
            }
        }
    }
}
