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
        private readonly ConcurrentDictionary<string, Thread> _managedThreads = new();
        private readonly ConcurrentDictionary<string, int> _updateCounts = new();
        private readonly CancellationTokenSource _globalCts = new();
        private bool _disposed = false;
        private ManualResetEvent _sharedSignal = new ManualResetEvent(false);
        private ConcurrentDictionary<string, ManualResetEvent> _namedSignals = new ConcurrentDictionary<string, ManualResetEvent>();
        
        /// <summary>
        /// Signals the shared event, allowing threads waiting on it to proceed
        /// </summary>
        /// <returns>True if the signal was set to the signaled state, false if it was already signaled</returns>
        public bool Signal()
        {
            bool wasSignaled = _sharedSignal.WaitOne(0);
            if (!wasSignaled)
            {
                _sharedSignal.Set();
                _logger.LogDebug("Shared signal set");
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Signals a named event, allowing threads waiting on it to proceed
        /// </summary>
        /// <param name="signalName">The name of the signal to set</param>
        /// <returns>True if the signal was set to the signaled state, false if it was already signaled or doesn't exist</returns>
        public bool Signal(string signalName)
        {
            if (string.IsNullOrEmpty(signalName))
                throw new ArgumentNullException(nameof(signalName));
                
            if (_namedSignals.TryGetValue(signalName, out var namedSignal))
            {
                bool wasSignaled = namedSignal.WaitOne(0);
                if (!wasSignaled)
                {
                    namedSignal.Set();
                    _logger.LogDebug($"Named signal '{signalName}' set");
                    return true;
                }
                return false;
            }
            else
            {
                // Create and signal a new named signal if it doesn't exist
                var newSignal = new ManualResetEvent(true); // Create in signaled state
                if (_namedSignals.TryAdd(signalName, newSignal))
                {
                    _logger.LogDebug($"Created and set new named signal '{signalName}'");
                    return true;
                }
                else
                {
                    // Another thread created it first, try to signal it
                    newSignal.Dispose();
                    return Signal(signalName); // Retry
                }
            }
        }
        
        /// <summary>
        /// Signals all named events, allowing threads waiting on them to proceed
        /// </summary>
        /// <returns>The number of signals that were set</returns>
        public int SignalAll()
        {
            int count = 0;
            
            // Set the shared signal first
            if (Signal())
                count++;
                
            // Then set all named signals
            foreach (var signalName in _namedSignals.Keys)
            {
                if (Signal(signalName))
                    count++;
            }
            
            _logger.LogDebug($"All signals set ({count} total)");
            return count;
        }
        
        /// <summary>
        /// Waits for the shared signal to be set
        /// </summary>
        /// <returns>True if the signal was set</returns>
        public bool WaitOne()
        {
            bool result = _sharedSignal.WaitOne();
            _logger.LogDebug($"Wait for shared signal completed with result: {result}");
            return result;
        }
        
        /// <summary>
        /// Waits for a named signal to be set
        /// </summary>
        /// <param name="signalName">The name of the signal to wait for</param>
        /// <returns>True if the signal was set, false if the signal doesn't exist</returns>
        public bool WaitOne(string signalName)
        {
            if (string.IsNullOrEmpty(signalName))
                throw new ArgumentNullException(nameof(signalName));
                
            if (_namedSignals.TryGetValue(signalName, out var namedSignal))
            {
                bool result = namedSignal.WaitOne();
                _logger.LogDebug($"Wait for named signal '{signalName}' completed with result: {result}");
                return result;
            }
            
            _logger.LogWarning($"Attempted to wait on non-existent named signal '{signalName}'");
            return false;
        }
        
        /// <summary>
        /// Waits for the shared signal to be set with a timeout
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds to wait</param>
        /// <returns>True if the signal was set, false if the timeout elapsed</returns>
        public bool WaitOneOrTimeout(int milliseconds)
        {
            if (milliseconds < 0 && milliseconds != Timeout.Infinite)
                throw new ArgumentOutOfRangeException(nameof(milliseconds), "Timeout must be non-negative or Timeout.Infinite");
                
            bool result = _sharedSignal.WaitOne(milliseconds);
            _logger.LogDebug($"Wait for shared signal completed with timeout {milliseconds}ms, result: {result}");
            return result;
        }
        
        /// <summary>
        /// Waits for a named signal to be set with a timeout
        /// </summary>
        /// <param name="signalName">The name of the signal to wait for</param>
        /// <param name="milliseconds">The number of milliseconds to wait</param>
        /// <returns>True if the signal was set, false if the timeout elapsed or the signal doesn't exist</returns>
        public bool WaitOneOrTimeout(string signalName, int milliseconds)
        {
            if (string.IsNullOrEmpty(signalName))
                throw new ArgumentNullException(nameof(signalName));
                
            if (milliseconds < 0 && milliseconds != Timeout.Infinite)
                throw new ArgumentOutOfRangeException(nameof(milliseconds), "Timeout must be non-negative or Timeout.Infinite");
                
            if (_namedSignals.TryGetValue(signalName, out var namedSignal))
            {
                bool result = namedSignal.WaitOne(milliseconds);
                _logger.LogDebug($"Wait for named signal '{signalName}' completed with timeout {milliseconds}ms, result: {result}");
                return result;
            }
            
            _logger.LogWarning($"Attempted to wait with timeout on non-existent named signal '{signalName}'");
            return false;
        }
        
        /// <summary>
        /// Waits for a specific signal to be set, creating it if it doesn't exist
        /// </summary>
        /// <param name="signalName">The name of the signal to wait for</param>
        /// <param name="createIfNotExists">Whether to create the signal if it doesn't exist</param>
        /// <param name="initialState">The initial state of the signal if it's created</param>
        /// <returns>True if the signal was set, false otherwise</returns>
        public bool WaitForSignal(string signalName, bool createIfNotExists = true, bool initialState = false)
        {
            if (string.IsNullOrEmpty(signalName))
                throw new ArgumentNullException(nameof(signalName));
                
            if (!_namedSignals.TryGetValue(signalName, out var namedSignal))
            {
                if (createIfNotExists)
                {
                    namedSignal = new ManualResetEvent(initialState);
                    if (_namedSignals.TryAdd(signalName, namedSignal))
                    {
                        _logger.LogDebug($"Created new named signal '{signalName}' with initial state: {initialState}");
                    }
                    else
                    {
                        // Another thread created it first
                        namedSignal.Dispose();
                        if (!_namedSignals.TryGetValue(signalName, out namedSignal))
                        {
                            _logger.LogWarning($"Failed to get or create signal '{signalName}'");
                            return false;
                        }
                    }
                }
                else
                {
                    _logger.LogWarning($"Attempted to wait on non-existent named signal '{signalName}' and creation was not requested");
                    return false;
                }
            }
            
            bool result = namedSignal.WaitOne();
            _logger.LogDebug($"Wait for signal '{signalName}' completed with result: {result}");
            return result;
        }
        
        /// <summary>
        /// Resets the shared signal to the non-signaled state
        /// </summary>
        public void ResetSignal()
        {
            _sharedSignal.Reset();
            _logger.LogDebug("Shared signal reset");
        }
        
        /// <summary>
        /// Resets a named signal to the non-signaled state
        /// </summary>
        /// <param name="signalName">The name of the signal to reset</param>
        /// <returns>True if the signal was reset, false if it doesn't exist</returns>
        public bool ResetSignal(string signalName)
        {
            if (string.IsNullOrEmpty(signalName))
                throw new ArgumentNullException(nameof(signalName));
                
            if (_namedSignals.TryGetValue(signalName, out var namedSignal))
            {
                namedSignal.Reset();
                _logger.LogDebug($"Named signal '{signalName}' reset");
                return true;
            }
            
            _logger.LogWarning($"Attempted to reset non-existent named signal '{signalName}'");
            return false;
        }
        
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

        #region Thread Management

        /// <summary>
        /// Creates and registers a new thread with the coordinator
        /// </summary>
        /// <param name="threadName">The name of the thread</param>
        /// <param name="threadStart">The ThreadStart delegate to execute</param>
        /// <param name="isBackground">Whether the thread should be a background thread</param>
        /// <param name="priority">Thread priority (default: Normal)</param>
        /// <returns>The created thread instance</returns>
        public Thread CreateThread(string threadName, ThreadStart threadStart, bool isBackground = true, ThreadPriority priority = ThreadPriority.Normal)
        {
            if (string.IsNullOrEmpty(threadName))
                throw new ArgumentNullException(nameof(threadName));
                
            if (threadStart == null)
                throw new ArgumentNullException(nameof(threadStart));
                
            if (_managedThreads.ContainsKey(threadName))
                throw new InvalidOperationException($"Thread with name '{threadName}' already exists");
                
            var thread = new Thread(threadStart)
            {
                Name = threadName,
                IsBackground = isBackground,
                Priority = priority
            };
            
            if (_managedThreads.TryAdd(threadName, thread))
            {
                _logger.LogDebug($"Thread '{threadName}' created with priority {priority}");
                return thread;
            }
            else
            {
                throw new InvalidOperationException($"Failed to register thread '{threadName}'");
            }
        }

        /// <summary>
        /// Creates and registers a new parameterized thread with the coordinator
        /// </summary>
        /// <param name="threadName">The name of the thread</param>
        /// <param name="paramThreadStart">The ParameterizedThreadStart delegate to execute</param>
        /// <param name="isBackground">Whether the thread should be a background thread</param>
        /// <param name="priority">Thread priority (default: Normal)</param>
        /// <returns>The created thread instance</returns>
        public Thread CreateThread(string threadName, ParameterizedThreadStart paramThreadStart, bool isBackground = true, ThreadPriority priority = ThreadPriority.Normal)
        {
            if (string.IsNullOrEmpty(threadName))
                throw new ArgumentNullException(nameof(threadName));
                
            if (paramThreadStart == null)
                throw new ArgumentNullException(nameof(paramThreadStart));
                
            if (_managedThreads.ContainsKey(threadName))
                throw new InvalidOperationException($"Thread with name '{threadName}' already exists");
                
            var thread = new Thread(paramThreadStart)
            {
                Name = threadName,
                IsBackground = isBackground,
                Priority = priority
            };
            
            if (_managedThreads.TryAdd(threadName, thread))
            {
                _logger.LogDebug($"Parameterized thread '{threadName}' created with priority {priority}");
                return thread;
            }
            else
            {
                throw new InvalidOperationException($"Failed to register thread '{threadName}'");
            }
        }

        /// <summary>
        /// Gets a thread by its name
        /// </summary>
        /// <param name="threadName">The name of the thread</param>
        /// <returns>The thread instance if found, otherwise null</returns>
        public Thread GetThread(string threadName)
        {
            if (string.IsNullOrEmpty(threadName))
                throw new ArgumentNullException(nameof(threadName));
                
            _managedThreads.TryGetValue(threadName, out var thread);
            return thread;
        }

        /// <summary>
        /// Notifies that an item has been updated
        /// </summary>
        /// <param name="itemType">Type of item that was updated</param>
        /// <returns>The total update count for this item type</returns>
        public int NotifyItemUpdated(string itemType)
        {
            if (string.IsNullOrEmpty(itemType))
                throw new ArgumentNullException(nameof(itemType));
                
            int count = _updateCounts.AddOrUpdate(itemType, 1, (_, currentCount) => currentCount + 1);
            _logger.LogDebug($"Item of type '{itemType}' updated. Total updates: {count}");
            return count;
        }

        /// <summary>
        /// Gets the current update count for an item type
        /// </summary>
        /// <param name="itemType">The item type to check</param>
        /// <returns>The current update count</returns>
        public int GetUpdateCount(string itemType)
        {
            if (string.IsNullOrEmpty(itemType))
                throw new ArgumentNullException(nameof(itemType));
                
            return _updateCounts.GetValueOrDefault(itemType, 0);
        }
        
        /// <summary>
        /// Resets the update count for an item type
        /// </summary>
        /// <param name="itemType">The item type to reset</param>
        public void ResetUpdateCount(string itemType)
        {
            if (string.IsNullOrEmpty(itemType))
                throw new ArgumentNullException(nameof(itemType));
                
            _updateCounts.TryRemove(itemType, out _);
            _logger.LogDebug($"Reset update count for item type '{itemType}'");
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
                
                // Dispose the shared signal
                _sharedSignal.Dispose();
                
                // Dispose all named signals
                foreach (var signal in _namedSignals.Values)
                {
                    signal.Dispose();
                }
                _namedSignals.Clear();
                
                // Clear shared state
                _sharedState.Clear();
                
                // Clear managed threads
                _managedThreads.Clear();
                
                // Clear update counts
                _updateCounts.Clear();
                
                _globalCts.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing thread coordinator");
            }
        }
    }
}
