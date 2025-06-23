using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TransportTracker.Core.Parallel
{
    /// <summary>
    /// Extension methods for parallel query operations
    /// </summary>
    public static class ParallelQueryExtensions
    {
        /// <summary>
        /// Specifies the degree of parallelism to use in a parallel query
        /// </summary>
        /// <typeparam name="TSource">Type of the elements in the source</typeparam>
        /// <param name="source">Source parallel query</param>
        /// <param name="degreeOfParallelism">The maximum degree of parallelism to use</param>
        /// <returns>A parallel query with the specified degree of parallelism</returns>
        public static ParallelQuery<TSource> WithDegreeOfParallelism<TSource>(
            this ParallelQuery<TSource> source, 
            int degreeOfParallelism)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
                
            return System.Linq.ParallelEnumerable.WithDegreeOfParallelism(source, degreeOfParallelism);
        }
        
        /// <summary>
        /// Executes a specified action on each element of a parallel sequence
        /// </summary>
        /// <typeparam name="TSource">Type of the elements in the source</typeparam>
        /// <param name="source">Source parallel query</param>
        /// <param name="action">The action to perform on each element</param>
        public static void ForEach<TSource>(
            this ParallelQuery<TSource> source,
            Action<TSource> action)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (action == null)
                throw new ArgumentNullException(nameof(action));
                
            System.Linq.ParallelEnumerable.ForAll(source, action);
        }
        
        /// <summary>
        /// Executes a specified action on each element of a parallel sequence with element index
        /// </summary>
        /// <typeparam name="TSource">Type of the elements in the source</typeparam>
        /// <param name="source">Source parallel query</param>
        /// <param name="action">The action to perform on each element</param>
        public static void ForEach<TSource>(
            this ParallelQuery<TSource> source,
            Action<TSource, int> action)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            
            int index = 0;
            System.Linq.ParallelEnumerable.ForAll(source, item => 
            {
                // This is not thread-safe for the index, but it's a best-effort
                // implementation since ParallelEnumerable.ForAll doesn't provide an indexed version
                action(item, Interlocked.Increment(ref index) - 1);
            });
        }
        
        /// <summary>
        /// Executes a specified action on each element of a parallel sequence with cancellation support
        /// </summary>
        /// <typeparam name="TSource">Type of the elements in the source</typeparam>
        /// <param name="source">Source parallel query</param>
        /// <param name="action">The action to perform on each element</param>
        /// <param name="cancellationToken">Cancellation token to observe</param>
        public static void ForEach<TSource>(
            this ParallelQuery<TSource> source,
            Action<TSource> action,
            CancellationToken cancellationToken)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            
            System.Linq.ParallelEnumerable.ForAll(source, item => 
            {
                cancellationToken.ThrowIfCancellationRequested();
                action(item);
            });
        }
    }
}
