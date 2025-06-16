using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Caching
{
    /// <summary>
    /// Implements a two-level caching system with memory (L1) and disk (L2) caches
    /// </summary>
    /// <typeparam name="TKey">The type of keys used for cache entries</typeparam>
    /// <typeparam name="TValue">The type of values stored in the cache</typeparam>
    public class TwoLevelCache<TKey, TValue> : ICacheProvider<TKey, TValue>, IDisposable 
        where TKey : notnull
    {
        private readonly MemoryCacheProvider<TKey, TValue> _memoryCache;
        private readonly DiskCacheProvider<TKey, TValue> _diskCache;
        private readonly ILogger _logger;
        private readonly TimeSpan _synchronizationInterval;
        private readonly SemaphoreSlim _synchronizationLock;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _backgroundSyncTask;
        private bool _disposed;
        
        /// <summary>
        /// Creates a new instance of TwoLevelCache
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="memorySizeLimit">Optional memory size limit in bytes</param>
        /// <param name="diskSpaceLimit">Optional disk space limit in bytes</param>
        /// <param name="cacheDirectory">Optional custom cache directory for disk cache</param>
        /// <param name="synchronizationInterval">How often to sync L1-L2 caches (default: 5 minutes)</param>
        public TwoLevelCache(
            ILogger logger,
            long memorySizeLimit = 104857600, // 100MB default
            long diskSpaceLimit = 1073741824, // 1GB default
            string cacheDirectory = null,
            TimeSpan? synchronizationInterval = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryCache = new MemoryCacheProvider<TKey, TValue>(
                logger, 
                typeof(TValue).Name + "MemoryCache", 
                memorySizeLimit);
                
            _diskCache = new DiskCacheProvider<TKey, TValue>(
                logger, 
                cacheDirectory, 
                diskSpaceLimit);
                
            _synchronizationInterval = synchronizationInterval ?? TimeSpan.FromMinutes(5);
            _synchronizationLock = new SemaphoreSlim(1, 1);
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Start background sync task
            _backgroundSyncTask = StartBackgroundSynchronizationAsync(_cancellationTokenSource.Token);
            
            _logger.LogInformation(
                "TwoLevelCache initialized for {Type} with {MemoryLimit}MB memory limit and {DiskLimit}GB disk limit",
                typeof(TValue).Name,
                memorySizeLimit / 1048576,
                diskSpaceLimit / 1073741824);
        }
        
        /// <summary>
        /// Gets an item from the cache, checking memory first, then disk if not found
        /// </summary>
        /// <param name="key">The key of the item to get</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The cached item, or default if not found in either cache</returns>
        public async Task<TValue> GetAsync(TKey key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Try to get from memory cache first (L1)
            var result = await _memoryCache.GetAsync(key, cancellationToken);
            if (!EqualityComparer<TValue>.Default.Equals(result, default))
            {
                _logger.LogTrace("Cache hit (L1) for key {Key}", key);
                return result;
            }
            
            // Not in memory, try disk (L2)
            result = await _diskCache.GetAsync(key, cancellationToken);
            if (!EqualityComparer<TValue>.Default.Equals(result, default))
            {
                _logger.LogTrace("Cache hit (L2) for key {Key}", key);
                
                // Promote to memory cache for future requests
                await _memoryCache.SetAsync(
                    key, 
                    result, 
                    CacheEntryOptions.Standard(CacheTier.MemoryOnly),
                    cancellationToken);
                    
                return result;
            }
            
            _logger.LogDebug("Cache miss (L1+L2) for key {Key}", key);
            return default;
        }
        
        /// <summary>
        /// Sets an item in the cache according to the tier specified in options
        /// </summary>
        /// <param name="key">The key of the item to set</param>
        /// <param name="value">The value to cache</param>
        /// <param name="options">Caching options</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task SetAsync(TKey key, TValue value, CacheEntryOptions options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (options == null)
            {
                options = CacheEntryOptions.Standard();
            }
            
            // Determine where to cache based on tier
            switch (options.Tier)
            {
                case CacheTier.MemoryOnly:
                    await _memoryCache.SetAsync(key, value, options, cancellationToken);
                    break;
                    
                case CacheTier.DiskOnly:
                    await _diskCache.SetAsync(key, value, options, cancellationToken);
                    break;
                    
                case CacheTier.Both:
                default:
                    // Set in memory cache (faster for reads)
                    var memoryOptions = options.Clone();
                    memoryOptions.Tier = CacheTier.MemoryOnly;
                    await _memoryCache.SetAsync(key, value, memoryOptions, cancellationToken);
                    
                    // Set in disk cache (persistence)
                    var diskOptions = options.Clone();
                    diskOptions.Tier = CacheTier.DiskOnly;
                    await _diskCache.SetAsync(key, value, diskOptions, cancellationToken);
                    break;
            }
            
            _logger.LogDebug("Cached item with key {Key} in tier {Tier}", key, options.Tier);
        }
        
        /// <summary>
        /// Removes an item from both caches
        /// </summary>
        /// <param name="key">The key of the item to remove</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if the item was removed from any tier, otherwise false</returns>
        public async Task<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var memoryRemoved = await _memoryCache.RemoveAsync(key, cancellationToken);
            var diskRemoved = await _diskCache.RemoveAsync(key, cancellationToken);
            
            return memoryRemoved || diskRemoved;
        }
        
        /// <summary>
        /// Checks if an item exists in any cache tier
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if the item exists in any tier, otherwise false</returns>
        public async Task<bool> ContainsKeyAsync(TKey key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Check memory first (faster)
            if (await _memoryCache.ContainsKeyAsync(key, cancellationToken))
            {
                return true;
            }
            
            // Then check disk
            return await _diskCache.ContainsKeyAsync(key, cancellationToken);
        }
        
        /// <summary>
        /// Clears all items from both cache tiers
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Clear both in parallel
            var memoryTask = _memoryCache.ClearAsync(cancellationToken);
            var diskTask = _diskCache.ClearAsync(cancellationToken);
            
            await Task.WhenAll(memoryTask, diskTask);
            
            _logger.LogInformation("Cleared all cache tiers for {Type}", typeof(TValue).Name);
        }
        
        /// <summary>
        /// Gets or creates an item in the cache, using a factory method if not found
        /// </summary>
        /// <param name="key">The key of the item</param>
        /// <param name="valueFactory">Factory method to create the item if not found</param>
        /// <param name="options">Caching options</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The cached or created value</returns>
        public async Task<TValue> GetOrCreateAsync(
            TKey key, 
            Func<Task<TValue>> valueFactory, 
            CacheEntryOptions options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (options == null)
            {
                options = CacheEntryOptions.Standard();
            }
            
            // Try to get from memory first (L1)
            var result = await _memoryCache.GetAsync(key, cancellationToken);
            if (!EqualityComparer<TValue>.Default.Equals(result, default))
            {
                _logger.LogTrace("Cache hit (L1) for key {Key}", key);
                return result;
            }
            
            // If not in memory but memory-only tier is requested, use memory cache's implementation
            if (options.Tier == CacheTier.MemoryOnly)
            {
                return await _memoryCache.GetOrCreateAsync(key, valueFactory, options, cancellationToken);
            }
            
            // Not in memory, try disk (L2)
            result = await _diskCache.GetAsync(key, cancellationToken);
            if (!EqualityComparer<TValue>.Default.Equals(result, default))
            {
                _logger.LogTrace("Cache hit (L2) for key {Key}", key);
                
                // Promote to memory cache for future requests if not disk-only
                if (options.Tier != CacheTier.DiskOnly)
                {
                    var memoryOptions = options.Clone();
                    memoryOptions.Tier = CacheTier.MemoryOnly;
                    await _memoryCache.SetAsync(key, result, memoryOptions, cancellationToken);
                }
                
                return result;
            }
            
            // Not found in any cache, create value and store in appropriate tiers
            _logger.LogDebug("Cache miss (L1+L2) for key {Key}, creating value", key);
            result = await valueFactory();
            
            if (!EqualityComparer<TValue>.Default.Equals(result, default))
            {
                await SetAsync(key, result, options, cancellationToken);
            }
            
            return result;
        }
        
        /// <summary>
        /// Gets statistics about both cache tiers
        /// </summary>
        /// <returns>Dictionary with cache statistics by tier</returns>
        public Dictionary<string, CacheStatistics> GetStatistics()
        {
            return new Dictionary<string, CacheStatistics>
            {
                { "Memory", _memoryCache.GetStatistics() },
                { "Disk", new CacheStatistics { CacheType = "Disk" } } // Disk stats would need implementation
            };
        }
        
        /// <summary>
        /// Forces a synchronization between memory and disk caches
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task ForceSynchronizationAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TwoLevelCache<TKey, TValue>));
            }
            
            await _synchronizationLock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Starting forced cache synchronization");
                // TODO: Implement cache synchronization logic if needed
                await Task.Delay(1, cancellationToken); // Placeholder
                _logger.LogInformation("Completed forced cache synchronization");
            }
            finally
            {
                _synchronizationLock.Release();
            }
        }
        
        /// <summary>
        /// Disposes the two-level cache resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Releases the unmanaged resources used by the cache and optionally releases the managed resources
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            
            if (disposing)
            {
                // Cancel background task
                _cancellationTokenSource.Cancel();
                try
                {
                    _backgroundSyncTask.Wait(TimeSpan.FromSeconds(5));
                }
                catch (TaskCanceledException)
                {
                    // Expected
                }
                catch (AggregateException ex) when (ex.InnerExceptions.Count == 1 && ex.InnerExceptions[0] is TaskCanceledException)
                {
                    // Expected
                }
                
                // Dispose resources
                _cancellationTokenSource.Dispose();
                _synchronizationLock.Dispose();
                (_diskCache as IDisposable)?.Dispose();
                
                _logger.LogInformation("TwoLevelCache disposed for {Type}", typeof(TValue).Name);
            }
            
            _disposed = true;
        }
        
        /// <summary>
        /// Starts a background task that periodically synchronizes the caches
        /// </summary>
        private async Task StartBackgroundSynchronizationAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(_synchronizationInterval, cancellationToken);
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    
                    try
                    {
                        await ForceSynchronizationAsync(cancellationToken);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        _logger.LogError(ex, "Error during background cache synchronization");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                _logger.LogInformation("Background cache synchronization task cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in background cache synchronization task");
            }
        }
    }
}
