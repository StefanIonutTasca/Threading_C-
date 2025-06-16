using System;

namespace TransportTracker.Core.Caching
{
    /// <summary>
    /// Represents a cache entry stored on disk
    /// </summary>
    /// <typeparam name="T">The type of cached value</typeparam>
    [Serializable]
    internal class DiskCacheEntry<T>
    {
        /// <summary>
        /// The cached value
        /// </summary>
        public T Value { get; set; }
        
        /// <summary>
        /// When the entry was created
        /// </summary>
        public DateTime CreationTime { get; set; }
        
        /// <summary>
        /// When the entry was last accessed
        /// </summary>
        public DateTime LastAccessTime { get; set; }
        
        /// <summary>
        /// Absolute expiration time, if any
        /// </summary>
        public DateTimeOffset? AbsoluteExpiration { get; set; }
        
        /// <summary>
        /// Sliding expiration duration, if any
        /// </summary>
        public TimeSpan? SlidingExpiration { get; set; }
        
        /// <summary>
        /// Size of the entry in bytes, if known
        /// </summary>
        public long Size { get; set; }
        
        /// <summary>
        /// Priority of the entry
        /// </summary>
        public CacheItemPriority Priority { get; set; }
    }
}
