using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TransportTracker.Core.Collections
{
    /// <summary>
    /// Interface for an atomic collection manager that provides thread-safe bulk operations
    /// on collections with proper UI synchronization and change notification batching
    /// </summary>
    /// <typeparam name="TKey">Type of the collection key</typeparam>
    /// <typeparam name="TItem">Type of items in the collection</typeparam>
    public interface IAtomicCollectionManager<TKey, TItem> where TKey : notnull
    {
        /// <summary>
        /// Gets a thread-safe observable collection by key
        /// </summary>
        /// <param name="key">Collection identifier</param>
        /// <returns>Thread-safe observable collection</returns>
        ThreadSafeObservableCollection<TItem> GetCollection(TKey key);
        
        /// <summary>
        /// Updates a collection with new items atomically
        /// </summary>
        /// <param name="key">Collection identifier</param>
        /// <param name="newItems">New items to add to the collection</param>
        /// <param name="preserveExistingItems">Whether to preserve existing items (true) or replace all (false)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Task representing the operation</returns>
        Task UpdateCollectionAsync(TKey key, IEnumerable<TItem> newItems, bool preserveExistingItems = false, 
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Conditionally updates specific items in the collection based on a predicate
        /// </summary>
        /// <param name="key">Collection identifier</param>
        /// <param name="itemUpdates">Dictionary of item updates with predicates to match existing items</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Number of items updated</returns>
        Task<int> UpdateItemsConditionallyAsync(TKey key, IDictionary<Predicate<TItem>, TItem> itemUpdates,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Adds items to a collection if they don't already exist (thread-safe)
        /// </summary>
        /// <param name="key">Collection identifier</param>
        /// <param name="items">Items to add</param>
        /// <param name="uniquenessPredicate">Optional predicate to determine uniqueness</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Number of items added</returns>
        Task<int> AddUniqueItemsAsync(TKey key, IEnumerable<TItem> items, 
            Func<TItem, TItem, bool> uniquenessPredicate = null,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Removes items from a collection based on a predicate (thread-safe)
        /// </summary>
        /// <param name="key">Collection identifier</param>
        /// <param name="predicate">Predicate to determine which items to remove</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Number of items removed</returns>
        Task<int> RemoveItemsAsync(TKey key, Predicate<TItem> predicate,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Clears a collection (thread-safe)
        /// </summary>
        /// <param name="key">Collection identifier</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Task representing the operation</returns>
        Task ClearCollectionAsync(TKey key, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Removes a collection from the manager
        /// </summary>
        /// <param name="key">Collection identifier</param>
        /// <returns>True if collection was removed, false if it didn't exist</returns>
        bool RemoveCollection(TKey key);
    }
}
