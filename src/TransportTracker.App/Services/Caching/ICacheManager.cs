using System;

namespace TransportTracker.App.Services.Caching
{
    /// <summary>
    /// Interface for cache management
    /// </summary>
    public interface ICacheManager
    {
        /// <summary>
        /// Get an item from cache by key, or null if not found
        /// </summary>
        T Get<T>(string key) where T : class;
        
        /// <summary>
        /// Store an item in cache with optional expiration
        /// </summary>
        void Set<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        
        /// <summary>
        /// Remove an item from cache
        /// </summary>
        void Remove(string key);
        
        /// <summary>
        /// Check if an item exists in cache
        /// </summary>
        bool Contains(string key);
        
        /// <summary>
        /// Clear all items from cache
        /// </summary>
        void Clear();
    }
}
