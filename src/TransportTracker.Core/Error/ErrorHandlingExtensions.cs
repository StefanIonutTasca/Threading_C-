using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Error
{
    /// <summary>
    /// Extension methods for error handling
    /// </summary>
    public static class ErrorHandlingExtensions
    {
        /// <summary>
        /// Adds error handling services to the service collection
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddErrorHandling(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Register error handling service
            services.AddSingleton<IErrorHandlingService>(sp => 
                new ErrorHandlingService(
                    sp.GetRequiredService<ILogger<ErrorHandlingService>>()));

            // Register circuit breaker factory
            services.AddSingleton<ICircuitBreakerFactory>(sp => 
                new CircuitBreakerFactory(sp.GetRequiredService<ILoggerFactory>()));

            return services;
        }

        /// <summary>
        /// Safely executes an action with error handling
        /// </summary>
        /// <param name="errorHandlingService">Error handling service</param>
        /// <param name="action">Action to execute</param>
        /// <param name="source">Source of the action</param>
        /// <returns>True if action executed successfully, false otherwise</returns>
        public static bool SafeExecute(
            this IErrorHandlingService errorHandlingService,
            Action action,
            string source)
        {
            if (errorHandlingService == null)
                throw new ArgumentNullException(nameof(errorHandlingService));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                ErrorAction errorAction = errorHandlingService.HandleException(ex, source);
                return errorAction == ErrorAction.Ignore || errorAction == ErrorAction.UseDefault;
            }
        }

        /// <summary>
        /// Safely executes an async action with error handling
        /// </summary>
        /// <param name="errorHandlingService">Error handling service</param>
        /// <param name="asyncAction">Async action to execute</param>
        /// <param name="source">Source of the action</param>
        /// <returns>Task with true if action executed successfully, false otherwise</returns>
        public static async Task<bool> SafeExecuteAsync(
            this IErrorHandlingService errorHandlingService,
            Func<Task> asyncAction,
            string source)
        {
            if (errorHandlingService == null)
                throw new ArgumentNullException(nameof(errorHandlingService));
            if (asyncAction == null)
                throw new ArgumentNullException(nameof(asyncAction));

            try
            {
                await asyncAction();
                return true;
            }
            catch (Exception ex)
            {
                ErrorAction errorAction = await errorHandlingService.HandleExceptionAsync(ex, source);
                return errorAction == ErrorAction.Ignore || errorAction == ErrorAction.UseDefault;
            }
        }

        /// <summary>
        /// Retries an operation until successful or timeout
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="operation">Operation to retry</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="retryDelay">Delay between retries in milliseconds</param>
        /// <param name="timeout">Timeout in milliseconds</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the operation</returns>
        /// <exception cref="TimeoutException">Thrown if operation times out</exception>
        /// <exception cref="OperationCanceledException">Thrown if operation is canceled</exception>
        public static async Task<T> RetryUntilSuccessAsync<T>(
            Func<Task<T>> operation,
            int maxRetries = 3,
            int retryDelay = 1000,
            int timeout = 30000,
            CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            // Create cancellation token source for timeout
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            int attempt = 0;
            Exception lastException = null;

            while (attempt <= maxRetries)
            {
                try
                {
                    linkedCts.Token.ThrowIfCancellationRequested();
                    return await operation();
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    throw new TimeoutException($"Operation timed out after {timeout}ms");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempt++;

                    if (attempt > maxRetries)
                        break;

                    await Task.Delay(retryDelay, linkedCts.Token);
                }
            }

            throw new RetryLimitExceededException(
                $"Operation failed after {maxRetries + 1} attempts",
                new System.Collections.Generic.List<Exception> { lastException },
                lastException);
        }
    }

    /// <summary>
    /// Factory for creating circuit breakers
    /// </summary>
    public interface ICircuitBreakerFactory
    {
        /// <summary>
        /// Creates a new circuit breaker
        /// </summary>
        /// <param name="name">Name for the circuit</param>
        /// <param name="failureThreshold">Number of failures before opening the circuit</param>
        /// <param name="resetTimeoutSeconds">Time in seconds after which to try closing the circuit</param>
        /// <returns>Circuit breaker instance</returns>
        CircuitBreaker CreateCircuitBreaker(
            string name,
            int failureThreshold = 3,
            int resetTimeoutSeconds = 60);
    }

    /// <summary>
    /// Implementation of circuit breaker factory
    /// </summary>
    public class CircuitBreakerFactory : ICircuitBreakerFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Creates a new instance of CircuitBreakerFactory
        /// </summary>
        /// <param name="loggerFactory">Logger factory</param>
        public CircuitBreakerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <summary>
        /// Creates a new circuit breaker
        /// </summary>
        /// <param name="name">Name for the circuit</param>
        /// <param name="failureThreshold">Number of failures before opening the circuit</param>
        /// <param name="resetTimeoutSeconds">Time in seconds after which to try closing the circuit</param>
        /// <returns>Circuit breaker instance</returns>
        public CircuitBreaker CreateCircuitBreaker(
            string name,
            int failureThreshold = 3,
            int resetTimeoutSeconds = 60)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            var logger = _loggerFactory.CreateLogger($"{typeof(CircuitBreaker).FullName}.{name}");
            return new CircuitBreaker(logger, name, failureThreshold, resetTimeoutSeconds);
        }
    }
}
