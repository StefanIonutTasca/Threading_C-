using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Runtime.Caching;
using System.Collections.Generic;
using System.Linq;

namespace TransportTracker.Core.Caching
{
    /// <summary>
    /// Thread-safe in-memory cache implementation
    /// </summary>
    /// <typeparam name="TKey">The type of keys used for cache entries</typeparam>
    /// <typeparam name="TValue">The type of values stored in the cache</typeparam>
    public class MemoryCacheProvider<TKey, TValue> : ICacheProvider<TKey, TValue> where TKey : notnull
    {
        private readonly MemoryCache _cache;
        private readonly ILogger _logger;
        private readonly ReaderWriterLockSlim _lock;
        private readonly ConcurrentDictionary<TKey, SemaphoreSlim> _keyLocks;
        private readonly string _cacheRegion;
        private readonly long _memorySizeLimit;
        private long _currentMemorySize;
        
        /// <summary>
        /// Creates a new instance of MemoryCacheProvider
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="cacheRegion">Optional cache region name</param>
        /// <param name="memorySizeLimit">Optional memory size limit in bytes</param>
        public MemoryCacheProvider(
            ILogger logger,
            string cacheRegion = null,
            long memorySizeLimit = 104857600) // 100MB default
        {
            _cache = new MemoryCache(cacheRegion ?? typeof(TValue).Name + "Cache");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _lock = new ReaderWriterLockSlim();
            _keyLocks = new ConcurrentDictionary<TKey, SemaphoreSlim>();
            _cacheRegion = cacheRegion ?? "DefaultRegion";
            _memorySizeLimit = memorySizeLimit;
            _currentMemorySize = 0;
            
            _logger.LogInformation(
                "Memory cache provider initialized for {ValueType} with memory limit {MemoryLimit}MB",
                typeof(TValue).Name,
                _memorySizeLimit / 1048576);
        }
        
