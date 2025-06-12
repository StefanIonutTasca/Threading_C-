using System;
using System.Threading;
using System.Threading.Tasks;

namespace TransportTracker.Core.Threading.Coordination
{
    /// <summary>
    /// Represents a synchronization primitive that is signaled when its count reaches zero.
    /// This implementation enhances the standard .NET CountdownEvent with async/await support
    /// and additional functionality useful for transport tracking scenarios.
    /// </summary>
    public class AsyncCountdownEvent : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
        private int _initialCount;
        private int _currentCount;
        private readonly object _syncLock = new object();
        private bool _disposed = false;
        
        /// <summary>
        /// Gets the initial count value.
        /// </summary>
        public int InitialCount => _initialCount;
        
        /// <summary>
        /// Gets the current count value.
        /// </summary>
        public int CurrentCount => _currentCount;
        
        /// <summary>
        /// Gets a value indicating whether the event is set (count is zero).
        /// </summary>
        public bool IsSet => _currentCount == 0;
        
        /// <summary>
        /// Initializes a new instance of the AsyncCountdownEvent class with the specified count.
        /// </summary>
        /// <param name="initialCount">The number of signals required to set the event.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if initialCount is less than 0.</exception>
        public AsyncCountdownEvent(int initialCount)
        {
            if (initialCount < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCount), "Initial count cannot be negative.");
            
            _initialCount = initialCount;
            _currentCount = initialCount;
            
            if (initialCount == 0)
                _semaphore.Release();
        }
        
        /// <summary>
        /// Registers a signal with the event, decrementing its count.
        /// </summary>
        /// <returns>True if the signal caused the count to reach zero, false otherwise.</returns>
        public bool Signal()
        {
            return Signal(1);
        }
        
        /// <summary>
        /// Registers multiple signals with the event, decrementing its count by the specified amount.
        /// </summary>
        /// <param name="signalCount">The number of signals to register.</param>
        /// <returns>True if the signals caused the count to reach zero, false otherwise.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if signalCount is less than 1.</exception>
        /// <exception cref="InvalidOperationException">Thrown if signalCount is greater than CurrentCount.</exception>
        public bool Signal(int signalCount)
        {
            if (signalCount < 1)
                throw new ArgumentOutOfRangeException(nameof(signalCount), "Signal count must be greater than zero.");
            
            lock (_syncLock)
            {
                if (signalCount > _currentCount)
                    throw new InvalidOperationException("Cannot signal more than the current count.");
                
                _currentCount -= signalCount;
                
                if (_currentCount == 0)
                {
                    _semaphore.Release();
                    return true;
                }
                
                return false;
            }
        }
        
        /// <summary>
        /// Adds signals to the event, incrementing its count by the specified amount.
        /// </summary>
        /// <param name="signalCount">The number of signals to add.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if signalCount is less than 1.</exception>
        public void AddCount(int signalCount = 1)
        {
            if (signalCount < 1)
                throw new ArgumentOutOfRangeException(nameof(signalCount), "Signal count must be greater than zero.");
            
            lock (_syncLock)
            {
                if (_currentCount == 0)
                    throw new InvalidOperationException("Cannot add signals after the count has reached zero.");
                
                _currentCount += signalCount;
            }
        }
        
        /// <summary>
        /// Resets the event to the specified count or its initial count.
        /// </summary>
        /// <param name="count">Optional new count. If not specified, the initial count is used.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if count is less than 0.</exception>
        public void Reset(int? count = null)
        {
            int newCount = count ?? _initialCount;
            
            if (newCount < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");
            
            lock (_syncLock)
            {
                if (newCount == 0 && _currentCount != 0)
                {
                    _semaphore.Release();
                }
                else if (newCount > 0 && _currentCount == 0)
                {
                    // Need to drain any permits from the semaphore
                    while (_semaphore.Wait(0))
                    {
                        // Intentionally empty
                    }
                }
                
                _currentCount = newCount;
                
                if (count.HasValue)
                    _initialCount = newCount;
            }
        }
        
        /// <summary>
        /// Asynchronously waits for the count to reach zero.
        /// </summary>
        /// <returns>A task that completes when the count reaches zero.</returns>
        public Task WaitAsync()
        {
            return WaitAsync(Timeout.InfiniteTimeSpan, CancellationToken.None);
        }
        
        /// <summary>
        /// Asynchronously waits for the count to reach zero with cancellation support.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        /// <returns>A task that completes when the count reaches zero.</returns>
        public Task WaitAsync(CancellationToken cancellationToken)
        {
            return WaitAsync(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        
        /// <summary>
        /// Asynchronously waits for the count to reach zero with a specified timeout.
        /// </summary>
        /// <param name="timeout">A TimeSpan representing the timeout period.</param>
        /// <returns>A task that completes with a boolean indicating whether the wait completed within the timeout period.</returns>
        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return WaitAsync(timeout, CancellationToken.None);
        }
        
        /// <summary>
        /// Asynchronously waits for the count to reach zero with a specified timeout and cancellation support.
        /// </summary>
        /// <param name="timeout">A TimeSpan representing the timeout period.</param>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        /// <returns>A task that completes with a boolean indicating whether the wait completed within the timeout period.</returns>
        public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AsyncCountdownEvent));
            
            if (_currentCount == 0)
                return true;
            
            return await _semaphore.WaitAsync(timeout, cancellationToken);
        }
        
        /// <summary>
        /// Waits for the count to reach zero.
        /// </summary>
        /// <returns>True if the wait completed without timing out.</returns>
        public bool Wait()
        {
            return Wait(Timeout.InfiniteTimeSpan);
        }
        
        /// <summary>
        /// Waits for the count to reach zero with a specified timeout.
        /// </summary>
        /// <param name="timeout">A TimeSpan representing the timeout period.</param>
        /// <returns>True if the wait completed without timing out.</returns>
        public bool Wait(TimeSpan timeout)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AsyncCountdownEvent));
            
            if (_currentCount == 0)
                return true;
            
            return _semaphore.Wait(timeout);
        }
        
        /// <summary>
        /// Waits for the count to reach zero with cancellation support.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        public void Wait(CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AsyncCountdownEvent));
            
            if (_currentCount == 0)
                return;
            
            _semaphore.Wait(cancellationToken);
        }
        
        /// <summary>
        /// Waits for the count to reach zero with a specified timeout and cancellation support.
        /// </summary>
        /// <param name="timeout">A TimeSpan representing the timeout period.</param>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        /// <returns>True if the wait completed without timing out.</returns>
        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AsyncCountdownEvent));
            
            if (_currentCount == 0)
                return true;
            
            return _semaphore.Wait(timeout, cancellationToken);
        }
        
        /// <summary>
        /// Releases all resources used by the AsyncCountdownEvent.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _semaphore.Dispose();
                _disposed = true;
            }
        }
    }
}
