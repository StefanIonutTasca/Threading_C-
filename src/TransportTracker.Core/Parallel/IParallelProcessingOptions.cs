using System;

namespace TransportTracker.Core.Parallel
{
    /// <summary>
    /// Defines configuration options for parallel processing operations
    /// </summary>
    public interface IParallelProcessingOptions
    {
        /// <summary>
        /// Gets or sets the maximum degree of parallelism
        /// </summary>
        /// <remarks>
        /// When not specified or less than 1, the system will use Environment.ProcessorCount
        /// </remarks>
        int? MaxDegreeOfParallelism { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the query should preserve ordering
        /// </summary>
        bool PreserveOrdering { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the operation should
        /// use a custom partitioner for performance optimization
        /// </summary>
        bool UseCustomPartitioner { get; set; }
        
        /// <summary>
        /// Gets or sets the target chunk size for partitioning operations
        /// </summary>
        int? PartitionChunkSize { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether to optimize for memory usage
        /// </summary>
        /// <remarks>
        /// When true, operations will be optimized for lower memory usage at the
        /// potential cost of decreased performance
        /// </remarks>
        bool OptimizeForMemory { get; set; }
        
        /// <summary>
        /// Gets or sets the cancellation token source for the operation
        /// </summary>
        System.Threading.CancellationTokenSource CancellationTokenSource { get; set; }
        
        /// <summary>
        /// Gets or sets whether to enable task scheduling for large datasets
        /// </summary>
        bool EnableTaskScheduling { get; set; }
    }
}