        /// <summary>
        /// Gets an item from the cache
        /// </summary>
        /// <param name="key">The key of the item to get</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The cached item, or default if not found</returns>
        public Task<TValue> GetAsync(TKey key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _lock.EnterReadLock();
            try
            {
                var cacheKey = GetFormattedCacheKey(key);
                var cachedItem = _cache.Get(cacheKey, _cacheRegion) as CacheItem<TValue>;
                
                if (cachedItem == null)
                {
                    _logger.LogDebug("Cache miss for key {Key}", key);
                    return Task.FromResult<TValue>(default);
                }
                
                _logger.LogTrace("Cache hit for key {Key}", key);
                return Task.FromResult(cachedItem.Value);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Sets an item in the cache with the specified options
        /// </summary>
        /// <param name="key">The key of the item to set</param>
        /// <param name="value">The value to cache</param>
        /// <param name="options">Caching options</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public Task SetAsync(TKey key, TValue value, CacheEntryOptions options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            
            if (options == null)
            {
                options = CacheEntryOptions.Standard();
            }
            
            if (options.Tier == CacheTier.DiskOnly)
            {
                _logger.LogTrace("Skipping memory cache for DiskOnly tier, key {Key}", key);
                return Task.CompletedTask;
            }
                
            var cacheKey = GetFormattedCacheKey(key);
            var cacheItem = new CacheItem<TValue>(value, options.Size);
            var policy = CreateCachePolicy(options);
            
            _lock.EnterWriteLock();
            try
            {
                // Check if we need to make room in the cache
                if (options.Size.HasValue && 
                    _currentMemorySize + options.Size.Value > _memorySizeLimit)
                {
                    MakeRoomInCache(options.Size.Value);
                }
                
                // Remove existing item if present
                if (_cache.Contains(cacheKey, _cacheRegion))
                {
                    var existingItem = _cache.Get(cacheKey, _cacheRegion) as CacheItem<TValue>;
                    if (existingItem?.Size.HasValue == true)
                    {
                        Interlocked.Add(ref _currentMemorySize, -existingItem.Size.Value);
                    }
                    _cache.Remove(cacheKey, _cacheRegion);
                }
                
                // Add new item
                _cache.Add(cacheKey, cacheItem, policy, _cacheRegion);
                
                // Update memory usage
                if (options.Size.HasValue)
                {
                    Interlocked.Add(ref _currentMemorySize, options.Size.Value);
                }
                
                _logger.LogTrace("Added item to cache with key {Key}", key);
                return Task.CompletedTask;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Removes an item from the cache
        /// </summary>
        /// <param name="key">The key of the item to remove</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if the item was removed, false if it wasn't in the cache</returns>
        public Task<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var cacheKey = GetFormattedCacheKey(key);
            
            _lock.EnterWriteLock();
            try
            {
                if (!_cache.Contains(cacheKey, _cacheRegion))
                {
                    return Task.FromResult(false);
                }
                
                // Update memory usage
                var existingItem = _cache.Get(cacheKey, _cacheRegion) as CacheItem<TValue>;
                if (existingItem?.Size.HasValue == true)
                {
                    Interlocked.Add(ref _currentMemorySize, -existingItem.Size.Value);
                }
                
                _cache.Remove(cacheKey, _cacheRegion);
                _logger.LogTrace("Removed item from cache with key {Key}", key);
                return Task.FromResult(true);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Checks if an item exists in the cache
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if the item exists, otherwise false</returns>
        public Task<bool> ContainsKeyAsync(TKey key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var cacheKey = GetFormattedCacheKey(key);
            
            _lock.EnterReadLock();
            try
            {
                bool exists = _cache.Contains(cacheKey, _cacheRegion);
                return Task.FromResult(exists);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Clears all items from the cache
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _lock.EnterWriteLock();
            try
            {
                // Get all keys for the region
                var regionKeys = new List<string>();
                foreach (var item in _cache)
                {
                    if (item is KeyValuePair<string, object> kvp &&
                        _cache.GetCacheItem(kvp.Key)?.RegionName == _cacheRegion)
                    {
                        regionKeys.Add(kvp.Key);
                    }
                }
                
                // Remove all items in the region
                foreach (var key in regionKeys)
                {
                    _cache.Remove(key, _cacheRegion);
                }
                
                // Reset memory usage
                Interlocked.Exchange(ref _currentMemorySize, 0);
                
                _logger.LogInformation("Cleared all items from memory cache region {Region}", _cacheRegion);
                return Task.CompletedTask;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Gets or sets an item in the cache, using a factory method to create it if not found
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
            
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            
            if (valueFactory == null)
            {
                throw new ArgumentNullException(nameof(valueFactory));
            }
            
            // First, try to get from cache
            var result = await GetAsync(key, cancellationToken);
            if (!EqualityComparer<TValue>.Default.Equals(result, default))
            {
                return result;
            }
            
            // Not in cache, acquire a lock specific to this key to prevent multiple factory calls
            var keyLock = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await keyLock.WaitAsync(cancellationToken);
            try
            {
                // Check cache again after acquiring lock (double-check pattern)
                result = await GetAsync(key, cancellationToken);
                if (!EqualityComparer<TValue>.Default.Equals(result, default))
                {
                    return result;
                }
                
                // Create new value
                _logger.LogDebug("Creating new cache value for key {Key} using factory method", key);
                result = await valueFactory();
                
                // Cache the result if it's not null
                if (!EqualityComparer<TValue>.Default.Equals(result, default))
                {
                    await SetAsync(key, result, options, cancellationToken);
                }
                
                return result;
            }
            finally
            {
                keyLock.Release();
                
                // Cleanup the lock if no longer needed
                if (_keyLocks.TryRemove(key, out var removedLock))
                {
                    removedLock.Dispose();
                }
            }
        }
        
        /// <summary>
        /// Gets current cache statistics
        /// </summary>
        /// <returns>Cache statistics</returns>
        public CacheStatistics GetStatistics()
        {
            _lock.EnterReadLock();
            try
            {
                return new CacheStatistics
                {
                    ItemCount = _cache.GetCount(),
                    MemoryUsageBytes = _currentMemorySize,
                    MemoryLimitBytes = _memorySizeLimit,
                    CacheType = "Memory",
                    CacheRegion = _cacheRegion
                };
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        #region Private Methods
        
        /// <summary>
        /// Creates a cache policy from the cache entry options
        /// </summary>
        private CacheItemPolicy CreateCachePolicy(CacheEntryOptions options)
        {
            var policy = new CacheItemPolicy();
            
            // Set expiration
            if (options.AbsoluteExpiration.HasValue)
            {
                policy.AbsoluteExpiration = options.AbsoluteExpiration.Value;
            }
            
            if (options.SlidingExpiration.HasValue)
            {
                policy.SlidingExpiration = options.SlidingExpiration.Value;
            }
            
            // Set priority
            switch (options.Priority)
            {
                case CacheItemPriority.Low:
                    policy.Priority = (System.Runtime.Caching.CacheItemPriority)CacheItemPriority.Normal;
                    break;
                case CacheItemPriority.Normal:
                    policy.Priority = (System.Runtime.Caching.CacheItemPriority)CacheItemPriority.Normal;
                    break;
                case CacheItemPriority.High:
                    policy.Priority = (System.Runtime.Caching.CacheItemPriority)CacheItemPriority.High;
                    break;
                case CacheItemPriority.NeverRemove:
                    policy.Priority = (System.Runtime.Caching.CacheItemPriority)CacheItemPriority.NeverRemove;
                    break;
            }
            
            return policy;
        }
        
        /// <summary>
        /// Formats a cache key based on the type and region
        /// </summary>
        private string GetFormattedCacheKey(TKey key)
        {
            return $"{typeof(TValue).Name}:{key}";
        }
        
        /// <summary>
        /// Removes items from cache to make room for a new item
        /// </summary>
        private void MakeRoomInCache(long requiredSize)
        {
            _logger.LogDebug("Making room in cache for {RequiredSize} bytes", requiredSize);
            
            // Get items that can be removed (not NotRemovable priority)
            var removableItems = new List<KeyValuePair<string, CacheItem<TValue>>>();
            foreach (var item in _cache)
            {
                if (item is KeyValuePair<string, object> kvp &&
                    _cache.GetCacheItem(kvp.Key)?.RegionName == _cacheRegion &&
                    kvp.Value is CacheItem<TValue> cacheItem)
                {
                    removableItems.Add(new KeyValuePair<string, CacheItem<TValue>>(kvp.Key, cacheItem));
                }
            }
            
            // Sort by last accessed time only since we can't access Priority directly anymore
            var itemsToRemove = removableItems
                .OrderBy(i => i.Key) // TODO: Replace with a real eviction policy property
                .ToList();
                
            long freedSpace = 0;
            foreach (var item in itemsToRemove)
            {
                if (_currentMemorySize + requiredSize - freedSpace <= _memorySizeLimit)
                {
                    break;
                }
                
                if (item.Value.Size.HasValue)
                {
                    freedSpace += item.Value.Size.Value;
                }
                
                _cache.Remove(item.Key, _cacheRegion);
                _logger.LogTrace("Removed item {Key} to free up space", item.Key);
            }
            
            // Update memory usage
            Interlocked.Add(ref _currentMemorySize, -freedSpace);
            _logger.LogDebug("Freed {FreedSpace} bytes from cache", freedSpace);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Wrapper for cached items with additional metadata
    /// </summary>
    /// <typeparam name="T">Type of the cached value</typeparam>
    internal class CacheItem<T>
    {
        /// <summary>
        /// The cached value
        /// </summary>
        public T Value { get; }
        
        /// <summary>
        /// Size of the item in bytes (if known)
        /// </summary>
        public long? Size { get; }
        
        /// <summary>
        /// Creates a new instance of CacheItem
        /// </summary>
        /// <param name="value">The value to cache</param>
        /// <param name="size">Optional size in bytes</param>
        public CacheItem(T value, long? size = null)
        {
            Value = value;
            Size = size;
        }
    }
    
    /// <summary>
    /// Statistics about the cache
    /// </summary>
    public class CacheStatistics
    {
        /// <summary>
        /// Number of items in the cache
        /// </summary>
        public long ItemCount { get; set; }
        
        /// <summary>
        /// Memory usage in bytes
        /// </summary>
        public long MemoryUsageBytes { get; set; }
        
        /// <summary>
        /// Memory limit in bytes
        /// </summary>
        public long MemoryLimitBytes { get; set; }
        
        /// <summary>
        /// Type of cache (Memory, Disk)
        /// </summary>
        public string CacheType { get; set; }
        
        /// <summary>
        /// Cache region name
        /// </summary>
        public string CacheRegion { get; set; }
        
        /// <summary>
        /// Percentage of memory used
        /// </summary>
        public double MemoryUsagePercentage => 
            MemoryLimitBytes > 0 ? (double)MemoryUsageBytes / MemoryLimitBytes * 100 : 0;
    }
}
