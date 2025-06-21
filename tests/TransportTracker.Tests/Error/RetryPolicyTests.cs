using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TransportTracker.Core.Error;
using Xunit;

namespace TransportTracker.Tests.Error
{
    /// <summary>
    /// Unit tests for the RetryPolicy class
    /// </summary>
    public class RetryPolicyTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private RetryPolicy _retryPolicy;

        public RetryPolicyTests()
        {
            _loggerMock = new Mock<ILogger>();
            _retryPolicy = new RetryPolicy(
                _loggerMock.Object,
                maxRetries: 3,
                initialDelayMilliseconds: 50,
                backoffMultiplier: 2.0,
                maxDelayMilliseconds: 1000);
        }

        [Fact]
        public void Execute_SuccessfulFirstAttempt_ReturnsResult()
        {
            // Arrange
            int executionCount = 0;
            Func<int> operation = () => 
            {
                executionCount++;
                return 42;
            };

            // Act
            int result = _retryPolicy.Execute(operation, "TestOperation");

            // Assert
            Assert.Equal(42, result);
            Assert.Equal(1, executionCount);
        }

        [Fact]
        public void Execute_FailsOnceAndThenSucceeds_ReturnsResult()
        {
            // Arrange
            int executionCount = 0;
            Func<int> operation = () => 
            {
                executionCount++;
                if (executionCount == 1)
                {
                    throw new TimeoutException("Simulated timeout");
                }
                return 42;
            };

            // Act
            int result = _retryPolicy.Execute(operation, "TestOperation");

            // Assert
            Assert.Equal(42, result);
            Assert.Equal(2, executionCount); // Should have executed twice
        }

        [Fact]
        public void Execute_FailsAllAttempts_ThrowsRetryLimitExceededException()
        {
            // Arrange
            int executionCount = 0;
            Func<string> operation = () => 
            {
                executionCount++;
                throw new InvalidOperationException("Simulated failure");
            };

            // Act & Assert
            var exception = Assert.Throws<RetryLimitExceededException>(() => 
                _retryPolicy.Execute(operation, "TestOperation"));
                
            Assert.Equal(4, executionCount); // Should have executed 4 times (initial + 3 retries)
            Assert.Equal(4, exception.Exceptions.Count); // Should have 4 exceptions recorded
            Assert.IsType<InvalidOperationException>(exception.InnerException);
            Assert.Contains("Failed to execute 'TestOperation'", exception.Message);
        }

        [Fact]
        public void Execute_WithCancellation_ThrowsOperationCanceledException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            int executionCount = 0;
            
            Func<int> operation = () => 
            {
                executionCount++;
                if (executionCount == 1)
                {
                    // Cancel after first attempt
                    cts.Cancel();
                }
                // Try to throw if canceled, otherwise throw a different exception
                cts.Token.ThrowIfCancellationRequested();
                throw new InvalidOperationException("Simulated failure");
            };

            // Act & Assert
            Assert.Throws<OperationCanceledException>(() => 
                _retryPolicy.Execute(operation, "TestOperation", cts.Token));
                
            Assert.Equal(1, executionCount);
        }

        [Fact]
        public void Execute_WithNonRetryableException_ThrowsOriginalException()
        {
            // Arrange
            // Create retry policy with a filter that considers InvalidOperationException non-retryable
            var retryPolicy = new RetryPolicy(
                _loggerMock.Object,
                maxRetries: 3,
                retryableExceptionFilter: ex => !(ex is InvalidOperationException));
            
            int executionCount = 0;
            Func<int> operation = () => 
            {
                executionCount++;
                throw new InvalidOperationException("Non-retryable exception");
            };

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => 
                retryPolicy.Execute(operation, "TestOperation"));
                
            Assert.Equal(1, executionCount); // Should have executed only once
            Assert.Equal("Non-retryable exception", exception.Message);
        }

        [Fact]
        public async Task ExecuteAsync_SuccessfulFirstAttempt_ReturnsResult()
        {
            // Arrange
            int executionCount = 0;
            Func<Task<int>> operation = async () => 
            {
                await Task.Delay(10);
                executionCount++;
                return 42;
            };

            // Act
            int result = await _retryPolicy.ExecuteAsync(operation, "TestAsyncOperation");

            // Assert
            Assert.Equal(42, result);
            Assert.Equal(1, executionCount);
        }

        [Fact]
        public async Task ExecuteAsync_FailsOnceAndThenSucceeds_ReturnsResult()
        {
            // Arrange
            int executionCount = 0;
            Func<Task<int>> operation = async () => 
            {
                await Task.Delay(10);
                executionCount++;
                if (executionCount == 1)
                {
                    throw new TimeoutException("Simulated timeout");
                }
                return 42;
            };

            // Act
            int result = await _retryPolicy.ExecuteAsync(operation, "TestAsyncOperation");

            // Assert
            Assert.Equal(42, result);
            Assert.Equal(2, executionCount); // Should have executed twice
        }

        [Fact]
        public async Task ExecuteAsync_FailsAllAttempts_ThrowsRetryLimitExceededException()
        {
            // Arrange
            int executionCount = 0;
            Func<Task<string>> operation = async () => 
            {
                await Task.Delay(10);
                executionCount++;
                throw new InvalidOperationException("Simulated async failure");
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<RetryLimitExceededException>(async () => 
                await _retryPolicy.ExecuteAsync(operation, "TestAsyncOperation"));
                
            Assert.Equal(4, executionCount); // Should have executed 4 times (initial + 3 retries)
            Assert.Equal(4, exception.Exceptions.Count); // Should have 4 exceptions recorded
            Assert.IsType<InvalidOperationException>(exception.InnerException);
            Assert.Contains("Failed to execute 'TestAsyncOperation'", exception.Message);
        }

        [Fact]
        public async Task ExecuteAsync_WithDelayyCancellation_Cancels()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            int executionCount = 0;
            
            Func<Task<int>> operation = async () => 
            {
                await Task.Delay(10);
                executionCount++;
                throw new InvalidOperationException("Simulated failure");
            };

            // Set a short delay and then cancel during the retry delay
            var task = _retryPolicy.ExecuteAsync(operation, "TestAsyncOperation", cts.Token);
            
            // Wait a bit for the first execution to fail and retry delay to start
            await Task.Delay(75);
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () => 
                await task);
                
            Assert.True(executionCount >= 1); // Should have executed at least once
        }

        [Fact]
        public void Constructor_WithInvalidParameters_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new RetryPolicy(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy(_loggerMock.Object, maxRetries: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy(_loggerMock.Object, maxRetries: -1));
        }
    }
}
