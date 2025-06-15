using System.Collections.ObjectModel;

namespace TransportTracker.App.Core.MVVM
{
    /// <summary>
    /// Grouping of items by key into ObservableRange
    /// </summary>
    /// <typeparam name="TKey">Key type for the group</typeparam>
    /// <typeparam name="TItem">Item type for the group items</typeparam>
    public class Grouping<TKey, TItem> : ObservableRangeCollection<TItem>
    {
        /// <summary>
        /// Gets the key of the group
        /// </summary>
        public TKey Key { get; }

        /// <summary>
        /// Gets or sets a short name for the group that can be used for titles
        /// </summary>
        public string ShortName { get; set; }
        
        /// <summary>
        /// Gets or sets the count of items in the group
        /// </summary>
        public int Count => Items.Count;

        /// <summary>
        /// Initializes a new instance of the <see cref="Grouping{TKey,TItem}"/> class.
        /// </summary>
        /// <param name="key">Key for the group</param>
        public Grouping(TKey key)
        {
            Key = key;
            ShortName = key?.ToString();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Grouping{TKey,TItem}"/> class.
        /// </summary>
        /// <param name="key">Key for the group</param>
        /// <param name="shortName">Short display name for the group</param>
        public Grouping(TKey key, string shortName)
        {
            Key = key;
            ShortName = shortName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Grouping{TKey,TItem}"/> class with items.
        /// </summary>
        /// <param name="key">Key for the group</param>
        /// <param name="items">Collection of items for the group</param>
        public Grouping(TKey key, IEnumerable<TItem> items)
            : base(items)
        {
            Key = key;
            ShortName = key?.ToString();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Grouping{TKey,TItem}"/> class with items and custom short name.
        /// </summary>
        /// <param name="key">Key for the group</param>
        /// <param name="shortName">Short display name for the group</param>
        /// <param name="items">Collection of items for the group</param>
        public Grouping(TKey key, string shortName, IEnumerable<TItem> items)
            : base(items)
        {
            Key = key;
            ShortName = shortName;
        }
    }
}
