using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TransportTracker.Core.Threading.Events;

namespace TransportTracker.Core.Parallel.Processing
{
    /// <summary>
    /// Extension methods for batch processing
    /// </summary>
    public static class BatchProcessingExtensions
    {
        /// <summary>
        /// Registers batch processing services with the dependency injection container
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddBatchProcessing(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            
            // Register batch processing components
            services.AddSingleton(typeof(BatchProcessor<,>));
            
            // Register chunking strategies
            services.AddSingleton(sp => 
            {
                var logger = sp.GetRequiredService<ILogger<DataChunkingStrategies>>();
                return new DataChunkingStrategies(logger);
            });
            
            // Register batch size optimizer
            services.AddSingleton(sp => 
            {
                var logger = sp.GetRequiredService<ILogger<BatchSizeOptimizer>>();
                return new BatchSizeOptimizer(logger);
            });
            
            return services;
        }

        /// <summary>
        /// Processes a collection in batches with progress reporting
        /// </summary>
        /// <typeparam name="T">Type of items to process</typeparam>
        /// <param name="source">Source collection</param>
        /// <param name="action">Action to perform on each item</param>
        /// <param name="batchSize">Size of each batch</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the asynchronous operation</returns>
        public static async Task ProcessInBatchesAsync<T>(
            this IEnumerable<T> source,
            Action<T> action,
            int batchSize = 100,
            IProgress<(int Completed, int Total)> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (batchSize <= 0) throw new ArgumentException("Batch size must be positive", nameof(batchSize));

            var items = source as IList<T> ?? source.ToList();
            int totalItems = items.Count;
            int processedItems = 0;
            
            // Create batches
            var batches = new List<List<T>>();
            for (int i = 0; i < totalItems; i += batchSize)
            {
                batches.Add(items.Skip(i).Take(batchSize).ToList());
            }
            
            // Process each batch
            foreach (var batch in batches)
            {
                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();
                
                // Process the batch on a background thread
                await Task.Run(() => 
                {
                    Parallel.ForEach(batch, item => 
                    {
                        action(item);
                    });
                }, cancellationToken);
                
                // Update progress
                processedItems += batch.Count;
                progress?.Report((processedItems, totalItems));
                
                // Yield to the thread pool
                await Task.Yield();
            }
        }
        
        /// <summary>
        /// Processes a collection in batches with results and progress reporting
        /// </summary>
        /// <typeparam name="TSource">Type of source items</typeparam>
        /// <typeparam name="TResult">Type of result items</typeparam>
        /// <param name="source">Source collection</param>
        /// <param name="selector">Function to transform each item</param>
        /// <param name="batchSize">Size of each batch</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of transformed items</returns>
        public static async Task<IEnumerable<TResult>> SelectInBatchesAsync<TSource, TResult>(
            this IEnumerable<TSource> source,
            Func<TSource, TResult> selector,
            int batchSize = 100,
            IProgress<(int Completed, int Total)> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            if (batchSize <= 0) throw new ArgumentException("Batch size must be positive", nameof(batchSize));

            var items = source as IList<TSource> ?? source.ToList();
            int totalItems = items.Count;
            int processedItems = 0;
            var results = new List<TResult>(totalItems);
            
            // Create batches
            var batches = new List<List<TSource>>();
            for (int i = 0; i < totalItems; i += batchSize)
            {
                batches.Add(items.Skip(i).Take(batchSize).ToList());
            }
            
            // Process each batch
            foreach (var batch in batches)
            {
                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();
                
                // Process the batch on a background thread
                var batchResults = await Task.Run(() => 
                {
                    return batch.AsParallel()
                        .WithCancellation(cancellationToken)
                        .Select(selector)
                        .ToList();
                }, cancellationToken);
                
                // Add results
                results.AddRange(batchResults);
                
                // Update progress
                processedItems += batch.Count;
                progress?.Report((processedItems, totalItems));
                
                // Yield to the thread pool
                await Task.Yield();
            }
            
            return results;
        }
        
        /// <summary>
        /// Converts a progress tracker to an IProgress interface
        /// </summary>
        /// <param name="progressReporter">Progress reporter</param>
        /// <returns>IProgress implementation</returns>
        public static IProgress<(int Completed, int Total)> AsProgress(this IProgressReporter progressReporter)
        {
            if (progressReporter == null) throw new ArgumentNullException(nameof(progressReporter));
            
            return new Progress<(int Completed, int Total)>(progress => 
            {
                double percentage = (double)progress.Completed / Math.Max(1, progress.Total);
                progressReporter.ReportProgress(percentage, $"Processed {progress.Completed} of {progress.Total} items");
            });
        }
    }
}
