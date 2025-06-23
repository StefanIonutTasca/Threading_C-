using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TransportTracker.Core.Threading.Events;

namespace TransportTracker.Core.Parallel.Processing
{
    /// <summary>
    /// Provides batch processing capabilities for large datasets using PLINQ
    /// </summary>
    /// <typeparam name="TInput">Type of input items</typeparam>
    /// <typeparam name="TOutput">Type of output items</typeparam>
    public class BatchProcessor<TInput, TOutput>
    {
        private readonly ILogger _logger;
        private readonly IParallelProcessingOptions _defaultOptions;
        private readonly IProgressReporter _progressReporter;
        
        /// <summary>
        /// Creates a new instance of BatchProcessor
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="progressReporter">Progress reporter for batch operations</param>
        /// <param name="defaultOptions">Default processing options</param>
        public BatchProcessor(
            ILogger logger,
            IProgressReporter progressReporter = null,
            IParallelProcessingOptions defaultOptions = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _progressReporter = progressReporter;
            _defaultOptions = defaultOptions ?? new ParallelProcessingOptions();
        }
        
        /// <summary>
        /// Processes a collection of items in batches
        /// </summary>
        /// <param name="items">Items to process</param>
        /// <param name="processor">Processing function for each item</param>
        /// <param name="batchSize">Size of each batch</param>
        /// <param name="options">Processing options</param>
        /// <returns>Collection of processed output items</returns>
        public IEnumerable<TOutput> ProcessBatches(
            IEnumerable<TInput> items,
            Func<TInput, TOutput> processor,
            int batchSize = 1000,
            IParallelProcessingOptions options = null)
        {
            options ??= _defaultOptions;
            
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }
            
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }
            
            // Convert to list to get accurate count
            var itemsList = items.ToList();
            int totalItems = itemsList.Count;
            
            _logger.LogInformation(
                $"Starting batch processing of {totalItems} {typeof(TInput).Name} items " +
                $"with batch size {batchSize}");
                
            var stopwatch = Stopwatch.StartNew();
            int processedCount = 0;
            var results = new ConcurrentBag<TOutput>();
            
            // Create batches
            var batches = new List<List<TInput>>();
            for (int i = 0; i < totalItems; i += batchSize)
            {
                batches.Add(itemsList.Skip(i).Take(batchSize).ToList());
            }
            
            _logger.LogDebug($"Created {batches.Count} batches");
            
            // Process batches in parallel
            foreach (var batch in batches)
            {
                try
                {
                    _logger.LogDebug($"Processing batch {batches.IndexOf(batch)} with {batch.Count} items");
                    
                    // Process each item in the batch using PLINQ
                    var batchResults = batch
                        .AsParallel()
                        .WithDegreeOfParallelism(options.MaxDegreeOfParallelism ?? Environment.ProcessorCount)
                        .WithCancellation(options.CancellationTokenSource?.Token ?? CancellationToken.None)
                        .Select(item =>
                        {
                            TOutput result = processor(item);
                            return result;
                        })
                        .ToList();
                            
                        // Add results to the concurrent bag
                        foreach (var result in batchResults)
                        {
                            results.Add(result);
                        }
                        
                        // Update progress
                        int batchProcessedCount = Interlocked.Add(ref processedCount, batch.Count);
                        
                        if (_progressReporter != null)
                        {
                            double progress = (double)batchProcessedCount / totalItems;
                            _progressReporter.ReportProgress(progress, $"Processed {batchProcessedCount} of {totalItems} items");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Batch processing canceled");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing batch");
                    }
                }
                
            stopwatch.Stop();
            _logger.LogInformation(
                $"Completed batch processing in {stopwatch.ElapsedMilliseconds}ms. " +
                $"Processed {processedCount} items with {batches.Count} batches");
                
