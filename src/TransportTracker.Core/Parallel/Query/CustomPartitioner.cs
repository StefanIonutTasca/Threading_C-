using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Parallel.Query
{
    /// <summary>
    /// Provides custom partitioning strategies for efficient parallel processing
    /// of large transport datasets
    /// </summary>
    public class CustomPartitioner
    {
        private readonly ILogger<CustomPartitioner> _logger;
        
        public CustomPartitioner(ILogger<CustomPartitioner> logger)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// Creates a range partitioner optimized for the given data size and processing options
        /// </summary>
        /// <param name="dataSize">Size of the data collection</param>
        /// <param name="options">Parallel processing options</param>
        /// <returns>An optimized OrderablePartitioner</returns>
        public OrderablePartitioner<Tuple<int, int>> CreateRangePartitioner(
            int dataSize,
            IParallelProcessingOptions options)
        {
            int degreeOfParallelism = options.MaxDegreeOfParallelism ?? Environment.ProcessorCount;
            int chunkSize = DetermineOptimalChunkSize(dataSize, degreeOfParallelism, options);
            
            _logger.LogDebug(
                $"Creating range partitioner with data size: {dataSize}, " +
                $"parallelism: {degreeOfParallelism}, chunk size: {chunkSize}");
                
            return Partitioner.Create(0, dataSize, chunkSize);
        }
        
        /// <summary>
        /// Creates a chunked partitioner for a specific collection with optimized chunk sizes
        /// </summary>
        /// <typeparam name="T">Type of elements in the collection</typeparam>
        /// <param name="source">Source collection</param>
        /// <param name="options">Parallel processing options</param>
        /// <returns>An optimized OrderablePartitioner</returns>
        public OrderablePartitioner<T> CreateChunkedPartitioner<T>(
            IList<T> source,
            IParallelProcessingOptions options)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            
            int degreeOfParallelism = options.MaxDegreeOfParallelism ?? Environment.ProcessorCount;
            int chunkSize = DetermineOptimalChunkSize(source.Count, degreeOfParallelism, options);
            
            _logger.LogDebug(
                $"Creating chunked partitioner for {typeof(T).Name} with count: {source.Count}, " +
                $"parallelism: {degreeOfParallelism}, chunk size: {chunkSize}");
                
            return Partitioner.Create(source, true).WithDegreeOfParallelism(degreeOfParallelism);
        }
        
        /// <summary>
        /// Creates a load-balanced partitioner for data with potential processing hotspots
        /// </summary>
        /// <typeparam name="T">Type of elements in the collection</typeparam>
        /// <param name="source">Source collection</param>
        /// <param name="processingCostEstimator">Function to estimate processing cost for each item</param>
        /// <param name="options">Parallel processing options</param>
        /// <returns>Partitioned data as a collection of arrays</returns>
        public IEnumerable<T[]> CreateLoadBalancedPartitioner<T>(
            IEnumerable<T> source,
            Func<T, int> processingCostEstimator,
            IParallelProcessingOptions options)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            
            if (processingCostEstimator == null)
            {
                throw new ArgumentNullException(nameof(processingCostEstimator));
            }
            
            // Convert to list to avoid multiple enumeration
            List<T> items = source.ToList();
            int degreeOfParallelism = options.MaxDegreeOfParallelism ?? Environment.ProcessorCount;
            
            _logger.LogDebug(
                $"Creating load-balanced partitioner for {typeof(T).Name} with count: {items.Count}, " +
                $"parallelism: {degreeOfParallelism}");
                
            // Sort items by processing cost (descending)
            var sortedItems = items
                .Select(item => new { Item = item, Cost = processingCostEstimator(item) })
                .OrderByDescending(x => x.Cost)
                .ToList();
                
            // Create partitions using a greedy approach to balance processing cost
            var partitions = new List<List<T>>();
            var partitionCosts = new int[degreeOfParallelism];
            
            for (int i = 0; i < degreeOfParallelism; i++)
            {
                partitions.Add(new List<T>());
            }
            
            foreach (var item in sortedItems)
            {
                // Find partition with lowest cost
                int targetPartition = Array.IndexOf(partitionCosts, partitionCosts.Min());
                partitions[targetPartition].Add(item.Item);
                partitionCosts[targetPartition] += item.Cost;
            }
            
            // Convert to array form as it's more efficient for parallel processing
            return partitions.Select(p => p.ToArray());
        }
        
        /// <summary>
        /// Creates a geographic partitioner that organizes data by spatial coordinates
        /// </summary>
        /// <typeparam name="T">Type of elements in the collection</typeparam>
        /// <param name="source">Source collection</param>
        /// <param name="getLatitude">Function to extract latitude from item</param>
        /// <param name="getLongitude">Function to extract longitude from item</param>
        /// <param name="options">Parallel processing options</param>
        /// <returns>Geographically partitioned data</returns>
        public IEnumerable<T[]> CreateGeographicPartitioner<T>(
            IEnumerable<T> source,
            Func<T, double> getLatitude,
            Func<T, double> getLongitude,
            IParallelProcessingOptions options)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            
            // Convert to list to avoid multiple enumeration
            List<T> items = source.ToList();
            int degreeOfParallelism = options.MaxDegreeOfParallelism ?? Environment.ProcessorCount;
            
            // Create a grid of cells
            int gridSize = (int)Math.Ceiling(Math.Sqrt(degreeOfParallelism));
            
            _logger.LogDebug(
                $"Creating geographic partitioner for {typeof(T).Name} with count: {items.Count}, " +
                $"parallelism: {degreeOfParallelism}, grid size: {gridSize}x{gridSize}");
                
            // Find min/max coordinates
            double minLat = items.Min(i => getLatitude(i));
            double maxLat = items.Max(i => getLatitude(i));
            double minLon = items.Min(i => getLongitude(i));
            double maxLon = items.Max(i => getLongitude(i));
            
            // Calculate cell sizes
            double latStep = (maxLat - minLat) / gridSize;
            double lonStep = (maxLon - minLon) / gridSize;
            
            // Create grid cells
            var partitions = new List<List<T>>();
            for (int i = 0; i < gridSize * gridSize; i++)
            {
                partitions.Add(new List<T>());
            }
            
            // Assign each item to a grid cell
            foreach (var item in items)
            {
                double lat = getLatitude(item);
                double lon = getLongitude(item);
                
                int latIdx = latStep <= 0 ? 0 : Math.Min(gridSize - 1, (int)((lat - minLat) / latStep));
                int lonIdx = lonStep <= 0 ? 0 : Math.Min(gridSize - 1, (int)((lon - minLon) / lonStep));
                
                int cellIndex = latIdx * gridSize + lonIdx;
                partitions[cellIndex].Add(item);
            }
            
            // Remove empty partitions and convert to arrays
            return partitions
                .Where(p => p.Count > 0)
                .Select(p => p.ToArray());
        }
        
        /// <summary>
        /// Determines the optimal chunk size for partitioning
        /// </summary>
        private int DetermineOptimalChunkSize(int dataSize, int degreeOfParallelism, IParallelProcessingOptions options)
        {
            // If user specified a chunk size, use that
            if (options.PartitionChunkSize.HasValue && options.PartitionChunkSize.Value > 0)
            {
                return options.PartitionChunkSize.Value;
            }
            
            // For small data sizes, use a smaller chunk to ensure parallelism
            if (dataSize < degreeOfParallelism * 100)
            {
                return Math.Max(1, dataSize / (degreeOfParallelism * 4));
            }
            
            // For memory-constrained environments, use smaller chunks
            if (options.OptimizeForMemory)
            {
                return Math.Max(1, dataSize / (degreeOfParallelism * 16));
            }
            
            // Default strategy: aim for ~4 chunks per processor for good load balancing
            int targetChunks = degreeOfParallelism * 4;
            return Math.Max(1, dataSize / targetChunks);
        }
    }
}
