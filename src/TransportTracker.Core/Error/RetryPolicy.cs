using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Error
{
    /// <summary>
    /// Implements retry logic for handling transient failures
    /// </summary>
    public class RetryPolicy
    {
        private readonly ILogger _logger;
        private readonly int _maxRetries;
        private readonly TimeSpan _initialDelay;
        private readonly double _backoffMultiplier;
        private readonly TimeSpan _maxDelay;
        private readonly Func<Exception, bool> _retryableExceptionFilter;

        /// <summary>
        /// Creates a new instance of RetryPolicy
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="maxRetries">Maximum number of retry attempts</param>
        /// <param name="initialDelayMilliseconds">Initial delay between retries in milliseconds</param>
        /// <param name="backoffMultiplier">Multiplier for exponential backoff</param>
        /// <param name="maxDelayMilliseconds">Maximum delay between retries in milliseconds</param>
        /// <param name="retryableExceptionFilter">Optional filter to determine if an exception is retryable</param>
        public RetryPolicy(
            ILogger logger,
            int maxRetries = 3,
            int initialDelayMilliseconds = 200,
            double backoffMultiplier = 2.0,
            int maxDelayMilliseconds = 10000,
            Func<Exception, bool> retryableExceptionFilter = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxRetries = maxRetries > 0 ? maxRetries : throw new ArgumentOutOfRangeException(nameof(maxRetries));
            _initialDelay = TimeSpan.FromMilliseconds(initialDelayMilliseconds);
            _backoffMultiplier = backoffMultiplier;
            _maxDelay = TimeSpan.FromMilliseconds(maxDelayMilliseconds);
            _retryableExceptionFilter = retryableExceptionFilter ?? (_ => true);
        }

        /// <summary>
        /// Executes an operation with retry logic
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="operation">Operation to execute</param>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the operation</returns>
        /// <exception cref="OperationCanceledException">Thrown if operation is canceled</exception>
        /// <exception cref="RetryLimitExceededException">Thrown if all retries fail</exception>
        public T Execute<T>(
            Func<T> operation,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            
            List<Exception> exceptions = null;
            
            for (int attempt = 0; attempt <= _maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    if (attempt > 0)
                    {
                        // Not the first attempt, log retry
                        _logger.LogInformation(
                            $"Retry {attempt}/{_maxRetries} for operation '{operationName}'");
                    }
                    
                    return operation();
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    // Check if exception is retryable
                    if (!_retryableExceptionFilter(ex))
                    {
                        _logger.LogWarning(ex, 
                            $"Non-retryable exception encountered during operation '{operationName}'");
                        throw;
                    }
                    
                    // Record exception
                    exceptions ??= new List<Exception>(_maxRetries);
                    exceptions.Add(ex);
                    
                    // Last attempt failed
                    if (attempt >= _maxRetries)
                    {
                        _logger.LogError(ex, 
                            $"Operation '{operationName}' failed after {_maxRetries + 1} attempts");
                        throw new RetryLimitExceededException(
                            $"Failed to execute '{operationName}' after {_maxRetries + 1} attempts",
                            exceptions,
                            ex);
                    }
                    
                    // Calculate delay with exponential backoff
                    TimeSpan delay = CalculateDelay(attempt);
                    
                    _logger.LogWarning(ex, 
                        $"Attempt {attempt + 1} of operation '{operationName}' failed. " +
                        $"Retrying in {delay.TotalMilliseconds:F0}ms");
                    
                    try
                    {
                        // Wait before retrying
                        Thread.Sleep(delay);
                    }
                    catch (ThreadInterruptedException)
                    {
                        // Thread was interrupted during delay
                        throw new OperationCanceledException(
                            $"Retry for operation '{operationName}' was interrupted",
                            ex,
                            cancellationToken);
                    }
                }
            }
            
            // Should never reach here due to throw inside the loop
            throw new InvalidOperationException("Unexpected end of retry loop");
        }

        /// <summary>
        /// Executes an asynchronous operation with retry logic
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="operation">Async operation to execute</param>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task with the result of the operation</returns>
        /// <exception cref="OperationCanceledException">Thrown if operation is canceled</exception>
        /// <exception cref="RetryLimitExceededException">Thrown if all retries fail</exception>
        public async Task<T> ExecuteAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            
            List<Exception> exceptions = null;
            
            for (int attempt = 0; attempt <= _maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    if (attempt > 0)
                    {
                        // Not the first attempt, log retry
                        _logger.LogInformation(
                            $"Retry {attempt}/{_maxRetries} for operation '{operationName}'");
                    }
                    
                    return await operation().ConfigureAwait(false);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    // Check if exception is retryable
                    if (!_retryableExceptionFilter(ex))
                    {
                        _logger.LogWarning(ex, 
                            $"Non-retryable exception encountered during operation '{operationName}'");
                        throw;
                    }
                    
                    // Record exception
                    exceptions ??= new List<Exception>(_maxRetries);
                    exceptions.Add(ex);
                    
                    // Last attempt failed
                    if (attempt >= _maxRetries)
                    {
                        _logger.LogError(ex, 
                            $"Operation '{operationName}' failed after {_maxRetries + 1} attempts");
                        throw new RetryLimitExceededException(
                            $"Failed to execute '{operationName}' after {_maxRetries + 1} attempts",
                            exceptions,
                            ex);
                    }
                    
                    // Calculate delay with exponential backoff
                    TimeSpan delay = CalculateDelay(attempt);
                    
                    _logger.LogWarning(ex, 
                        $"Attempt {attempt + 1} of operation '{operationName}' failed. " +
                        $"Retrying in {delay.TotalMilliseconds:F0}ms");
                    
                    try
                    {
                        // Wait before retrying
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        // Delay was canceled
                        throw new OperationCanceledException(
                            $"Retry for operation '{operationName}' was canceled",
                            ex,
                            cancellationToken);
                    }
                }
            }
            
            // Should never reach here due to throw inside the loop
            throw new InvalidOperationException("Unexpected end of retry loop");
        }

        /// <summary>
        /// Calculates the delay for a retry attempt
        /// </summary>
        /// <param name="attempt">Current attempt number (0-based)</param>
        /// <returns>TimeSpan delay before the next attempt</returns>
        private TimeSpan CalculateDelay(int attempt)
        {
            // Calculate exponential backoff
            double delayMs = _initialDelay.TotalMilliseconds * Math.Pow(_backoffMultiplier, attempt);
            
            // Apply jitter (±20%)
            double jitter = 0.8 + (new Random().NextDouble() * 0.4);
            delayMs *= jitter;
            
            // Cap at max delay
            delayMs = Math.Min(delayMs, _maxDelay.TotalMilliseconds);
            
            return TimeSpan.FromMilliseconds(delayMs);
        }
    }

    /// <summary>
    /// Exception thrown when retry limit is exceeded
    /// </summary>
    public class RetryLimitExceededException : Exception
    {
        /// <summary>
        /// Gets the list of exceptions from each attempt
        /// </summary>
        public IReadOnlyList<Exception> Exceptions { get; }

        /// <summary>
        /// Creates a new instance of RetryLimitExceededException
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="exceptions">List of exceptions from each attempt</param>
        /// <param name="innerException">Last inner exception</param>
        public RetryLimitExceededException(
            string message,
            List<Exception> exceptions,
            Exception innerException) 
            : base(message, innerException)
        {
            Exceptions = exceptions?.AsReadOnly() ?? 
                         throw new ArgumentNullException(nameof(exceptions));
        }
    }
}
