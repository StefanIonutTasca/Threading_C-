using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TransportTracker.App.Core.Processing
{
    /// <summary>
    /// Handles batch processing of large datasets using Task Parallel Library
    /// </summary>
    /// <typeparam name="TInput">The input data type</typeparam>
    /// <typeparam name="TOutput">The output data type</typeparam>
    public class BatchProcessor<TInput, TOutput>
    {
        private readonly Func<IEnumerable<TInput>, CancellationToken, Task<IEnumerable<TOutput>>> _processor;
        private readonly IProgress<BatchProcessingProgress> _progress;
        private readonly BatchProcessingOptions _options;

        /// <summary>
        /// Creates a new batch processor with specified options
        /// </summary>
        /// <param name="processor">The batch processing function</param>
        /// <param name="progress">Progress reporting mechanism</param>
        /// <param name="options">Configuration options for batch processing</param>
        public BatchProcessor(
            Func<IEnumerable<TInput>, CancellationToken, Task<IEnumerable<TOutput>>> processor,
            IProgress<BatchProcessingProgress> progress = null,
            BatchProcessingOptions options = null)
        {
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _progress = progress;
            _options = options ?? new BatchProcessingOptions();
        }

        /// <summary>
        /// Processes the input data in optimal batches based on processor configuration
        /// </summary>
        /// <param name="inputData">The complete dataset to process</param>
        /// <param name="cancellationToken">Cancellation token to stop processing</param>
        /// <returns>The processed output data</returns>
        public async Task<IEnumerable<TOutput>> ProcessAsync(IEnumerable<TInput> inputData, CancellationToken cancellationToken = default)
        {
            if (inputData == null)
                return Enumerable.Empty<TOutput>();

            var data = inputData.ToList();
            if (!data.Any())
                return Enumerable.Empty<TOutput>();

            // Report initial progress
            _progress?.Report(new BatchProcessingProgress(0, data.Count, 0));

            // Calculate appropriate batch size
            int batchSize = DetermineBatchSize(data.Count, _options);
            
            // Create batches
            var batches = ChunkData(data, batchSize).ToList();
            
            // Create task list
            var tasks = new List<Task<IEnumerable<TOutput>>>();
            var results = new ConcurrentBag<TOutput>();
            int processedItems = 0;
            
            // Process each batch
            foreach (var batch in batches)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var batchTask = Task.Run(async () =>
                {
                    var batchResult = await _processor(batch, cancellationToken);
                    
                    // Update processed count
                    Interlocked.Add(ref processedItems, batch.Count);
                    
                    // Report progress
                    _progress?.Report(new BatchProcessingProgress(processedItems, data.Count, 
                        (double)processedItems / data.Count));
                    
                    return batchResult;
                }, cancellationToken);
                
                tasks.Add(batchTask);
                
                // Limit maximum concurrent tasks
                if (tasks.Count >= _options.MaxDegreeOfParallelism && _options.MaxDegreeOfParallelism > 0)
                {
                    var completedTask = await Task.WhenAny(tasks);
                    tasks.Remove(completedTask);
                    
                    var batchResults = await completedTask;
                    foreach (var result in batchResults)
                    {
                        results.Add(result);
                    }
                }
            }

            // Wait for remaining tasks
            await Task.WhenAll(tasks);
            
            // Collect results from remaining tasks
            foreach (var task in tasks)
            {
                var batchResults = await task;
                foreach (var result in batchResults)
                {
                    results.Add(result);
                }
            }
            
            // Report final progress
            _progress?.Report(new BatchProcessingProgress(data.Count, data.Count, 1.0));
            
            return results;
        }

        /// <summary>
        /// Splits input data into optimally sized chunks
        /// </summary>
        private static IEnumerable<List<TInput>> ChunkData(List<TInput> data, int chunkSize)
        {
            for (int i = 0; i < data.Count; i += chunkSize)
            {
                yield return data.GetRange(i, Math.Min(chunkSize, data.Count - i));
            }
        }
        
        /// <summary>
        /// Determines optimal batch size based on input data size and system capabilities
        /// </summary>
        private static int DetermineBatchSize(int dataCount, BatchProcessingOptions options)
        {
            // If batch size is set explicitly, use it
            if (options.BatchSize > 0)
                return options.BatchSize;
            
            // Calculate optimal batch size based on data size and processor count
            int processorCount = Environment.ProcessorCount;
            
            // Use adaptive batch sizing based on total data size and available cores
            // For smaller datasets, smaller batches are better to avoid overhead
            // For larger datasets, larger batches reduce task creation overhead
            if (dataCount <= 1000)
                return Math.Max(10, dataCount / Math.Max(1, processorCount * 2));
            else if (dataCount <= 10000)
                return Math.Max(50, dataCount / Math.Max(1, processorCount));
            else
                return Math.Max(200, dataCount / Math.Max(1, processorCount / 2));
        }
    }

    /// <summary>
    /// Configuration options for batch processing
    /// </summary>
    public class BatchProcessingOptions
    {
        /// <summary>
        /// Gets or sets the size of each batch (0 = auto-determine)
        /// </summary>
        public int BatchSize { get; set; }

        /// <summary>
        /// Gets or sets the maximum degree of parallelism (0 = unlimited)
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; }

        /// <summary>
        /// Creates a new options instance with default settings
        /// </summary>
        public BatchProcessingOptions()
        {
            // Default to automatic batch sizing
            BatchSize = 0;
            
            // Default to processor count * 2 for IO-bound operations
            MaxDegreeOfParallelism = Environment.ProcessorCount * 2;
        }
    }

    /// <summary>
    /// Provides progress information for batch processing operations
    /// </summary>
    public class BatchProcessingProgress
    {
        /// <summary>
        /// Number of items processed so far
        /// </summary>
        public int ProcessedItems { get; }
        
        /// <summary>
        /// Total number of items to process
        /// </summary>
        public int TotalItems { get; }
        
        /// <summary>
        /// Percentage complete (0.0 - 1.0)
        /// </summary>
        public double PercentComplete { get; }

        /// <summary>
        /// Creates a new progress report instance
        /// </summary>
        public BatchProcessingProgress(int processedItems, int totalItems, double percentComplete)
        {
            ProcessedItems = processedItems;
            TotalItems = totalItems;
            PercentComplete = percentComplete;
        }
    }
}
