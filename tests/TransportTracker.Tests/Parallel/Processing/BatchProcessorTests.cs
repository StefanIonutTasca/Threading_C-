using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TransportTracker.Core.Parallel.Processing;
using Xunit;

namespace TransportTracker.Tests.Parallel.Processing
{
    /// <summary>
    /// Unit tests for the BatchProcessor class
    /// </summary>
    public class BatchProcessorTests
    {
        private readonly Mock<ILogger<BatchProcessor<int, int>>> _loggerMock;
        private readonly BatchProcessor<int, int> _processor;

        public BatchProcessorTests()
        {
            _loggerMock = new Mock<ILogger<BatchProcessor<int, int>>>();
            _processor = new BatchProcessor<int, int>(_loggerMock.Object);
        }

        [Fact]
        public void ProcessBatches_WithValidInput_ReturnsProcessedItems()
        {
            // Arrange
            var items = Enumerable.Range(1, 1000).ToList();
            Func<int, int> processor = item => item * 2;

            // Act
            var result = _processor.ProcessBatches(items, processor, 100).ToList();

            // Assert
            Assert.Equal(1000, result.Count);
            Assert.Equal(2, result[0]);
            Assert.Equal(2000, result[999]);
            Assert.All(result, item => Assert.True(item % 2 == 0));
        }

        [Fact]
        public void ProcessBatches_WithEmptyInput_ReturnsEmptyCollection()
        {
            // Arrange
            var items = new List<int>();
            Func<int, int> processor = item => item * 2;

            // Act
            var result = _processor.ProcessBatches(items, processor, 100).ToList();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ProcessBatches_WithCancellation_StopsProcessing()
        {
            // Arrange
            var items = Enumerable.Range(1, 10000).ToList();
            var tokenSource = new CancellationTokenSource();
            var options = new ParallelProcessingOptions
            {
                CancellationToken = tokenSource.Token,
                MaxDegreeOfParallelism = 4
            };

            int processedCount = 0;
            Func<int, int> processor = item => 
            {
                Interlocked.Increment(ref processedCount);
                
                // Cancel after processing a few items
                if (item == 100)
                {
                    tokenSource.Cancel();
                }
                
                // Simulate some work
                Thread.Sleep(1);
                return item * 2;
            };

            // Act & Assert
            Assert.Throws<OperationCanceledException>(() => 
                _processor.ProcessBatches(items, processor, 50, options).ToList());
                
            // Should have processed at most a few hundred items
            // (exact number is non-deterministic due to parallelism)
            Assert.True(processedCount < items.Count);
        }

        [Fact]
        public async Task ProcessBatchesAsync_WithValidInput_ReturnsProcessedItems()
        {
            // Arrange
            var items = Enumerable.Range(1, 1000).ToList();
            Func<int, Task<int>> processor = item => Task.FromResult(item * 2);

            // Act
            var result = await _processor.ProcessBatchesAsync(items, processor, 100).ToListAsync();

            // Assert
            Assert.Equal(1000, result.Count);
            Assert.Equal(2, result[0]);
            Assert.Equal(2000, result[999]);
            Assert.All(result, item => Assert.True(item % 2 == 0));
        }

        [Fact]
        public void ProcessBatchesWithProgress_ReportsProgress()
        {
            // Arrange
            var items = Enumerable.Range(1, 1000).ToList();
            Func<int, int> processor = item => item * 2;
            
            int progressCallCount = 0;
            float lastProgressPercentage = 0;

            var progress = new Progress<ProgressReport>(report => 
            {
                progressCallCount++;
                lastProgressPercentage = report.PercentComplete;
            });

            var options = new ParallelProcessingOptions
            {
                ProgressReporter = progress,
                BatchSize = 100
            };

            // Act
            var result = _processor.ProcessBatches(items, processor, 100, options).ToList();

            // Assert
            Assert.Equal(1000, result.Count);
            Assert.True(progressCallCount > 0);
            Assert.Equal(100f, lastProgressPercentage);
        }

        [Fact]
        public void AggregateByKey_GroupsItemsCorrectly()
        {
            // Arrange
            var items = new List<KeyValuePair<string, int>>
            {
                new("key1", 1),
                new("key1", 2),
                new("key2", 3),
                new("key2", 4),
                new("key3", 5)
            };

            // Act
            var result = _processor.AggregateByKey(
                items, 
                pair => pair.Key, 
                pair => pair.Value, 
                (key, values) => new KeyValuePair<string, int>(key, values.Sum()))
                .ToList();

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Contains(result, item => item.Key == "key1" && item.Value == 3);
            Assert.Contains(result, item => item.Key == "key2" && item.Value == 7);
            Assert.Contains(result, item => item.Key == "key3" && item.Value == 5);
        }
    }
}
