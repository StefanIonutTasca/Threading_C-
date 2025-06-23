using System;
using System.Collections.Generic;

namespace TransportTracker.Core.Collections
{
    /// <summary>
    /// Interface for a thread-safe read-only dictionary implementation
    /// </summary>
    /// <typeparam name="TKey">Key type</typeparam>
    /// <typeparam name="TValue">Value type</typeparam>
    public interface IReadOnlyThreadSafeDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue> 
        where TKey : notnull
    {
        /// <summary>
        /// Attempts to get a value from the dictionary by key
        /// </summary>
        /// <param name="key">The key to look up</param>
        /// <param name="value">The value if found</param>
        /// <returns>True if the key was found, otherwise false</returns>
        new bool TryGetValue(TKey key, out TValue value);
        
        /// <summary>
        /// Checks if the dictionary contains the specified key
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>True if the key exists, otherwise false</returns>
        new bool ContainsKey(TKey key);
        
        /// <summary>
        /// Takes a snapshot of the dictionary
        /// </summary>
        /// <returns>A copy of the current dictionary state</returns>
        Dictionary<TKey, TValue> ToSnapshot();
    }
}
