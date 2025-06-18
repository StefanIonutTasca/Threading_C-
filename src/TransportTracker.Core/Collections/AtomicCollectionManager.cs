using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Collections
{
    /// <summary>
    /// Implementation of IAtomicCollectionManager that provides thread-safe collection operations
    /// with proper UI synchronization and change notification batching
    /// </summary>
    /// <typeparam name="TKey">Type of the collection key</typeparam>
    /// <typeparam name="TItem">Type of items in the collection</typeparam>
    public class AtomicCollectionManager<TKey, TItem> : IAtomicCollectionManager<TKey, TItem> where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, ThreadSafeObservableCollection<TItem>> _collections = new();
        private readonly ConcurrentDictionary<TKey, SemaphoreSlim> _collectionLocks = new();
        private readonly ILogger _logger;
        
        /// <summary>
        /// Creates a new AtomicCollectionManager
        /// </summary>
        /// <param name="logger">Optional logger for diagnostics</param>
        public AtomicCollectionManager(ILogger logger = null)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// Gets a thread-safe observable collection by key, creating it if it doesn't exist
        /// </summary>
        /// <param name="key">Collection identifier</param>
        /// <returns>Thread-safe observable collection</returns>
        public ThreadSafeObservableCollection<TItem> GetCollection(TKey key)
        {
            return _collections.GetOrAdd(key, k => new ThreadSafeObservableCollection<TItem>(_logger));
        }
        
        /// <summary>
        /// Updates a collection with new items atomically
        /// </summary>
        /// <param name="key">Collection identifier</param>
        /// <param name="newItems">New items to add to the collection</param>
        /// <param name="preserveExistingItems">Whether to preserve existing items (true) or replace all (false)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Task representing the operation</returns>
        public async Task UpdateCollectionAsync(TKey key, IEnumerable<TItem> newItems, bool preserveExistingItems = false, 
            CancellationToken cancellationToken = default)
        {
            if (newItems == null)
            {
                throw new ArgumentNullException(nameof(newItems));
            }
            
            var collection = GetCollection(key);
            var collectionLock = _collectionLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            
            await collectionLock.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                _logger?.LogTrace("Updating collection {Key} with {Count} items", key, newItems.Count());
                
                collection.BatchUpdate(() => 
                {
                    if (!preserveExistingItems)
                    {
                        // Replace everything
                        collection.Clear();
                        foreach (var item in newItems)
                        {
                            collection.Add(item);
                        }
                    }
                    else
                    {
                        // Add only new items
                        var existingItems = collection.ToList();
                        foreach (var item in newItems.Where(i => !existingItems.Contains(i)))
                        {
                            collection.Add(item);
                        }
                    }
                });
            }
            finally
            {
                collectionLock.Release();
            }
        }
        
        /// <summary>
        /// Conditionally updates specific items in the collection based on a predicate
        /// </summary>
        /// <param name="key">Collection identifier</param>
        /// <param name="itemUpdates">Dictionary of item updates with predicates to match existing items</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Number of items updated</returns>
        public async Task<int> UpdateItemsConditionallyAsync(TKey key, IDictionary<Predicate<TItem>, TItem> itemUpdates,
            CancellationToken cancellationToken = default)
        {
            if (itemUpdates == null || itemUpdates.Count == 0)
            {
                return 0;
            }
            
            var collection = GetCollection(key);
            var collectionLock = _collectionLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            
            await collectionLock.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                int updatedCount = 0;
                collection.BatchUpdate(() =>
                {
                    // Get a snapshot of the collection
                    var items = collection.ToList();
                    
                    // Keep track of indices to update
                    var indicesToUpdate = new Dictionary<int, TItem>();
                    
                    // Find all items matching the predicates
                    for (int i = 0; i < items.Count; i++)
                    {
                        foreach (var update in itemUpdates)
                        {
                            if (update.Key(items[i]))
                            {
                                indicesToUpdate[i] = update.Value;
                                break;
                            }
                        }
                    }
                    
                    // Apply updates
                    foreach (var update in indicesToUpdate)
                    {
                        collection[update.Key] = update.Value;
                    }
                    
                    updatedCount = indicesToUpdate.Count;
                });
                
                _logger?.LogTrace("Updated {Count} items in collection {Key}", updatedCount, key);
                return updatedCount;
            }
            finally
            {
                collectionLock.Release();
            }
        }
        
        /// <summary>
        /// Adds items to a collection if they don't already exist (thread-safe)
        /// </summary>
        /// <param name="key">Collection identifier</param>
        /// <param name="items">Items to add</param>
        /// <param name="uniquenessPredicate">Optional predicate to determine uniqueness</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Number of items added</returns>
        public async Task<int> AddUniqueItemsAsync(TKey key, IEnumerable<TItem> items, 
            Func<TItem, TItem, bool> uniquenessPredicate = null,
            CancellationToken cancellationToken = default)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }
            
            var collection = GetCollection(key);
            var collectionLock = _collectionLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            
            await collectionLock.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var itemsList = items.ToList();
                if (itemsList.Count == 0)
                {
                    return 0;
                }
                
                int addedCount = 0;
                collection.BatchUpdate(() => 
                {
                    foreach (var item in itemsList)
                    {
                        bool exists = false;
                        
                        if (uniquenessPredicate != null)
                        {
                            // Use custom uniqueness logic
                            for (int i = 0; i < collection.Count; i++)
                            {
                                if (uniquenessPredicate(collection[i], item))
                                {
                                    exists = true;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // Use default equality comparison
                            exists = collection.Contains(item);
                        }
                        
                        if (!exists)
                        {
                            collection.Add(item);
                            addedCount++;
                        }
                    }
                });
                
                _logger?.LogTrace("Added {Count} unique items to collection {Key}", addedCount, key);
                return addedCount;
            }
            finally
            {
                collectionLock.Release();
            }
        }
        
        /// <summary>
        /// Removes items from a collection based on a predicate (thread-safe)
        /// </summary>
        /// <param name="key">Collection identifier</param>
        /// <param name="predicate">Predicate to determine which items to remove</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Number of items removed</returns>
        public async Task<int> RemoveItemsAsync(TKey key, Predicate<TItem> predicate,
            CancellationToken cancellationToken = default)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }
            
            if (!_collections.TryGetValue(key, out var collection))
            {
                return 0;
            }
            
            var collectionLock = _collectionLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            
            await collectionLock.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                int removedCount = 0;
                collection.BatchUpdate(() => 
                {
                    for (int i = collection.Count - 1; i >= 0; i--)
                    {
                        if (predicate(collection[i]))
                        {
                            collection.RemoveAt(i);
                            removedCount++;
                        }
                    }
                });
                
                _logger?.LogTrace("Removed {Count} items from collection {Key}", removedCount, key);
                return removedCount;
            }
            finally
            {
                collectionLock.Release();
            }
        }
        
        /// <summary>
        /// Clears a collection (thread-safe)
        /// </summary>
        /// <param name="key">Collection identifier</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Task representing the operation</returns>
        public async Task ClearCollectionAsync(TKey key, CancellationToken cancellationToken = default)
        {
            if (!_collections.TryGetValue(key, out var collection))
            {
                return;
            }
            
            var collectionLock = _collectionLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            
            await collectionLock.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                collection.Clear();
                _logger?.LogTrace("Cleared collection {Key}", key);
            }
            finally
            {
                collectionLock.Release();
            }
        }
        
        /// <summary>
        /// Removes a collection from the manager
        /// </summary>
        /// <param name="key">Collection identifier</param>
        /// <returns>True if collection was removed, false if it didn't exist</returns>
        public bool RemoveCollection(TKey key)
        {
            if (_collectionLocks.TryRemove(key, out var semaphore))
            {
                semaphore.Dispose();
            }
            
            bool result = _collections.TryRemove(key, out _);
            if (result)
            {
                _logger?.LogTrace("Removed collection {Key} from manager", key);
            }
            
            return result;
        }
    }
}