            return results;
        }
        
        /// <summary>
        /// Processes a collection of items in batches asynchronously
        /// </summary>
        /// <param name="items">Items to process</param>
        /// <param name="processor">Asynchronous processing function for each item</param>
        /// <param name="batchSize">Size of each batch</param>
        /// <param name="options">Processing options</param>
        /// <returns>Collection of processed output items</returns>
        public async Task<IEnumerable<TOutput>> ProcessBatchesAsync(
            IEnumerable<TInput> items,
            Func<TInput, Task<TOutput>> processor,
            int batchSize = 1000,
            IParallelProcessingOptions options = null)
        {
            options ??= _defaultOptions;
            
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }
            
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }
            
            // Convert to list to get accurate count
            var itemsList = items.ToList();
            int totalItems = itemsList.Count;
            
            _logger.LogInformation(
                $"Starting async batch processing of {totalItems} {typeof(TInput).Name} items " +
                $"with batch size {batchSize}");
                
            var stopwatch = Stopwatch.StartNew();
            int processedCount = 0;
            var results = new ConcurrentBag<TOutput>();
            
            // Create batches
            var batches = new List<List<TInput>>();
            for (int i = 0; i < totalItems; i += batchSize)
            {
                batches.Add(itemsList.Skip(i).Take(batchSize).ToList());
            }
            
            _logger.LogDebug($"Created {batches.Count} batches for async processing");
            
            // Create a task for each batch
            var batchTasks = new List<Task>();
            foreach (var batch in batches)
            {
                var batchTask = Task.Run(async () =>
                {
                    try
                    {
                        // Process items in batch concurrently using Task.WhenAll
                        var tasks = batch.Select(processor).ToArray();
                        var batchResults = await Task.WhenAll(tasks);
                        
                        // Add results to the concurrent bag
                        foreach (var result in batchResults)
                        {
                            results.Add(result);
                        }
                        
                        // Update progress
                        int batchProcessedCount = Interlocked.Add(ref processedCount, batch.Count);
                        
                        if (_progressReporter != null)
                        {
                            double progress = (double)batchProcessedCount / totalItems;
                            _progressReporter.ReportProgress(progress, $"Processed {batchProcessedCount} of {totalItems} items");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Async batch processing canceled");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing batch asynchronously");
                    }
                }, options.CancellationTokenSource?.Token ?? CancellationToken.None);
                
                batchTasks.Add(batchTask);
            }
            
            // Wait for all batch tasks to complete
            await Task.WhenAll(batchTasks);
            
            stopwatch.Stop();
            _logger.LogInformation(
                $"Completed async batch processing in {stopwatch.ElapsedMilliseconds}ms. " +
                $"Processed {processedCount} items with {batches.Count} batches");
                
            return results;
        }
        
        /// <summary>
        /// Processes a source collection and transforms it to a target collection with aggregation
        /// </summary>
        /// <typeparam name="TKey">Type of grouping key</typeparam>
        /// <typeparam name="TAggregate">Type of aggregated value</typeparam>
        /// <param name="items">Source items</param>
        /// <param name="keySelector">Function to extract the key for grouping</param>
        /// <param name="aggregator">Function to aggregate values for each key</param>
        /// <param name="resultSelector">Function to create a result from key and aggregate</param>
        /// <param name="options">Processing options</param>
        /// <returns>Collection of aggregated outputs</returns>
        public IEnumerable<TOutput> AggregateByKey<TKey, TAggregate>(
            IEnumerable<TInput> items,
            Func<TInput, TKey> keySelector,
            Func<IEnumerable<TInput>, TAggregate> aggregator,
            Func<TKey, TAggregate, TOutput> resultSelector,
            IParallelProcessingOptions options = null)
        {
            options ??= _defaultOptions;
            
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }
            
            _logger.LogInformation($"Starting aggregation by key for {typeof(TInput).Name} items");
            
            // Create parallel query with options
            var parallelQuery = items.AsParallel();
            
            if (options.MaxDegreeOfParallelism.HasValue && options.MaxDegreeOfParallelism > 0)
            {
                parallelQuery = parallelQuery.WithDegreeOfParallelism(options.MaxDegreeOfParallelism.Value);
            }
            
            if (options.CancellationTokenSource != null)
            {
                parallelQuery = parallelQuery.WithCancellation(options.CancellationTokenSource.Token);
            }
            
            // Group, aggregate, and transform in parallel
            var result = parallelQuery
                .GroupBy(keySelector)
                .Select(group => 
                {
                    var aggregate = aggregator(group);
                    return resultSelector(group.Key, aggregate);
                })
                .ToList();
                
            _logger.LogInformation($"Completed aggregation with {result.Count} result groups");
            
            return result;
        }
    }
}
