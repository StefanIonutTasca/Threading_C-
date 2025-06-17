using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TransportTracker.App.Core.Processing
{
    /// <summary>
    /// Provides utilities for processing data in parallel using TPL
    /// </summary>
    public static class ParallelDataProcessor
    {
        /// <summary>
        /// Processes a collection in parallel with adaptive partitioning
        /// </summary>
        /// <typeparam name="T">The data type</typeparam>
        /// <param name="source">The source collection</param>
        /// <param name="action">The action to perform on each item</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="maxDegreeOfParallelism">Maximum parallelism degree (0 = unlimited)</param>
        public static Task ProcessInParallelAsync<T>(
            IEnumerable<T> source, 
            Action<T> action,
            CancellationToken cancellationToken = default,
            int maxDegreeOfParallelism = 0)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (action == null) throw new ArgumentNullException(nameof(action));
            
            // Set options for parallelism
            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = maxDegreeOfParallelism <= 0 ? -1 : maxDegreeOfParallelism
            };
            
            return Task.Run(() => Parallel.ForEach(source, options, action), cancellationToken);
        }

        /// <summary>
        /// Processes a collection in parallel with adaptive partitioning and returns results
        /// </summary>
        /// <typeparam name="TInput">The input type</typeparam>
        /// <typeparam name="TOutput">The output type</typeparam>
        /// <param name="source">The source collection</param>
        /// <param name="selector">The transform function</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="maxDegreeOfParallelism">Maximum parallelism degree (0 = unlimited)</param>
        /// <returns>The transformed collection</returns>
        public static Task<IEnumerable<TOutput>> ProcessInParallelAsync<TInput, TOutput>(
            IEnumerable<TInput> source, 
            Func<TInput, TOutput> selector,
            CancellationToken cancellationToken = default,
            int maxDegreeOfParallelism = 0)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            
            return Task.Run(() =>
            {
                // Use PLINQ with configured degrees of parallelism
                var query = source.AsParallel();
                
                if (maxDegreeOfParallelism > 0)
                {
                    query = query.WithDegreeOfParallelism(maxDegreeOfParallelism);
                }
                
                return query
                    .WithCancellation(cancellationToken)
                    .Select(selector)
                    .AsEnumerable();
            }, cancellationToken);
        }

        /// <summary>
        /// Creates optimal chunks of data for parallel processing
        /// </summary>
        /// <typeparam name="T">The data type</typeparam>
        /// <param name="source">The source collection</param>
        /// <param name="chunkSize">Size of each chunk (0 = auto-determine)</param>
        /// <returns>Collections chunked for optimal parallel processing</returns>
        public static IEnumerable<IEnumerable<T>> Chunk<T>(IEnumerable<T> source, int chunkSize = 0)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            
            var sourceArray = source.ToArray();
            if (sourceArray.Length == 0) yield break;
            
            // Auto-determine chunk size if not specified
            if (chunkSize <= 0)
            {
                int processorCount = Environment.ProcessorCount;
                int dataCount = sourceArray.Length;
                
                // Use similar logic to BatchProcessor for consistency
                if (dataCount <= 1000)
                    chunkSize = Math.Max(10, dataCount / Math.Max(1, processorCount * 2));
                else if (dataCount <= 10000)
                    chunkSize = Math.Max(50, dataCount / Math.Max(1, processorCount));
                else
                    chunkSize = Math.Max(200, dataCount / Math.Max(1, processorCount / 2));
            }
            
            // Create chunks of appropriate size
            for (int i = 0; i < sourceArray.Length; i += chunkSize)
            {
                yield return sourceArray.Skip(i).Take(Math.Min(chunkSize, sourceArray.Length - i));
            }
        }
    }
}
