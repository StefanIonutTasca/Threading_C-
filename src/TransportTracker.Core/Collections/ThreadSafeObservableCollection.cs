using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Collections
{
    /// <summary>
    /// A thread-safe implementation of ObservableCollection with batch update capabilities,
    /// optimized for MVVM scenarios where UI updates need to be synchronized with data changes.
    /// </summary>
    /// <typeparam name="T">Type of elements in the collection</typeparam>
    public class ThreadSafeObservableCollection<T> : ObservableCollection<T>
    {
        private readonly SynchronizationContext _synchronizationContext;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly ILogger _logger;
        private bool _suppressNotifications;
        private readonly object _syncRoot = new object();
        
        /// <summary>
        /// Creates a new thread-safe observable collection
        /// </summary>
        /// <param name="logger">Optional logger for diagnostics</param>
        public ThreadSafeObservableCollection(ILogger logger = null)
            : base()
        {
            _logger = logger;
            _synchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();
            _suppressNotifications = false;
        }
        
        /// <summary>
        /// Creates a new thread-safe observable collection with initial items
        /// </summary>
        /// <param name="collection">Initial collection of items</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        public ThreadSafeObservableCollection(IEnumerable<T> collection, ILogger logger = null)
            : base(collection)
        {
            _logger = logger;
            _synchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();
            _suppressNotifications = false;
        }
        
        /// <summary>
        /// Gets a value indicating whether the collection has notifications suppressed
        /// </summary>
        public bool SuppressNotifications
        {
            get { return _suppressNotifications; }
            set
            {
                _lock.EnterWriteLock();
                try
                {
                    if (_suppressNotifications != value)
                    {
                        _suppressNotifications = value;
                        if (!_suppressNotifications)
                        {
                            NotifyCollectionReset();
                        }
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }
        
        /// <summary>
        /// Performs a batch update on the collection without triggering individual notifications
        /// </summary>
        /// <param name="action">The action to perform on the collection</param>
        public void BatchUpdate(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }
            
            _lock.EnterWriteLock();
            bool originalState = _suppressNotifications;
            try
            {
                _suppressNotifications = true;
                action();
            }
            finally
            {
                _suppressNotifications = originalState;
                _lock.ExitWriteLock();
            }
            
            // Notify that the entire collection has changed
            NotifyCollectionReset();
        }
        
        /// <summary>
        /// Updates the collection with new items, optionally preserving selected items
        /// </summary>
        /// <param name="newItems">New collection of items</param>
        /// <param name="preserveItems">Optional function to identify items that should be preserved</param>
        public void UpdateCollection(IEnumerable<T> newItems, Func<T, bool> preserveItems = null)
        {
            if (newItems == null)
            {
                throw new ArgumentNullException(nameof(newItems));
            }
            
            BatchUpdate(() =>
            {
                if (preserveItems == null)
                {
                    // Simple full replacement
                    Clear();
                    foreach (var item in newItems)
                    {
                        Add(item);
                    }
                }
                else
                {
                    // Selective update preserving some items
                    var toPreserve = this.Where(preserveItems).ToList();
                    var newList = newItems.ToList();
                    
                    // Keep track of preserved items
                    var preservedItems = new List<T>();
                    
                    // First remove items that aren't in the new collection
                    for (int i = Count - 1; i >= 0; i--)
                    {
                        var item = this[i];
                        if (preserveItems(item))
                        {
                            preservedItems.Add(item);
                        }
                        else
                        {
                            RemoveItem(i);
                        }
                    }
                    
                    // Add new items that aren't preserved
                    foreach (var item in newList.Where(i => !preservedItems.Contains(i)))
                    {
                        Add(item);
                    }
                }
            });
            
            _logger?.LogDebug("Updated collection with {Count} items", Count);
        }
        
        /// <summary>
        /// Thread-safe implementation of adding an item
        /// </summary>
        /// <param name="item">Item to add</param>
        public new void Add(T item)
        {
            _lock.EnterWriteLock();
            try
            {
                base.Add(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Thread-safe implementation of inserting an item
        /// </summary>
        /// <param name="index">Index to insert at</param>
        /// <param name="item">Item to insert</param>
        protected override void InsertItem(int index, T item)
        {
            _lock.EnterWriteLock();
            try
            {
                base.InsertItem(index, item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Thread-safe implementation of removing an item
        /// </summary>
        /// <param name="index">Index of item to remove</param>
        protected override void RemoveItem(int index)
        {
            _lock.EnterWriteLock();
            try
            {
                base.RemoveItem(index);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Thread-safe implementation of setting an item
        /// </summary>
        /// <param name="index">Index of item to set</param>
        /// <param name="item">New item value</param>
        protected override void SetItem(int index, T item)
        {
            _lock.EnterWriteLock();
            try
            {
                base.SetItem(index, item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Thread-safe implementation of clearing the collection
        /// </summary>
        protected override void ClearItems()
        {
            _lock.EnterWriteLock();
            try
            {
                base.ClearItems();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Raises collection changed notification on the UI thread
        /// </summary>
        /// <param name="e">Event args</param>
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (_suppressNotifications)
            {
                return;
            }
            
            // If we're on the UI thread already, just raise the event
            if (SynchronizationContext.Current == _synchronizationContext)
            {
                base.OnCollectionChanged(e);
            }
            else
            {
                // Marshal to the UI thread
                _synchronizationContext.Post(_ => base.OnCollectionChanged(e), null);
            }
        }
        
        /// <summary>
        /// Raises property changed notification on the UI thread
        /// </summary>
        /// <param name="e">Event args</param>
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (_suppressNotifications)
            {
                return;
            }
            
            // If we're on the UI thread already, just raise the event
            if (SynchronizationContext.Current == _synchronizationContext)
            {
                base.OnPropertyChanged(e);
            }
            else
            {
                // Marshal to the UI thread
                _synchronizationContext.Post(_ => base.OnPropertyChanged(e), null);
            }
        }
        
        /// <summary>
        /// Atomically finds and replaces an item
        /// </summary>
        /// <param name="match">Function to find the item to replace</param>
        /// <param name="newValue">New value to set</param>
        /// <returns>True if found and replaced, false otherwise</returns>
        public bool FindAndReplace(Predicate<T> match, T newValue)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }
            
            _lock.EnterWriteLock();
            try
            {
                for (int i = 0; i < Count; i++)
                {
                    if (match(this[i]))
                    {
                        SetItem(i, newValue);
                        return true;
                    }
                }
                
                return false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Notifies that the entire collection has been reset
        /// </summary>
        private void NotifyCollectionReset()
        {
            var args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
            OnCollectionChanged(args);
        }
        
        /// <summary>
        /// Threadsafe check if the collection contains an item
        /// </summary>
        /// <param name="item">Item to check for</param>
        /// <returns>True if the collection contains the item</returns>
        public new bool Contains(T item)
        {
            _lock.EnterReadLock();
            try
            {
                return base.Contains(item);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Thread-safe count of items
        /// </summary>
        public new int Count
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return base.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }
        
        /// <summary>
        /// Thread-safe indexer
        /// </summary>
        /// <param name="index">Index to get or set</param>
        /// <returns>Item at the specified index</returns>
        public new T this[int index]
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return base[index];
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
                    base[index] = value;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }
    }
}
