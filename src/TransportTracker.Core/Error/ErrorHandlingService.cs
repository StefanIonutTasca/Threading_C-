using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Error
{
    /// <summary>
    /// Centralized error handling service for managing and responding to application errors
    /// </summary>
    public class ErrorHandlingService : IErrorHandlingService
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, ErrorPolicy> _errorPolicies = new();
        private readonly ConcurrentQueue<ErrorRecord> _recentErrors = new();
        private readonly int _maxErrorHistorySize;

        /// <summary>
        /// Event triggered when a new error occurs
        /// </summary>
        public event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;

        /// <summary>
        /// Creates a new instance of ErrorHandlingService
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="maxErrorHistorySize">Maximum number of errors to keep in history</param>
        public ErrorHandlingService(ILogger<ErrorHandlingService> logger, int maxErrorHistorySize = 100)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxErrorHistorySize = maxErrorHistorySize;
            
            // Register default policies
            RegisterDefaultPolicies();
        }

        /// <summary>
        /// Handles an exception according to registered policies
        /// </summary>
        /// <param name="ex">The exception to handle</param>
        /// <param name="source">Source of the error (component/module name)</param>
        /// <param name="contextData">Additional context data about the error</param>
        /// <returns>Recommended error action</returns>
        public ErrorAction HandleException(Exception ex, string source, object contextData = null)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            
            // Create error record
            var errorRecord = new ErrorRecord(
                DateTime.Now,
                ex,
                source,
                contextData);
            
            // Add to error history
            AddToErrorHistory(errorRecord);
            
            // Find applicable policy
            var policy = GetApplicablePolicy(ex);
            var action = policy?.DetermineAction(ex, source) ?? ErrorAction.Rethrow;
            
            // Log error with appropriate level based on action
            LogError(errorRecord, action, policy);
            
            // Notify subscribers
            NotifyErrorOccurred(errorRecord, action);
            
            return action;
        }

        /// <summary>
        /// Handles an exception asynchronously
        /// </summary>
        /// <param name="ex">The exception to handle</param>
        /// <param name="source">Source of the error</param>
        /// <param name="contextData">Additional context data</param>
        /// <returns>Task with recommended error action</returns>
        public async Task<ErrorAction> HandleExceptionAsync(Exception ex, string source, object contextData = null)
        {
            return await Task.Run(() => HandleException(ex, source, contextData));
        }

        /// <summary>
        /// Registers a new error policy
        /// </summary>
        /// <param name="exceptionType">Type of exception to handle</param>
        /// <param name="policy">Policy for handling the exception</param>
        public void RegisterPolicy(Type exceptionType, ErrorPolicy policy)
        {
            if (exceptionType == null) throw new ArgumentNullException(nameof(exceptionType));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            
            string key = exceptionType.FullName;
            _errorPolicies[key] = policy;
            
            _logger.LogDebug($"Registered error policy for {key}: {policy.Description}");
        }

        /// <summary>
        /// Gets recent error records
        /// </summary>
        /// <param name="count">Maximum number of records to return</param>
        /// <returns>Collection of recent errors</returns>
        public IEnumerable<ErrorRecord> GetRecentErrors(int count = 10)
        {
            var result = new List<ErrorRecord>();
            var tempList = new List<ErrorRecord>(_recentErrors);
            
            // Return most recent errors first
            tempList.Reverse();
            
            int resultCount = Math.Min(count, tempList.Count);
            for (int i = 0; i < resultCount; i++)
            {
                result.Add(tempList[i]);
            }
            
            return result;
        }

        /// <summary>
        /// Executes an operation with error handling
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="operation">Operation to execute</param>
        /// <param name="source">Source of the operation</param>
        /// <param name="fallback">Fallback value if operation fails</param>
        /// <returns>Result of the operation or fallback value</returns>
        public T ExecuteWithErrorHandling<T>(Func<T> operation, string source, T fallback = default)
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                var action = HandleException(ex, source);
                
                if (action == ErrorAction.UseDefault)
                {
                    return fallback;
                }
                
                if (action == ErrorAction.Retry)
                {
                    try
                    {
                        return operation();
                    }
                    catch (Exception retryEx)
                    {
                        HandleException(retryEx, $"{source} (retry)", new { OriginalError = ex.Message });
                        return fallback;
                    }
                }
                
                if (action == ErrorAction.Rethrow)
                {
                    throw;
                }
                
                return fallback;
            }
        }

        /// <summary>
        /// Executes an asynchronous operation with error handling
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="operation">Async operation to execute</param>
        /// <param name="source">Source of the operation</param>
        /// <param name="fallback">Fallback value if operation fails</param>
        /// <returns>Task with the result of the operation or fallback value</returns>
        public async Task<T> ExecuteWithErrorHandlingAsync<T>(
            Func<Task<T>> operation, 
            string source, 
            T fallback = default)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                var action = HandleException(ex, source);
                
                if (action == ErrorAction.UseDefault)
                {
                    return fallback;
                }
                
                if (action == ErrorAction.Retry)
                {
                    try
                    {
                        return await operation();
                    }
                    catch (Exception retryEx)
                    {
                        HandleException(retryEx, $"{source} (retry)", new { OriginalError = ex.Message });
                        return fallback;
                    }
                }
                
                if (action == ErrorAction.Rethrow)
                {
                    throw;
                }
                
                return fallback;
            }
        }

        #region Private Methods
        
        /// <summary>
        /// Adds an error record to the history, maintaining maximum size
        /// </summary>
        private void AddToErrorHistory(ErrorRecord error)
        {
            _recentErrors.Enqueue(error);
            
            // Trim history if needed
            while (_recentErrors.Count > _maxErrorHistorySize && _recentErrors.TryDequeue(out _))
            {
                // Just dequeuing to maintain size limit
            }
        }

        /// <summary>
        /// Gets the most applicable policy for an exception
        /// </summary>
        private ErrorPolicy GetApplicablePolicy(Exception ex)
        {
            Type currentType = ex.GetType();
            
            while (currentType != null && currentType != typeof(object))
            {
                if (_errorPolicies.TryGetValue(currentType.FullName, out var policy))
                {
                    return policy;
                }
                
                currentType = currentType.BaseType;
            }
            
            // No specific policy found, use default
            return _errorPolicies.TryGetValue(typeof(Exception).FullName, out var defaultPolicy)
                ? defaultPolicy
                : null;
        }

        /// <summary>
        /// Logs an error with the appropriate level
        /// </summary>
        private void LogError(ErrorRecord error, ErrorAction action, ErrorPolicy policy)
        {
            var logLevel = action switch
            {
                ErrorAction.Ignore => LogLevel.Debug,
                ErrorAction.UseDefault => LogLevel.Warning,
                ErrorAction.Retry => LogLevel.Warning,
                ErrorAction.Rethrow => LogLevel.Error,
                ErrorAction.Crash => LogLevel.Critical,
                _ => LogLevel.Error
            };
            
            _logger.Log(
                logLevel,
                error.Exception,
                $"Error in {error.Source}: {error.Exception.Message}. " +
                $"Action: {action}, Policy: {policy?.Description ?? "None"}");
        }

        /// <summary>
        /// Notifies subscribers of a new error
        /// </summary>
        private void NotifyErrorOccurred(ErrorRecord error, ErrorAction action)
        {
            ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(error, action));
        }

        /// <summary>
        /// Registers default policies for common exception types
        /// </summary>
        private void RegisterDefaultPolicies()
        {
            // Default catch-all policy
            RegisterPolicy(
                typeof(Exception),
                new ErrorPolicy(
                    "Default fallback policy",
                    ErrorAction.Rethrow));
                
            // Transient exceptions - retry
            RegisterPolicy(
                typeof(System.Net.Http.HttpRequestException),
                new ErrorPolicy(
                    "HTTP request retry policy",
                    ErrorAction.Retry,
                    maxRetries: 3,
                    retryDelay: TimeSpan.FromSeconds(2)));
                
            // Argument exceptions - crash application in debug, otherwise log and rethrow
            RegisterPolicy(
                typeof(ArgumentException),
                new ErrorPolicy(
                    "Arguments validation policy",
                    ErrorAction.Rethrow));
                
            // Threading exceptions - log and use fallbacks
            RegisterPolicy(
                typeof(System.Threading.ThreadInterruptedException),
                new ErrorPolicy(
                    "Thread interruption policy",
                    ErrorAction.UseDefault));
                
            // Out of memory - crash with diagnostics
            RegisterPolicy(
                typeof(OutOfMemoryException),
                new ErrorPolicy(
                    "Critical memory error policy",
                    ErrorAction.Crash));
        }
        
        #endregion
    }
}
