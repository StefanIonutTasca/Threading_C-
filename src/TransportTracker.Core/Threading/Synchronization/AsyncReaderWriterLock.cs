using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TransportTracker.Core.Threading.Synchronization
{
    /// <summary>
    /// Provides an async/await-friendly reader-writer lock
    /// Allows multiple concurrent readers but exclusive writer access
    /// </summary>
    public class AsyncReaderWriterLock
    {
        private readonly SemaphoreSlim _writerSemaphore = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _readerLock = new SemaphoreSlim(1, 1);
        private int _readerCount = 0;
        
        /// <summary>
        /// Asynchronously acquires a reader lock
        /// Multiple readers can hold the lock simultaneously
        /// </summary>
        /// <returns>A disposable object that releases the lock when disposed</returns>
        public async Task<IDisposable> ReaderLockAsync()
        {
            await _readerLock.WaitAsync();
            try
            {
                _readerCount++;
                if (_readerCount == 1)
                {
                    // First reader acquires the writer lock
                    await _writerSemaphore.WaitAsync();
                }
            }
            finally
            {
                _readerLock.Release();
            }
            
            return new ReaderReleaser(this);
        }
        
        /// <summary>
        /// Asynchronously acquires a writer lock
        /// Only one writer can hold the lock, and no readers can be active
        /// </summary>
        /// <returns>A disposable object that releases the lock when disposed</returns>
        public async Task<IDisposable> WriterLockAsync()
        {
            await _writerSemaphore.WaitAsync();
            return new WriterReleaser(this);
        }
        
        /// <summary>
        /// Asynchronously acquires a reader lock with timeout
        /// </summary>
        /// <param name="timeout">Maximum time to wait for the lock</param>
        /// <returns>A disposable object that releases the lock if acquired, or null if timed out</returns>
        public async Task<IDisposable> ReaderLockAsync(TimeSpan timeout)
        {
            if (!await _readerLock.WaitAsync(timeout))
                return null;
            
            try
            {
                _readerCount++;
                if (_readerCount == 1)
                {
                    // First reader acquires the writer lock
                    if (!await _writerSemaphore.WaitAsync(timeout))
                    {
                        // If we can't get the writer lock, decrement the reader count
                        _readerCount--;
                        return null;
                    }
                }
            }
            finally
            {
                _readerLock.Release();
            }
            
            return new ReaderReleaser(this);
        }
        
        /// <summary>
        /// Asynchronously acquires a writer lock with timeout
        /// </summary>
        /// <param name="timeout">Maximum time to wait for the lock</param>
        /// <returns>A disposable object that releases the lock if acquired, or null if timed out</returns>
        public async Task<IDisposable> WriterLockAsync(TimeSpan timeout)
        {
            if (await _writerSemaphore.WaitAsync(timeout))
            {
                return new WriterReleaser(this);
            }
            
            return null;
        }
        
        /// <summary>
        /// Helper class that releases a reader lock when disposed
        /// </summary>
        private class ReaderReleaser : IDisposable
        {
            private readonly AsyncReaderWriterLock _toRelease;
            private bool _disposed = false;
            
            internal ReaderReleaser(AsyncReaderWriterLock toRelease)
            {
                _toRelease = toRelease;
            }
            
            public void Dispose()
            {
                if (!_disposed)
                {
                    _toRelease._readerLock.Wait();
                    try
                    {
                        _toRelease._readerCount--;
                        if (_toRelease._readerCount == 0)
                        {
                            // Last reader releases the writer lock
                            _toRelease._writerSemaphore.Release();
                        }
                    }
                    finally
                    {
                        _toRelease._readerLock.Release();
                        _disposed = true;
                    }
                }
            }
        }
        
        /// <summary>
        /// Helper class that releases a writer lock when disposed
        /// </summary>
        private class WriterReleaser : IDisposable
        {
            private readonly AsyncReaderWriterLock _toRelease;
            private bool _disposed = false;
            
            internal WriterReleaser(AsyncReaderWriterLock toRelease)
            {
                _toRelease = toRelease;
            }
            
            public void Dispose()
            {
                if (!_disposed)
                {
                    _toRelease._writerSemaphore.Release();
                    _disposed = true;
                }
            }
        }
    }
}
