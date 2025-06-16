using System;

namespace TransportTracker.Core.Caching
{
    /// <summary>
    /// Defines options for cache entries including expiration and priority
    /// </summary>
    public class CacheEntryOptions
    {
        /// <summary>
        /// Gets or sets the absolute expiration date for the cache entry
        /// </summary>
        public DateTimeOffset? AbsoluteExpiration { get; set; }
        
        /// <summary>
        /// Gets or sets the sliding expiration timespan for the cache entry
        /// </summary>
        public TimeSpan? SlidingExpiration { get; set; }
        
        /// <summary>
        /// Gets or sets the priority of the cache entry
        /// </summary>
        public CacheItemPriority Priority { get; set; } = CacheItemPriority.Normal;
        
        /// <summary>
        /// Gets or sets the cache tier where this entry should be stored
        /// </summary>
        public CacheTier Tier { get; set; } = CacheTier.Both;
        
        /// <summary>
        /// Gets or sets the size of the cache entry in bytes
        /// </summary>
        /// <remarks>
        /// Used for optimizing memory usage and determining eviction policies
        /// </remarks>
        public long? Size { get; set; }
        
        /// <summary>
        /// Creates a new instance of CacheEntryOptions with default values
        /// </summary>
        public CacheEntryOptions() { }
        
        /// <summary>
        /// Creates a clone of the current cache entry options
        /// </summary>
        /// <returns>A new CacheEntryOptions instance with the same values</returns>
        public CacheEntryOptions Clone()
        {
            return new CacheEntryOptions
            {
                AbsoluteExpiration = this.AbsoluteExpiration,
                SlidingExpiration = this.SlidingExpiration,
                Priority = this.Priority,
                Tier = this.Tier,
                Size = this.Size
            };
        }
        
        /// <summary>
        /// Creates cache entry options with absolute expiration
        /// </summary>
        /// <param name="absoluteExpiration">The absolute expiration time</param>
        /// <param name="tier">The cache tier to use</param>
        /// <returns>New cache entry options</returns>
        public static CacheEntryOptions WithAbsoluteExpiration(
            DateTimeOffset absoluteExpiration,
            CacheTier tier = CacheTier.Both)
        {
            return new CacheEntryOptions
            {
                AbsoluteExpiration = absoluteExpiration,
                Tier = tier
            };
        }
        
        /// <summary>
        /// Creates cache entry options with sliding expiration
        /// </summary>
        /// <param name="slidingExpiration">The sliding expiration timespan</param>
        /// <param name="tier">The cache tier to use</param>
        /// <returns>New cache entry options</returns>
        public static CacheEntryOptions WithSlidingExpiration(
            TimeSpan slidingExpiration,
            CacheTier tier = CacheTier.Both)
        {
            return new CacheEntryOptions
            {
                SlidingExpiration = slidingExpiration,
                Tier = tier
            };
        }
        
        /// <summary>
        /// Creates cache entry options with the specified priority
        /// </summary>
        /// <param name="priority">The cache item priority</param>
        /// <param name="tier">The cache tier to use</param>
        /// <returns>New cache entry options</returns>
        public static CacheEntryOptions WithPriority(
            CacheItemPriority priority,
            CacheTier tier = CacheTier.Both)
        {
            return new CacheEntryOptions
            {
                Priority = priority,
                Tier = tier
            };
        }
        
        /// <summary>
        /// Creates cache entry options that never expire
        /// </summary>
        /// <param name="tier">The cache tier to use</param>
        /// <returns>New cache entry options</returns>
        public static CacheEntryOptions NeverExpire(CacheTier tier = CacheTier.Both)
        {
            return new CacheEntryOptions
            {
                Tier = tier,
                Priority = CacheItemPriority.High
            };
        }
        
        /// <summary>
        /// Creates cache entry options for short-lived data
        /// </summary>
        /// <param name="tier">The cache tier to use</param>
        /// <returns>New cache entry options with 1 minute expiration</returns>
        public static CacheEntryOptions ShortLived(CacheTier tier = CacheTier.MemoryOnly)
        {
            return new CacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(1),
                Tier = tier,
                Priority = CacheItemPriority.Low
            };
        }
        
        /// <summary>
        /// Creates cache entry options for standard use cases
        /// </summary>
        /// <param name="tier">The cache tier to use</param>
        /// <returns>New cache entry options with 10 minutes expiration</returns>
        public static CacheEntryOptions Standard(CacheTier tier = CacheTier.Both)
        {
            return new CacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(10),
                Tier = tier,
                Priority = CacheItemPriority.Normal
            };
        }
        
        /// <summary>
        /// Creates cache entry options for long-lived data
        /// </summary>
        /// <param name="tier">The cache tier to use</param>
        /// <returns>New cache entry options with 1 hour expiration</returns>
        public static CacheEntryOptions LongLived(CacheTier tier = CacheTier.Both)
        {
            return new CacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromHours(1),
                Tier = tier,
                Priority = CacheItemPriority.High
            };
        }
    }
    
    /// <summary>
    /// Defines priority levels for cache entries
    /// </summary>
    public enum CacheItemPriority
    {
        /// <summary>
        /// Low priority items are removed first during memory pressure
        /// </summary>
        Low,
        
        /// <summary>
        /// Normal priority items are removed after low priority during memory pressure
        /// </summary>
        Normal,
        
        /// <summary>
        /// High priority items are removed last during memory pressure
        /// </summary>
        High,
        
        /// <summary>
        /// NeverRemove items are not automatically removed during memory pressure
        /// </summary>
        NeverRemove
    }
    
    /// <summary>
    /// Defines the tier(s) where a cache entry should be stored
    /// </summary>
    public enum CacheTier
    {
        /// <summary>
        /// Store in memory cache only
        /// </summary>
        MemoryOnly,
        
        /// <summary>
        /// Store in disk cache only
        /// </summary>
        DiskOnly,
        
        /// <summary>
        /// Store in both memory and disk caches
        /// </summary>
        Both
    }
}
