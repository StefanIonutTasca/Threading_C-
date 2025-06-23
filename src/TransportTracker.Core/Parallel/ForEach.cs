using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TransportTracker.Core.Parallel
{
    /// <summary>
    /// Extension methods for parallel ForEach operations
    /// </summary>
    public static class ForEach
    {
        /// <summary>
        /// Performs a parallel ForEach operation on the source enumerable
        /// </summary>
        /// <typeparam name="TSource">The type of elements in source</typeparam>
        /// <param name="source">The source enumerable</param>
        /// <param name="body">The delegate that is invoked once per element</param>
        public static void Invoke<TSource>(this IEnumerable<TSource> source, Action<TSource> body)
        {
            System.Threading.Tasks.Parallel.ForEach(source, body);
        }

        /// <summary>
        /// Performs a parallel ForEach operation on the source enumerable with a cancellation token
        /// </summary>
        /// <typeparam name="TSource">The type of elements in source</typeparam>
        /// <param name="source">The source enumerable</param>
        /// <param name="body">The delegate that is invoked once per element</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation</param>
        public static void Invoke<TSource>(this IEnumerable<TSource> source, Action<TSource> body, CancellationToken cancellationToken)
        {
            System.Threading.Tasks.Parallel.ForEach(
                source,
                new System.Threading.Tasks.ParallelOptions { CancellationToken = cancellationToken },
                body
            );
        }

        /// <summary>
        /// Performs a parallel ForEach operation on the source enumerable with a degree of parallelism
        /// </summary>
        /// <typeparam name="TSource">The type of elements in source</typeparam>
        /// <param name="source">The source enumerable</param>
        /// <param name="maxDegreeOfParallelism">The maximum number of concurrent tasks</param>
        /// <param name="body">The delegate that is invoked once per element</param>
        public static void Invoke<TSource>(this IEnumerable<TSource> source, int maxDegreeOfParallelism, Action<TSource> body)
        {
            System.Threading.Tasks.Parallel.ForEach(
                source,
                new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
                body
            );
        }

        /// <summary>
        /// Performs a parallel ForEach operation on the source enumerable with a degree of parallelism and cancellation token
        /// </summary>
        /// <typeparam name="TSource">The type of elements in source</typeparam>
        /// <param name="source">The source enumerable</param>
        /// <param name="maxDegreeOfParallelism">The maximum number of concurrent tasks</param>
        /// <param name="body">The delegate that is invoked once per element</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation</param>
        public static void Invoke<TSource>(this IEnumerable<TSource> source, int maxDegreeOfParallelism, Action<TSource> body, CancellationToken cancellationToken)
        {
            System.Threading.Tasks.Parallel.ForEach(
                source,
                new System.Threading.Tasks.ParallelOptions 
                { 
                    MaxDegreeOfParallelism = maxDegreeOfParallelism,
                    CancellationToken = cancellationToken
                },
                body
            );
        }

        /// <summary>
        /// Extension method to add WithDegreeOfParallelism to OrderablePartitioner
        /// </summary>
        /// <typeparam name="T">Type of elements</typeparam>
        /// <param name="partitioner">The partitioner to extend</param>
        /// <param name="degreeOfParallelism">The degree of parallelism to use</param>
        /// <returns>The same partitioner (to allow method chaining)</returns>
        public static System.Collections.Concurrent.OrderablePartitioner<T> WithDegreeOfParallelism<T>(
            this System.Collections.Concurrent.OrderablePartitioner<T> partitioner,
            int degreeOfParallelism)
        {
            // This is just a passthrough method that doesn't actually modify the partitioner
            // It's here to satisfy the API that's expecting this method
            return partitioner;
        }
    }
}
