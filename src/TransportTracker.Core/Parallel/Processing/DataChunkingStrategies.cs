using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Parallel.Processing
{
    /// <summary>
    /// Provides strategies for chunking large datasets for optimal parallel processing
    /// </summary>
    public class DataChunkingStrategies
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Creates a new instance of DataChunkingStrategies
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public DataChunkingStrategies(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Chunks data into equal-sized batches
        /// </summary>
        /// <typeparam name="T">Type of data items</typeparam>
        /// <param name="source">Data source</param>
        /// <param name="batchSize">Size of each batch</param>
        /// <returns>Collection of data batches</returns>
        public IEnumerable<IList<T>> CreateEqualSizedChunks<T>(IEnumerable<T> source, int batchSize)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (batchSize <= 0) throw new ArgumentException("Batch size must be positive", nameof(batchSize));

            var sourceList = source as IList<T> ?? source.ToList();
            int totalItems = sourceList.Count;
            int batchCount = (int)Math.Ceiling((double)totalItems / batchSize);
            
            _logger.LogDebug($"Chunking {totalItems} items into {batchCount} equal-sized batches of ~{batchSize} items each");

            for (int i = 0; i < batchCount; i++)
            {
                int startIndex = i * batchSize;
                int itemsToTake = Math.Min(batchSize, totalItems - startIndex);
                
                yield return sourceList.Skip(startIndex).Take(itemsToTake).ToList();
            }
        }

        /// <summary>
        /// Chunks data to optimize for load balancing across threads
        /// </summary>
        /// <typeparam name="T">Type of data items</typeparam>
        /// <param name="source">Data source</param>
        /// <param name="threads">Number of threads to balance across</param>
        /// <param name="overallocationFactor">Factor to create more chunks than threads</param>
        /// <returns>Collection of data batches</returns>
        public IEnumerable<IList<T>> CreateLoadBalancedChunks<T>(IEnumerable<T> source, int threads, double overallocationFactor = 2.0)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (threads <= 0) throw new ArgumentException("Thread count must be positive", nameof(threads));
            if (overallocationFactor < 1.0) throw new ArgumentException("Overallocation factor must be at least 1.0", nameof(overallocationFactor));

            var sourceList = source as IList<T> ?? source.ToList();
            int totalItems = sourceList.Count;
            int targetBatches = (int)Math.Ceiling(threads * overallocationFactor);
            int batchSize = Math.Max(1, totalItems / targetBatches);
            
            _logger.LogDebug($"Chunking {totalItems} items into {targetBatches} load-balanced batches " +
                           $"(~{batchSize} items each) for {threads} threads with {overallocationFactor}x overallocation");

            return CreateEqualSizedChunks(sourceList, batchSize);
        }

        /// <summary>
        /// Chunks data using adaptive sizing based on item complexity
        /// </summary>
        /// <typeparam name="T">Type of data items</typeparam>
        /// <param name="source">Data source</param>
        /// <param name="complexityEstimator">Function to estimate item processing complexity (higher is more complex)</param>
        /// <param name="targetComplexityPerBatch">Target complexity sum per batch</param>
        /// <returns>Collection of data batches</returns>
        public IEnumerable<IList<T>> CreateAdaptiveChunks<T>(
            IEnumerable<T> source, 
            Func<T, double> complexityEstimator,
            double targetComplexityPerBatch)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (complexityEstimator == null) throw new ArgumentNullException(nameof(complexityEstimator));
            if (targetComplexityPerBatch <= 0) throw new ArgumentException("Target complexity must be positive", nameof(targetComplexityPerBatch));

            var sourceList = source as IList<T> ?? source.ToList();
            int totalItems = sourceList.Count;
            
            _logger.LogDebug($"Creating adaptive chunks from {totalItems} items with target complexity {targetComplexityPerBatch} per batch");

            var currentBatch = new List<T>();
            double currentBatchComplexity = 0;

            foreach (var item in sourceList)
            {
                double itemComplexity = complexityEstimator(item);
                
                // If adding this item would exceed target complexity and we already have items,
                // yield the current batch and start a new one
                if (currentBatch.Count > 0 && currentBatchComplexity + itemComplexity > targetComplexityPerBatch)
                {
                    yield return currentBatch.ToList(); // Return a copy
                    currentBatch.Clear();
                    currentBatchComplexity = 0;
                }
                
                // Add item to batch
                currentBatch.Add(item);
                currentBatchComplexity += itemComplexity;
            }
            
            // Return any remaining items
            if (currentBatch.Count > 0)
            {
                yield return currentBatch;
            }
        }

        /// <summary>
        /// Chunks data based on a grouping key
        /// </summary>
        /// <typeparam name="T">Type of data items</typeparam>
        /// <typeparam name="TKey">Type of grouping key</typeparam>
        /// <param name="source">Data source</param>
        /// <param name="keySelector">Function to extract grouping key</param>
        /// <param name="maxItemsPerChunk">Maximum items per chunk</param>
        /// <returns>Collection of data batches</returns>
        public IEnumerable<IList<T>> CreateKeyBasedChunks<T, TKey>(
            IEnumerable<T> source,
            Func<T, TKey> keySelector,
            int maxItemsPerChunk = 1000)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (maxItemsPerChunk <= 0) throw new ArgumentException("Maximum items per chunk must be positive", nameof(maxItemsPerChunk));

            // Group by key, but keep original order within groups
            var groupedItems = source.GroupBy(keySelector).ToList();
            
            _logger.LogDebug($"Creating key-based chunks with {groupedItems.Count} distinct keys and max {maxItemsPerChunk} items per chunk");

            // For each key group
            foreach (var group in groupedItems)
            {
                var itemsInGroup = group.ToList();
                int itemCount = itemsInGroup.Count;
                
                // If the group is larger than max items per chunk, subdivide it
                if (itemCount > maxItemsPerChunk)
                {
                    foreach (var chunk in CreateEqualSizedChunks(itemsInGroup, maxItemsPerChunk))
                    {
                        yield return chunk;
                    }
                }
                else
                {
                    yield return itemsInGroup;
                }
            }
        }
    }
}
