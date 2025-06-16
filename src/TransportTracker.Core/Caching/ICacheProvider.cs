using System;
using System.Threading;
using System.Threading.Tasks;

namespace TransportTracker.Core.Caching
{
    /// <summary>
    /// Defines the contract for cache providers in the application
    /// </summary>
    /// <typeparam name="TKey">The type of keys used for cache entries</typeparam>
    /// <typeparam name="TValue">The type of values stored in the cache</typeparam>
    public interface ICacheProvider<TKey, TValue> where TKey : notnull
    {
        /// <summary>
        /// Gets an item from the cache
        /// </summary>
        /// <param name="key">The key of the item to get</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The cached item, or default if not found</returns>
        Task<TValue> GetAsync(TKey key, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Sets an item in the cache with the specified options
        /// </summary>
        /// <param name="key">The key of the item to set</param>
        /// <param name="value">The value to cache</param>
        /// <param name="options">Caching options</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task SetAsync(TKey key, TValue value, CacheEntryOptions options = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Removes an item from the cache
        /// </summary>
        /// <param name="key">The key of the item to remove</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if the item was removed, false if it wasn't in the cache</returns>
        Task<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Checks if an item exists in the cache
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if the item exists, otherwise false</returns>
        Task<bool> ContainsKeyAsync(TKey key, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Clears all items from the cache
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task ClearAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets or sets an item in the cache, using a factory method to create it if not found
        /// </summary>
        /// <param name="key">The key of the item</param>
        /// <param name="valueFactory">Factory method to create the item if not found</param>
        /// <param name="options">Caching options</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The cached or created value</returns>
        Task<TValue> GetOrCreateAsync(
            TKey key, 
            Func<Task<TValue>> valueFactory, 
            CacheEntryOptions options = null,
            CancellationToken cancellationToken = default);
    }
}
