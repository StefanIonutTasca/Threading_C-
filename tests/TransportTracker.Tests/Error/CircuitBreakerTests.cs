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
    /// Unit tests for the CircuitBreaker class
    /// </summary>
    public class CircuitBreakerTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private CircuitBreaker _circuitBreaker;

        public CircuitBreakerTests()
        {
            _loggerMock = new Mock<ILogger>();
            _circuitBreaker = new CircuitBreaker(_loggerMock.Object, "TestCircuit", 3, 1);
        }

        [Fact]
        public void Execute_WithSuccessfulOperation_CompletesSuccessfully()
        {
            // Arrange
            bool operationExecuted = false;
            Func<bool> operation = () => 
            {
                operationExecuted = true;
                return true;
            };

            // Act
            bool result = _circuitBreaker.Execute(operation);

            // Assert
            Assert.True(result);
            Assert.True(operationExecuted);
            Assert.Equal(CircuitState.Closed, _circuitBreaker.State);
            Assert.Equal(0, _circuitBreaker.FailureCount);
        }

        [Fact]
        public void Execute_WithFailedOperations_OpensCircuit()
        {
            // Arrange
            int executionCount = 0;
            Func<bool> operation = () => 
            {
                executionCount++;
                throw new Exception("Simulated failure");
            };

            // Act & Assert
            // First failure
            Assert.Throws<Exception>(() => _circuitBreaker.Execute(operation));
            Assert.Equal(CircuitState.Closed, _circuitBreaker.State);
            Assert.Equal(1, _circuitBreaker.FailureCount);
            Assert.Equal(1, executionCount);

            // Second failure
            Assert.Throws<Exception>(() => _circuitBreaker.Execute(operation));
            Assert.Equal(CircuitState.Closed, _circuitBreaker.State);
            Assert.Equal(2, _circuitBreaker.FailureCount);
            Assert.Equal(2, executionCount);

            // Third failure - should trip the circuit
            Assert.Throws<Exception>(() => _circuitBreaker.Execute(operation));
            Assert.Equal(CircuitState.Open, _circuitBreaker.State);
            Assert.Equal(3, _circuitBreaker.FailureCount);
            Assert.Equal(3, executionCount);

            // After circuit is open
            Assert.Throws<CircuitBreakerOpenException>(() => _circuitBreaker.Execute(operation));
            Assert.Equal(CircuitState.Open, _circuitBreaker.State);
            Assert.Equal(3, _circuitBreaker.FailureCount);
            Assert.Equal(3, executionCount); // Operation should not execute when circuit is open
        }

        [Fact]
        public async Task ExecuteAsync_WithSuccessfulOperation_CompletesSuccessfully()
        {
            // Arrange
            bool operationExecuted = false;
            Func<Task<bool>> operation = async () => 
            {
                await Task.Delay(10);
                operationExecuted = true;
                return true;
            };

            // Act
            bool result = await _circuitBreaker.ExecuteAsync(operation);

            // Assert
            Assert.True(result);
            Assert.True(operationExecuted);
            Assert.Equal(CircuitState.Closed, _circuitBreaker.State);
            Assert.Equal(0, _circuitBreaker.FailureCount);
        }

        [Fact]
        public async Task ExecuteAsync_WithFailedOperations_OpensCircuit()
        {
            // Arrange
            int executionCount = 0;
            Func<Task<bool>> operation = async () => 
            {
                await Task.Delay(10);
                executionCount++;
                throw new Exception("Simulated async failure");
            };

            // Act & Assert
            // First failure
            await Assert.ThrowsAsync<Exception>(() => _circuitBreaker.ExecuteAsync(operation));
            Assert.Equal(CircuitState.Closed, _circuitBreaker.State);
            Assert.Equal(1, _circuitBreaker.FailureCount);
            Assert.Equal(1, executionCount);

            // Second failure
            await Assert.ThrowsAsync<Exception>(() => _circuitBreaker.ExecuteAsync(operation));
            Assert.Equal(CircuitState.Closed, _circuitBreaker.State);
            Assert.Equal(2, _circuitBreaker.FailureCount);
            Assert.Equal(2, executionCount);

            // Third failure - should trip the circuit
            await Assert.ThrowsAsync<Exception>(() => _circuitBreaker.ExecuteAsync(operation));
            Assert.Equal(CircuitState.Open, _circuitBreaker.State);
            Assert.Equal(3, _circuitBreaker.FailureCount);
            Assert.Equal(3, executionCount);

            // After circuit is open
            await Assert.ThrowsAsync<CircuitBreakerOpenException>(() => _circuitBreaker.ExecuteAsync(operation));
            Assert.Equal(CircuitState.Open, _circuitBreaker.State);
            Assert.Equal(3, _circuitBreaker.FailureCount);
            Assert.Equal(3, executionCount); // Operation should not execute when circuit is open
        }

        [Fact]
        public async Task CircuitTransitionsToHalfOpen_AfterTimeout()
        {
            // Arrange
            _circuitBreaker = new CircuitBreaker(_loggerMock.Object, "TimeoutCircuit", 2, 1);
            
            int executionCount = 0;
            Func<bool> operation = () => 
            {
                executionCount++;
                throw new Exception("Simulated failure");
            };
            
            // Act
            // First two failures to open circuit
            Assert.Throws<Exception>(() => _circuitBreaker.Execute(operation));
            Assert.Throws<Exception>(() => _circuitBreaker.Execute(operation));
            Assert.Equal(CircuitState.Open, _circuitBreaker.State);
            
            // Wait for timeout
            await Task.Delay(1100); // Wait for 1.1 seconds (1 second timeout + buffer)
            
            // Circuit should now be half-open
            Assert.Equal(CircuitState.HalfOpen, _circuitBreaker.State);
            
            // First operation after half-open should execute
            Assert.Throws<Exception>(() => _circuitBreaker.Execute(operation));
            
            // Circuit should go back to Open after failure
            Assert.Equal(CircuitState.Open, _circuitBreaker.State);
            Assert.Equal(3, executionCount);
        }

        [Fact]
        public void HalfOpenCircuit_ResetsAfterSuccess()
        {
            // Arrange
            _circuitBreaker = new CircuitBreaker(_loggerMock.Object, "ResetCircuit", 2, 1);
            
            int failureCount = 0;
            Func<bool> failOperation = () => 
            {
                failureCount++;
                throw new Exception("Simulated failure");
            };
            
            // Force circuit to open
            Assert.Throws<Exception>(() => _circuitBreaker.Execute(failOperation));
            Assert.Throws<Exception>(() => _circuitBreaker.Execute(failOperation));
            Assert.Equal(CircuitState.Open, _circuitBreaker.State);
            
            // Manually transition to half-open
            _circuitBreaker.Reset();
            Assert.Equal(CircuitState.Closed, _circuitBreaker.State);
            
            // Execute successful operation - should close the circuit
            bool successResult = _circuitBreaker.Execute(() => true);
            
            // Assert
            Assert.True(successResult);
            Assert.Equal(CircuitState.Closed, _circuitBreaker.State);
            Assert.Equal(0, _circuitBreaker.FailureCount); // Failures should be reset
        }

        [Fact]
        public void CircuitNotifies_OnStateChange()
        {
            // Arrange
            _circuitBreaker = new CircuitBreaker(_loggerMock.Object, "EventCircuit", 2, 1);
            
            CircuitState oldState = CircuitState.Closed;
            CircuitState newState = CircuitState.Closed;
            int eventCallCount = 0;
            
            _circuitBreaker.StateChanged += (sender, args) =>
            {
                eventCallCount++;
                oldState = args.OldState;
                newState = args.NewState;
            };
            
            Func<bool> failOperation = () => 
            {
                throw new Exception("Simulated failure");
            };
            
            // Act
            // Trip the circuit
            Assert.Throws<Exception>(() => _circuitBreaker.Execute(failOperation));
            Assert.Throws<Exception>(() => _circuitBreaker.Execute(failOperation));
            
            // Assert
            Assert.Equal(1, eventCallCount);
            Assert.Equal(CircuitState.Closed, oldState);
            Assert.Equal(CircuitState.Open, newState);
        }

        [Fact]
        public void Execute_WithFallback_ReturnsFallbackResult()
        {
            // Arrange
            _circuitBreaker = new CircuitBreaker(_loggerMock.Object, "FallbackCircuit", 1, 1);
            
            Func<string> operation = () => 
            {
                throw new Exception("Simulated failure");
            };
            
            Func<Exception, string> fallback = (ex) => "Fallback value";
            
            // Act
            // First call - should execute operation, fail, and return fallback
            string result1 = _circuitBreaker.Execute(operation, fallback);
            
            // Second call - should skip operation (circuit open) and return fallback
            string result2 = _circuitBreaker.Execute(operation, fallback);
            
            // Assert
            Assert.Equal("Fallback value", result1);
            Assert.Equal("Fallback value", result2);
            Assert.Equal(CircuitState.Open, _circuitBreaker.State);
        }
    }
}
