using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Collections
{
    /// <summary>
    /// Thread-safe dictionary implementation with notification support for MVVM scenarios
    /// </summary>
    /// <typeparam name="TKey">Type of keys</typeparam>
    /// <typeparam name="TValue">Type of values</typeparam>
    public class ThreadSafeDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyThreadSafeDictionary<TKey, TValue> where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, TValue> _dictionary;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly ILogger _logger;
        
        // Events for notification
        public event EventHandler<KeyValuePair<TKey, TValue>> ItemAdded;
        public event EventHandler<TKey> ItemRemoved;
        public event EventHandler<KeyValuePair<TKey, TValue>> ItemUpdated;
        public event EventHandler CollectionChanged;
        
        /// <summary>
        /// Creates a new thread-safe dictionary
        /// </summary>
        /// <param name="logger">Optional logger for diagnostics</param>
        public ThreadSafeDictionary(ILogger logger = null)
        {
            _dictionary = new ConcurrentDictionary<TKey, TValue>();
            _logger = logger;
        }
        
        /// <summary>
        /// Creates a new thread-safe dictionary with initial capacity
        /// </summary>
        /// <param name="capacity">Initial capacity</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        public ThreadSafeDictionary(int capacity, ILogger logger = null)
        {
            _dictionary = new ConcurrentDictionary<TKey, TValue>(Environment.ProcessorCount, capacity);
            _logger = logger;
        }
        
        /// <summary>
        /// Creates a new thread-safe dictionary with initial items
        /// </summary>
        /// <param name="dictionary">Initial dictionary to copy</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        public ThreadSafeDictionary(IDictionary<TKey, TValue> dictionary, ILogger logger = null)
        {
            _dictionary = new ConcurrentDictionary<TKey, TValue>(dictionary);
            _logger = logger;
        }
        
        /// <summary>
        /// Adds a new key-value pair to the dictionary
        /// </summary>
        /// <param name="key">The key of the element to add</param>
        /// <param name="value">The value of the element to add</param>
        public void Add(TKey key, TValue value)
        {
            _lock.EnterWriteLock();
            try
            {
                _dictionary.TryAdd(key, value);
                OnItemAdded(new KeyValuePair<TKey, TValue>(key, value));
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Adds a new key-value pair to the dictionary
        /// </summary>
        /// <param name="item">The key-value pair to add</param>
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }
        
        /// <summary>
        /// Removes all items from the dictionary
        /// </summary>
        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _dictionary.Clear();
                OnCollectionChanged();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Determines whether the dictionary contains a specific key-value pair
        /// </summary>
        /// <param name="item">The key-value pair to locate</param>
        /// <returns>True if found, otherwise false</returns>
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            _lock.EnterReadLock();
            try
            {
                return ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).Contains(item);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Determines whether the dictionary contains a specific key
        /// </summary>
        /// <param name="key">The key to locate</param>
        /// <returns>True if found, otherwise false</returns>
        public bool ContainsKey(TKey key)
        {
            _lock.EnterReadLock();
            try
            {
                return _dictionary.ContainsKey(key);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Copies the elements to an array, starting at a particular index
        /// </summary>
        /// <param name="array">The destination array</param>
        /// <param name="arrayIndex">The starting index</param>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            _lock.EnterReadLock();
            try
            {
                ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).CopyTo(array, arrayIndex);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Gets the number of elements in the dictionary
        /// </summary>
        public int Count
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _dictionary.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }
        
        /// <summary>
        /// Gets a value indicating whether the dictionary is read-only
        /// </summary>
        public bool IsReadOnly => false;
        
        /// <summary>
        /// Removes the element with the specified key
        /// </summary>
        /// <param name="key">The key to remove</param>
        /// <returns>True if removed, otherwise false</returns>
        public bool Remove(TKey key)
        {
            _lock.EnterWriteLock();
            try
            {
                bool result = _dictionary.TryRemove(key, out _);
                if (result)
                {
                    OnItemRemoved(key);
                }
                return result;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Removes the element with the specified key-value pair
        /// </summary>
        /// <param name="item">The key-value pair to remove</param>
        /// <returns>True if removed, otherwise false</returns>
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            _lock.EnterWriteLock();
            try
            {
                var removed = ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).Remove(item);
                if (removed)
                {
                    OnItemRemoved(item.Key);
                }
                return removed;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Gets the value associated with the specified key
        /// </summary>
        /// <param name="key">The key to get the value for</param>
        /// <param name="value">The value found, or default if not found</param>
        /// <returns>True if found, otherwise false</returns>
        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            _lock.EnterReadLock();
            try
            {
                return _dictionary.TryGetValue(key, out value);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Gets or sets the value associated with the specified key
        /// </summary>
        /// <param name="key">The key to get or set</param>
        /// <returns>The value associated with the key</returns>
        public TValue this[TKey key]
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _dictionary[key];
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
            set
            {
                _lock.EnterWriteLock();
                try
                {
                    bool updated = _dictionary.ContainsKey(key);
                    _dictionary[key] = value;
                    
                    if (updated)
                    {
                        OnItemUpdated(new KeyValuePair<TKey, TValue>(key, value));
                    }
                    else
                    {
                        OnItemAdded(new KeyValuePair<TKey, TValue>(key, value));
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }
        
        /// <summary>
        /// Gets a collection containing the keys in the dictionary
        /// </summary>
        public ICollection<TKey> Keys
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _dictionary.Keys.ToList();
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }
        
        /// <summary>
        /// Gets a collection containing the values in the dictionary
        /// </summary>
        public ICollection<TValue> Values
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _dictionary.Values.ToList();
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }
        
        /// <summary>
        /// Gets an enumerator for the dictionary
        /// </summary>
        /// <returns>Dictionary enumerator</returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            _lock.EnterReadLock();
            try
            {
                // Return a snapshot to avoid threading issues
                var snapshot = _dictionary.ToArray();
                _lock.ExitReadLock();
                return ((IEnumerable<KeyValuePair<TKey, TValue>>)snapshot).GetEnumerator();
            }
            catch
            {
                _lock.ExitReadLock();
                throw;
            }
        }
        
        /// <summary>
        /// Gets an enumerator for the dictionary
        /// </summary>
        /// <returns>Dictionary enumerator</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        
        /// <summary>
        /// Performs an atomic add or update operation
        /// </summary>
        /// <param name="key">The key to add or update</param>
        /// <param name="addValue">The value to add if the key does not exist</param>
        /// <param name="updateValueFactory">Function to update the value if the key exists</param>
        /// <returns>The new value</returns>
        public TValue AddOrUpdate(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory)
        {
            _lock.EnterWriteLock();
            try
            {
                bool exists = _dictionary.ContainsKey(key);
                var result = _dictionary.AddOrUpdate(key, addValue, updateValueFactory);
                
                if (exists)
                {
                    OnItemUpdated(new KeyValuePair<TKey, TValue>(key, result));
                }
                else
                {
                    OnItemAdded(new KeyValuePair<TKey, TValue>(key, result));
                }
                
                return result;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Performs an atomic get or add operation
        /// </summary>
        /// <param name="key">The key to get or add</param>
        /// <param name="valueFactory">Function to create the value if the key does not exist</param>
        /// <returns>The existing or new value</returns>
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_dictionary.TryGetValue(key, out var existingValue))
                {
                    return existingValue;
                }
                
                _lock.EnterWriteLock();
                try
                {
                    var result = _dictionary.GetOrAdd(key, valueFactory);
                    OnItemAdded(new KeyValuePair<TKey, TValue>(key, result));
                    return result;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }
        
        /// <summary>
        /// Updates all items using a batch update function
        /// </summary>
        /// <param name="updateAction">Action to apply to the dictionary</param>
        public void BatchUpdate(Action<ConcurrentDictionary<TKey, TValue>> updateAction)
        {
            if (updateAction == null)
            {
                throw new ArgumentNullException(nameof(updateAction));
            }
            
            _lock.EnterWriteLock();
            try
            {
                updateAction(_dictionary);
                OnCollectionChanged();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        protected virtual void OnItemAdded(KeyValuePair<TKey, TValue> item)
        {
            ItemAdded?.Invoke(this, item);
            OnCollectionChanged();
        }
        
        protected virtual void OnItemRemoved(TKey key)
        {
            ItemRemoved?.Invoke(this, key);
            OnCollectionChanged();
        }
        
        protected virtual void OnItemUpdated(KeyValuePair<TKey, TValue> item)
        {
            ItemUpdated?.Invoke(this, item);
            OnCollectionChanged();
        }
        
        protected virtual void OnCollectionChanged()
        {
            CollectionChanged?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Takes a snapshot of the dictionary
        /// </summary>
        /// <returns>A copy of the current dictionary state</returns>
        public Dictionary<TKey, TValue> ToSnapshot()
        {
            _lock.EnterReadLock();
            try
            {
                return new Dictionary<TKey, TValue>(_dictionary);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Updates all items in the dictionary with data from the provided collection
        /// </summary>
        /// <param name="items">Collection of items to update the dictionary with</param>
        public void UpdateAll(IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }
            
            _lock.EnterWriteLock();
            try
            {
                foreach (var item in items)
                {
                    if (_dictionary.TryGetValue(item.Key, out _))
                    {
                        _dictionary[item.Key] = item.Value;
                        NotifyItemUpdated(item);
                    }
                    else
                    {
                        _dictionary[item.Key] = item.Value;
                        OnItemAdded(item);
                    }
                }
                
                OnCollectionChanged();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Notifies that an item in the dictionary has been updated
        /// </summary>
        /// <param name="item">The updated key-value pair</param>
        public void NotifyItemUpdated(KeyValuePair<TKey, TValue> item)
        {
            OnItemUpdated(item);
        }
        
        
        /// <summary>
        /// Gets an enumerable collection containing the keys for IReadOnlyDictionary implementation
        /// </summary>
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _dictionary.Keys.ToList();
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }
        
        /// <summary>
        /// Gets an enumerable collection containing the values for IReadOnlyDictionary implementation
        /// </summary>
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _dictionary.Values.ToList();
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }
    }
}
