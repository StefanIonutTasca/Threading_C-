using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Threading.Coordination
{
    /// <summary>
    /// Provides coordination services for multiple threads, including signaling, 
    /// state management, and barrier synchronization.
    /// Particularly useful for transport tracking where multiple operations 
    /// need to coordinate (e.g., API polling, data processing, and UI updates).
    /// </summary>
    public class ThreadCoordinator : IDisposable
    {
        private readonly ILogger<ThreadCoordinator> _logger;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _signals = new();
        private readonly ConcurrentDictionary<string, object> _sharedState = new();
        private readonly ConcurrentDictionary<string, AsyncCountdownEvent> _barriers = new();
        private readonly CancellationTokenSource _globalCts = new();
        private bool _disposed = false;
        
        /// <summary>
        /// Creates a new thread coordinator instance
        /// </summary>
        /// <param name="logger">Logger for recording coordination events</param>
        public ThreadCoordinator(ILogger<ThreadCoordinator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Gets a cancellation token that will be triggered when the coordinator is disposed
        /// </summary>
        public CancellationToken GlobalCancellationToken => _globalCts.Token;
        
        /// <summary>
        /// Creates a new linked cancellation token that combines the global token with a caller-provided token
        /// </summary>
        /// <param name="token">The token to link with the global token</param>
        /// <returns>A linked cancellation token</returns>
        public CancellationToken CreateLinkedToken(CancellationToken token)
        {
            if (token == CancellationToken.None)
                return GlobalCancellationToken;
                
            return CancellationTokenSource.CreateLinkedTokenSource(GlobalCancellationToken, token).Token;
        }
        
        #region Signal Management
        
        /// <summary>
        /// Registers a new named signal with the coordinator
        /// </summary>
        /// <param name="signalName">The name of the signal</param>
        /// <param name="initialCount">The initial count of the semaphore (default: 0)</param>
        /// <returns>True if registered, false if already exists</returns>
        public bool RegisterSignal(string signalName, int initialCount = 0)
        {
            if (string.IsNullOrEmpty(signalName))
                throw new ArgumentNullException(nameof(signalName));
                
            if (initialCount < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCount), "Initial count cannot be negative");
                
            var created = _signals.TryAdd(signalName, new SemaphoreSlim(initialCount, int.MaxValue));
            if (created)
            {
                _logger.LogDebug($"Signal '{signalName}' registered with initial count {initialCount}");
            }
            
            return created;
        }
        
        /// <summary>
        /// Raises a signal, incrementing its count and potentially releasing waiting threads
        /// </summary>
        /// <param name="signalName">The name of the signal to raise</param>
        /// <param name="count">The number of times to raise the signal (default: 1)</param>
        /// <returns>True if the signal was raised, false if it doesn't exist</returns>
        public bool RaiseSignal(string signalName, int count = 1)
        {
            if (string.IsNullOrEmpty(signalName))
                throw new ArgumentNullException(nameof(signalName));
                
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive");
                
            if (_signals.TryGetValue(signalName, out var signal))
            {
                try
                {
                    signal.Release(count);
                    _logger.LogDebug($"Signal '{signalName}' raised {count} time(s)");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error raising signal '{signalName}'");
                    throw;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Asynchronously waits for a signal to be raised
        /// </summary>
        /// <param name="signalName">The name of the signal to wait for</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task that completes when the signal is raised</returns>
        public async Task WaitForSignalAsync(string signalName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(signalName))
                throw new ArgumentNullException(nameof(signalName));
                
            // Create a linked token to ensure cancellation if the coordinator is disposed
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(GlobalCancellationToken, cancellationToken);
            
            if (_signals.TryGetValue(signalName, out var signal))
            {
                try
                {
                    await signal.WaitAsync(linkedCts.Token);
                    _logger.LogDebug($"Signal '{signalName}' wait completed");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug($"Wait for signal '{signalName}' was cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error waiting for signal '{signalName}'");
                    throw;
                }
                finally
                {
                    linkedCts.Dispose();
                }
            }
            else
            {
                linkedCts.Dispose();
                throw new KeyNotFoundException($"Signal '{signalName}' is not registered");
            }
        }
        
        /// <summary>
        /// Asynchronously waits for a signal with a timeout
        /// </summary>
        /// <param name="signalName">The name of the signal to wait for</param>
        /// <param name="timeout">Maximum wait time</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if the signal was raised, false if timed out</returns>
        public async Task<bool> WaitForSignalAsync(string signalName, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(signalName))
                throw new ArgumentNullException(nameof(signalName));
                
            // Create a linked token to ensure cancellation if the coordinator is disposed
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(GlobalCancellationToken, cancellationToken);
            
            if (_signals.TryGetValue(signalName, out var signal))
            {
                try
                {
                    var result = await signal.WaitAsync(timeout, linkedCts.Token);
                    _logger.LogDebug($"Signal '{signalName}' wait completed: {(result ? "Success" : "Timeout")}");
                    return result;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug($"Wait for signal '{signalName}' was cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error waiting for signal '{signalName}'");
                    throw;
                }
                finally
                {
                    linkedCts.Dispose();
                }
            }
            else
            {
                linkedCts.Dispose();
                throw new KeyNotFoundException($"Signal '{signalName}' is not registered");
            }
        }
        
        #endregion
        
        #region Shared State Management
        
        /// <summary>
        /// Sets a value in the shared state dictionary
        /// </summary>
        /// <param name="key">The state key</param>
        /// <param name="value">The value to store</param>
        public void SetSharedState(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
                
            _sharedState[key] = value;
        }
        
        /// <summary>
        /// Tries to get a value from the shared state
        /// </summary>
        /// <typeparam name="T">The expected type of the value</typeparam>
        /// <param name="key">The state key</param>
        /// <param name="value">The retrieved value if successful</param>
        /// <returns>True if the value was retrieved and is of the correct type</returns>
        public bool TryGetSharedState<T>(string key, out T value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
                
            value = default;
            
            if (_sharedState.TryGetValue(key, out var storedValue) && storedValue is T typedValue)
            {
                value = typedValue;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Removes a value from the shared state
        /// </summary>
        /// <param name="key">The state key to remove</param>
        /// <returns>True if the value was removed</returns>
        public bool RemoveSharedState(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
                
            return _sharedState.TryRemove(key, out _);
        }
        
        #endregion
        
        #region Barrier Synchronization
        
        /// <summary>
        /// Creates a named synchronization barrier
        /// </summary>
        /// <param name="barrierName">The name of the barrier</param>
        /// <param name="participantCount">The number of participants that must signal before the barrier is released</param>
        /// <returns>True if created, false if already exists</returns>
        public bool CreateBarrier(string barrierName, int participantCount)
        {
            if (string.IsNullOrEmpty(barrierName))
                throw new ArgumentNullException(nameof(barrierName));
                
            if (participantCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(participantCount), "Participant count must be positive");
                
            var created = _barriers.TryAdd(barrierName, new AsyncCountdownEvent(participantCount));
            if (created)
            {
                _logger.LogDebug($"Barrier '{barrierName}' created with {participantCount} participants");
            }
            
            return created;
        }
        
        /// <summary>
        /// Signals arrival at the named barrier
        /// </summary>
        /// <param name="barrierName">The name of the barrier</param>
        /// <returns>True if this signal caused the barrier to be released, false otherwise</returns>
        public bool SignalBarrier(string barrierName)
        {
            if (string.IsNullOrEmpty(barrierName))
                throw new ArgumentNullException(nameof(barrierName));
                
            if (_barriers.TryGetValue(barrierName, out var barrier))
            {
                var result = barrier.Signal();
                _logger.LogDebug($"Barrier '{barrierName}' signaled. Barrier {(result ? "released" : "still waiting")}");
                return result;
            }
            
            throw new KeyNotFoundException($"Barrier '{barrierName}' does not exist");
        }
        
        /// <summary>
        /// Asynchronously waits for all participants to arrive at the barrier
        /// </summary>
        /// <param name="barrierName">The name of the barrier</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task that completes when the barrier is released</returns>
        public async Task WaitForBarrierAsync(string barrierName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(barrierName))
                throw new ArgumentNullException(nameof(barrierName));
                
            // Create a linked token to ensure cancellation if the coordinator is disposed
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(GlobalCancellationToken, cancellationToken);
            
            if (_barriers.TryGetValue(barrierName, out var barrier))
            {
                try
                {
                    await barrier.WaitAsync(linkedCts.Token);
                    _logger.LogDebug($"Barrier '{barrierName}' wait completed");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug($"Wait for barrier '{barrierName}' was cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error waiting for barrier '{barrierName}'");
                    throw;
                }
                finally
                {
                    linkedCts.Dispose();
                }
            }
            else
            {
                linkedCts.Dispose();
                throw new KeyNotFoundException($"Barrier '{barrierName}' does not exist");
            }
        }
        
        /// <summary>
        /// Resets a barrier to its initial state
        /// </summary>
        /// <param name="barrierName">The name of the barrier</param>
        /// <param name="newParticipantCount">Optional new participant count</param>
        public void ResetBarrier(string barrierName, int? newParticipantCount = null)
        {
            if (string.IsNullOrEmpty(barrierName))
                throw new ArgumentNullException(nameof(barrierName));
                
            if (newParticipantCount.HasValue && newParticipantCount.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(newParticipantCount), "Participant count must be positive");
                
            if (_barriers.TryGetValue(barrierName, out var barrier))
            {
                barrier.Reset(newParticipantCount);
                _logger.LogDebug($"Barrier '{barrierName}' reset{(newParticipantCount.HasValue ? $" with new count {newParticipantCount.Value}" : "")}");
            }
            else
            {
                throw new KeyNotFoundException($"Barrier '{barrierName}' does not exist");
            }
        }
        
        #endregion
        
        /// <summary>
        /// Disposes all resources used by the coordinator
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
                
            _disposed = true;
            
            try
            {
                _globalCts.Cancel();
                
                // Dispose all signals
                foreach (var signal in _signals.Values)
                {
                    signal.Dispose();
                }
                _signals.Clear();
                
                // Dispose all barriers
                foreach (var barrier in _barriers.Values)
                {
                    barrier.Dispose();
                }
                _barriers.Clear();
                
                // Clear shared state
                _sharedState.Clear();
                
                _globalCts.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing thread coordinator");
            }
        }
    }
}
