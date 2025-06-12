using System;
using System.Threading;
using System.Threading.Tasks;

namespace TransportTracker.Core.Threading.Synchronization
{
    /// <summary>
    /// Provides an async/await-friendly mutual exclusion lock that can be used with the 'using' statement
    /// </summary>
    public class AsyncLock
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly Task<IDisposable> _releaser;
        
        /// <summary>
        /// Creates a new instance of AsyncLock
        /// </summary>
        public AsyncLock()
        {
            _releaser = Task.FromResult((IDisposable)new Releaser(this));
        }
        
        /// <summary>
        /// Asynchronously acquires the lock
        /// Use with 'using' statement for automatic release
        /// </summary>
        /// <returns>A disposable object that releases the lock when disposed</returns>
        public Task<IDisposable> LockAsync()
        {
            var wait = _semaphore.WaitAsync();
            
            // If the lock is available immediately, return the cached releaser
            if (wait.IsCompleted)
            {
                return _releaser;
            }
            
            // Otherwise, create a new task that waits for the lock and returns a releaser
            return wait.ContinueWith(
                (_, state) => (IDisposable)new Releaser((AsyncLock)state),
                this,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        
        /// <summary>
        /// Asynchronously acquires the lock with timeout
        /// </summary>
        /// <param name="timeout">Maximum time to wait for the lock</param>
        /// <returns>A disposable object that releases the lock if acquired, or null if timed out</returns>
        public async Task<IDisposable> LockAsync(TimeSpan timeout)
        {
            if (await _semaphore.WaitAsync(timeout))
            {
                return new Releaser(this);
            }
            
            return null;
        }
        
        /// <summary>
        /// Asynchronously acquires the lock with cancellation support
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A disposable object that releases the lock when disposed</returns>
        public async Task<IDisposable> LockAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            return new Releaser(this);
        }
        
        /// <summary>
        /// Asynchronously acquires the lock with timeout and cancellation support
        /// </summary>
        /// <param name="timeout">Maximum time to wait for the lock</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A disposable object that releases the lock if acquired, or null if timed out</returns>
        public async Task<IDisposable> LockAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (await _semaphore.WaitAsync(timeout, cancellationToken))
            {
                return new Releaser(this);
            }
            
            return null;
        }
        
        /// <summary>
        /// Attempts to acquire the lock synchronously without waiting
        /// </summary>
        /// <returns>A disposable object that releases the lock if acquired, or null if the lock was not available</returns>
        public IDisposable TryLock()
        {
            if (_semaphore.Wait(0))
            {
                return new Releaser(this);
            }
            
            return null;
        }
        
        /// <summary>
        /// Helper class that releases the lock when disposed
        /// </summary>
        private class Releaser : IDisposable
        {
            private readonly AsyncLock _toRelease;
            private bool _disposed = false;
            
            internal Releaser(AsyncLock toRelease)
            {
                _toRelease = toRelease;
            }
            
            public void Dispose()
            {
                if (!_disposed)
                {
                    _toRelease._semaphore.Release();
                    _disposed = true;
                }
            }
        }
    }
}
