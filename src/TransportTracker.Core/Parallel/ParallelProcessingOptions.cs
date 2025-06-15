using System;
using System.Threading;

namespace TransportTracker.Core.Parallel
{
    /// <summary>
    /// Default implementation of parallel processing options
    /// </summary>
    public class ParallelProcessingOptions : IParallelProcessingOptions
    {
        /// <summary>
        /// Gets or sets the maximum degree of parallelism
        /// </summary>
        public int? MaxDegreeOfParallelism { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the query should preserve ordering
        /// </summary>
        public bool PreserveOrdering { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the operation should
        /// use a custom partitioner for performance optimization
        /// </summary>
        public bool UseCustomPartitioner { get; set; }
        
        /// <summary>
        /// Gets or sets the target chunk size for partitioning operations
        /// </summary>
        public int? PartitionChunkSize { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether to optimize for memory usage
        /// </summary>
        public bool OptimizeForMemory { get; set; }
        
        /// <summary>
        /// Gets or sets the cancellation token source for the operation
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; set; }
        
        /// <summary>
        /// Gets or sets whether to enable task scheduling for large datasets
        /// </summary>
        public bool EnableTaskScheduling { get; set; }
        
        /// <summary>
        /// Creates a new instance of ParallelProcessingOptions with default settings
        /// </summary>
        public ParallelProcessingOptions()
        {
            // Default to preserving order for predictable results
            PreserveOrdering = true;
            
            // Use custom partitioner by default for performance
            UseCustomPartitioner = true;
            
            // Default partition size - will be optimized in the ParallelDataProcessor
            PartitionChunkSize = 1000;
            
            // Default to creating a new cancellation token source
            CancellationTokenSource = new CancellationTokenSource();
        }
        
        /// <summary>
        /// Creates a configuration optimized for batch processing of large datasets
        /// </summary>
        public static ParallelProcessingOptions CreateBatchProcessingConfiguration()
        {
            return new ParallelProcessingOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                PreserveOrdering = false, // Ordering not critical in batch operations
                UseCustomPartitioner = true,
                PartitionChunkSize = 5000, // Larger chunks for batch operations
                OptimizeForMemory = false,  // Prioritize speed over memory
                EnableTaskScheduling = true
            };
        }
        
        /// <summary>
        /// Creates a configuration optimized for real-time updates
        /// </summary>
        public static ParallelProcessingOptions CreateRealtimeConfiguration()
        {
            return new ParallelProcessingOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount / 2, // Lower to prevent UI thread starvation
                PreserveOrdering = true,    // Ordering important for UI updates
                UseCustomPartitioner = true,
                PartitionChunkSize = 100,   // Smaller chunks for responsive updates
                OptimizeForMemory = true,   // Mobile devices may have memory constraints
                EnableTaskScheduling = false // Less overhead for small operations
            };
        }
        
        /// <summary>
        /// Creates a configuration for memory-constrained environments
        /// </summary>
        public static ParallelProcessingOptions CreateMemoryOptimizedConfiguration()
        {
            return new ParallelProcessingOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
                PreserveOrdering = true,
                UseCustomPartitioner = true,
                PartitionChunkSize = 500,
                OptimizeForMemory = true,
                EnableTaskScheduling = true
            };
        }
    }
}
