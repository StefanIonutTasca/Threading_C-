using System;
using System.Collections.Concurrent;
using System.Text.Json;
using TransportTracker.App.Core.Diagnostics;

namespace TransportTracker.App.Services.Caching
{
    /// <summary>
    /// In-memory implementation of cache manager
    /// </summary>
    public class MemoryCacheManager : ICacheManager
    {
        private readonly ConcurrentDictionary<string, CacheItem> _cache;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(30);
        
        public MemoryCacheManager()
        {
            _cache = new ConcurrentDictionary<string, CacheItem>();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            // Start automatic cleanup task
            StartCleanupTask();
        }
        
        /// <summary>
        /// Get an item from cache by key with performance monitoring
        /// </summary>
        public T Get<T>(string key) where T : class
        {
            using (PerformanceMonitor.Instance.StartOperation("Cache_Get"))
            {
                try
                {
                    // Check if the key exists
                    if (!_cache.TryGetValue(key, out CacheItem cacheItem))
                        return null;
                    
                    // Check if the item has expired
                    if (DateTime.Now > cacheItem.ExpiryTime)
                    {
                        Remove(key);
                        return null;
                    }
                    
                    // Deserialize the item
                    try
                    {
                        T item = JsonSerializer.Deserialize<T>(cacheItem.JsonData, _jsonOptions);
                        
                        // Update last accessed time
                        cacheItem.LastAccessedTime = DateTime.Now;
                        
                        return item;
                    }
                    catch (JsonException)
                    {
                        // If deserialization fails, remove the item
                        Remove(key);
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    PerformanceMonitor.Instance.RecordFailure("Cache_Get", ex);
                    System.Diagnostics.Debug.WriteLine($"Cache error: {ex.Message}");
                    return null;
                }
            }
        }
        
        /// <summary>
        /// Store an item in cache with optional expiration
        /// </summary>
        public void Set<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            if (value == null)
                return;
            
            using (PerformanceMonitor.Instance.StartOperation("Cache_Set"))
            {
                try
                {
                    // Serialize the item to JSON
                    string jsonData = JsonSerializer.Serialize(value, _jsonOptions);
                    
                    // Create cache item
                    var cacheItem = new CacheItem
                    {
                        JsonData = jsonData,
                        ExpiryTime = DateTime.Now.Add(expiration ?? _defaultExpiration),
                        LastAccessedTime = DateTime.Now,
                        Size = jsonData.Length
                    };
                    
                    // Store in cache
                    _cache[key] = cacheItem;
                }
                catch (Exception ex)
                {
                    PerformanceMonitor.Instance.RecordFailure("Cache_Set", ex);
                    System.Diagnostics.Debug.WriteLine($"Cache error: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Remove an item from cache
        /// </summary>
        public void Remove(string key)
        {
            using (PerformanceMonitor.Instance.StartOperation("Cache_Remove"))
            {
                _cache.TryRemove(key, out _);
            }
        }
        
        /// <summary>
        /// Check if an item exists in cache
        /// </summary>
        public bool Contains(string key)
        {
            using (PerformanceMonitor.Instance.StartOperation("Cache_Contains"))
            {
                if (!_cache.TryGetValue(key, out CacheItem cacheItem))
                    return false;
                
                // Check if the item has expired
                if (DateTime.Now > cacheItem.ExpiryTime)
                {
                    Remove(key);
                    return false;
                }
                
                return true;
            }
        }
        
        /// <summary>
        /// Clear all items from cache
        /// </summary>
        public void Clear()
        {
            using (PerformanceMonitor.Instance.StartOperation("Cache_Clear"))
            {
                _cache.Clear();
            }
        }
        
        /// <summary>
        /// Start background task to clean up expired items
        /// </summary>
        private void StartCleanupTask()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        // Register this thread with the performance monitor
                        PerformanceMonitor.Instance.RegisterCurrentThread(
                            "CacheCleanupThread", 
                            ThreadCategory.BackgroundTask);
                        
                        // Remove expired items
                        foreach (var key in _cache.Keys)
                        {
                            if (_cache.TryGetValue(key, out CacheItem cacheItem) && 
                                DateTime.Now > cacheItem.ExpiryTime)
                            {
                                _cache.TryRemove(key, out _);
                            }
                        }
                        
                        // Wait 5 minutes before checking again
                        await Task.Delay(TimeSpan.FromMinutes(5));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Cache cleanup error: {ex.Message}");
                        await Task.Delay(TimeSpan.FromMinutes(1));
                    }
                }
            });
        }
        
        /// <summary>
        /// Represents an item stored in cache
        /// </summary>
        private class CacheItem
        {
            public string JsonData { get; set; }
            public DateTime ExpiryTime { get; set; }
            public DateTime LastAccessedTime { get; set; }
            public int Size { get; set; }
        }
    }
}
