using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Parallel.Processing
{
    /// <summary>
    /// Dynamically optimizes batch size for processing large datasets
    /// based on system performance and processing time
    /// </summary>
    public class BatchSizeOptimizer
    {
        private readonly ILogger _logger;
        private int _currentOptimalBatchSize;
        private readonly int _minBatchSize;
        private readonly int _maxBatchSize;
        private readonly int _optimizationStepSize;
        private readonly Dictionary<int, double> _batchSizePerformanceHistory = new();
        private readonly object _syncLock = new();

        /// <summary>
        /// Gets the current optimal batch size for processing
        /// </summary>
        public int CurrentOptimalBatchSize => _currentOptimalBatchSize;

        /// <summary>
        /// Creates a new instance of BatchSizeOptimizer
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="initialBatchSize">Initial batch size to start with</param>
        /// <param name="minBatchSize">Minimum allowed batch size</param>
        /// <param name="maxBatchSize">Maximum allowed batch size</param>
        /// <param name="optimizationStepSize">Step size for batch size adjustments</param>
        public BatchSizeOptimizer(
            ILogger logger,
            int initialBatchSize = 1000,
            int minBatchSize = 100,
            int maxBatchSize = 10000,
            int optimizationStepSize = 100)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            if (initialBatchSize <= 0) throw new ArgumentException("Initial batch size must be positive", nameof(initialBatchSize));
            if (minBatchSize <= 0) throw new ArgumentException("Minimum batch size must be positive", nameof(minBatchSize));
            if (maxBatchSize <= minBatchSize) throw new ArgumentException("Maximum batch size must be greater than minimum", nameof(maxBatchSize));
            if (optimizationStepSize <= 0) throw new ArgumentException("Optimization step size must be positive", nameof(optimizationStepSize));
            
            _currentOptimalBatchSize = initialBatchSize;
            _minBatchSize = minBatchSize;
            _maxBatchSize = maxBatchSize;
            _optimizationStepSize = optimizationStepSize;
            
            _logger.LogInformation($"BatchSizeOptimizer initialized with batch size {_currentOptimalBatchSize} " +
                                 $"(min: {_minBatchSize}, max: {_maxBatchSize}, step: {_optimizationStepSize})");
        }

        /// <summary>
        /// Records performance metrics for a batch processing operation and adjusts optimal batch size
        /// </summary>
        /// <param name="batchSize">Size of batch that was processed</param>
        /// <param name="processingTime">Time taken to process the batch in milliseconds</param>
        /// <param name="itemCount">Number of items processed</param>
        public void RecordPerformanceMetrics(int batchSize, double processingTime, int itemCount)
        {
            lock (_syncLock)
            {
                // Calculate processing rate (items per millisecond)
                double processingRate = itemCount / Math.Max(1.0, processingTime);
                
                // Add to performance history, overwriting previous entry for this batch size
                _batchSizePerformanceHistory[batchSize] = processingRate;
                
                _logger.LogDebug($"Batch size {batchSize} processed {itemCount} items in {processingTime}ms " +
                               $"at rate {processingRate:F3} items/ms");
                
                // Only optimize if we have enough data points
                if (_batchSizePerformanceHistory.Count >= 3)
                {
                    OptimizeBatchSize();
                }
            }
        }

        /// <summary>
        /// Finds the optimal batch size based on recorded performance metrics
        /// </summary>
        private void OptimizeBatchSize()
        {
            // Find batch size with the best performance
            var bestPerformance = _batchSizePerformanceHistory
                .OrderByDescending(kvp => kvp.Value)
                .First();
            
            int bestBatchSize = bestPerformance.Key;
            double bestRate = bestPerformance.Value;
            
            // If current optimal batch size is not the best, adjust it
            if (bestBatchSize != _currentOptimalBatchSize)
            {
                int newBatchSize = _currentOptimalBatchSize;
                
                // Move toward the best performing batch size
                if (bestBatchSize > _currentOptimalBatchSize)
                {
                    newBatchSize = Math.Min(_maxBatchSize, _currentOptimalBatchSize + _optimizationStepSize);
                }
                else if (bestBatchSize < _currentOptimalBatchSize)
                {
                    newBatchSize = Math.Max(_minBatchSize, _currentOptimalBatchSize - _optimizationStepSize);
                }
                
                if (newBatchSize != _currentOptimalBatchSize)
                {
                    _logger.LogInformation($"Adjusting optimal batch size from {_currentOptimalBatchSize} to {newBatchSize} " +
                                         $"based on performance metrics (best rate: {bestRate:F3} items/ms at batch size {bestBatchSize})");
                    
                    _currentOptimalBatchSize = newBatchSize;
                }
            }
        }

        /// <summary>
        /// Suggests an optimal batch size for a given total item count and thread count
        /// </summary>
        /// <param name="totalItemCount">Total number of items to process</param>
        /// <param name="threadCount">Number of threads available for processing</param>
        /// <returns>Recommended batch size</returns>
        public int SuggestBatchSize(int totalItemCount, int threadCount)
        {
            lock (_syncLock)
            {
                // Use current optimal batch size as starting point
                int suggestedBatchSize = _currentOptimalBatchSize;
                
                // Ensure we don't have too few or too many batches
                int targetBatchCount = threadCount * 4; // Aim for 4x more batches than threads for good load balancing
                int idealBatchSize = Math.Max(1, totalItemCount / targetBatchCount);
                
                // Constrain to reasonable limits
                idealBatchSize = Math.Max(_minBatchSize, Math.Min(_maxBatchSize, idealBatchSize));
                
                // If we have history and the ideal batch size is significantly different, 
                // blend with the optimal batch size
                if (_batchSizePerformanceHistory.Count > 0 && 
                    Math.Abs(idealBatchSize - suggestedBatchSize) > _optimizationStepSize * 2)
                {
                    suggestedBatchSize = (idealBatchSize + suggestedBatchSize) / 2;
                }
                
                _logger.LogDebug($"Suggested batch size for {totalItemCount} items across {threadCount} threads: {suggestedBatchSize}");
                return suggestedBatchSize;
            }
        }

        /// <summary>
        /// Resets the optimizer's performance history
        /// </summary>
        public void Reset()
        {
            lock (_syncLock)
            {
                _batchSizePerformanceHistory.Clear();
                _logger.LogInformation("BatchSizeOptimizer performance history reset");
            }
        }
    }
}
