using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace TransportTracker.App.Core.MVVM
{
    /// <summary>
    /// Represents a dynamic data collection that provides notifications when items get
    /// added, removed, or when the whole list is refreshed, with optimized operations for handling ranges.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    public class ObservableRangeCollection<T> : ObservableCollection<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObservableRangeCollection{T}"/> class.
        /// </summary>
        public ObservableRangeCollection() : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservableRangeCollection{T}"/> class
        /// that contains elements copied from the specified collection.
        /// </summary>
        /// <param name="collection">The collection from which the elements are copied.</param>
        public ObservableRangeCollection(IEnumerable<T> collection) : base(collection)
        {
        }

        /// <summary>
        /// Adds a range of items to the collection with a single notification at the end.
        /// </summary>
        /// <param name="collection">The collection of items to add.</param>
        /// <param name="notifyCollectionChanged">Whether to notify that the collection has changed.</param>
        public void AddRange(IEnumerable<T> collection, bool notifyCollectionChanged = true)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            CheckReentrancy();

            foreach (var item in collection)
            {
                Items.Add(item);
            }

            if (notifyCollectionChanged)
            {
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        /// <summary>
        /// Removes a range of items from the collection with a single notification at the end.
        /// </summary>
        /// <param name="collection">The collection of items to remove.</param>
        /// <param name="notifyCollectionChanged">Whether to notify that the collection has changed.</param>
        public void RemoveRange(IEnumerable<T> collection, bool notifyCollectionChanged = true)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            CheckReentrancy();

            foreach (var item in collection)
            {
                Items.Remove(item);
            }

            if (notifyCollectionChanged)
            {
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        /// <summary>
        /// Replaces all items in the collection with the items in the specified collection.
        /// </summary>
        /// <param name="collection">The collection of replacement items.</param>
        /// <param name="notifyCollectionChanged">Whether to notify that the collection has changed.</param>
        public void ReplaceRange(IEnumerable<T> collection, bool notifyCollectionChanged = true)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            CheckReentrancy();

            Items.Clear();
            foreach (var item in collection)
            {
                Items.Add(item);
            }

            if (notifyCollectionChanged)
            {
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        /// <summary>
        /// Clears the collection and adds the items from the specified collection.
        /// </summary>
        /// <param name="collection">The collection of items to add.</param>
        /// <param name="notifyCollectionChanged">Whether to notify that the collection has changed.</param>
        public void Reset(IEnumerable<T> collection, bool notifyCollectionChanged = true)
        {
            ReplaceRange(collection, notifyCollectionChanged);
        }
    }
}
