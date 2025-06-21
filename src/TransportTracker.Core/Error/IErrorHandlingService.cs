using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TransportTracker.Core.Error
{
    /// <summary>
    /// Interface for error handling services
    /// </summary>
    public interface IErrorHandlingService
    {
        /// <summary>
        /// Event triggered when an error occurs
        /// </summary>
        event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;

        /// <summary>
        /// Handles an exception according to registered policies
        /// </summary>
        /// <param name="ex">The exception to handle</param>
        /// <param name="source">Source of the error (component/module name)</param>
        /// <param name="contextData">Additional context data about the error</param>
        /// <returns>Recommended error action</returns>
        ErrorAction HandleException(Exception ex, string source, object contextData = null);

        /// <summary>
        /// Handles an exception asynchronously
        /// </summary>
        /// <param name="ex">The exception to handle</param>
        /// <param name="source">Source of the error</param>
        /// <param name="contextData">Additional context data</param>
        /// <returns>Task with recommended error action</returns>
        Task<ErrorAction> HandleExceptionAsync(Exception ex, string source, object contextData = null);

        /// <summary>
        /// Registers a new error policy
        /// </summary>
        /// <param name="exceptionType">Type of exception to handle</param>
        /// <param name="policy">Policy for handling the exception</param>
        void RegisterPolicy(Type exceptionType, ErrorPolicy policy);

        /// <summary>
        /// Gets recent error records
        /// </summary>
        /// <param name="count">Maximum number of records to return</param>
        /// <returns>Collection of recent errors</returns>
        IEnumerable<ErrorRecord> GetRecentErrors(int count = 10);

        /// <summary>
        /// Executes an operation with error handling
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="operation">Operation to execute</param>
        /// <param name="source">Source of the operation</param>
        /// <param name="fallback">Fallback value if operation fails</param>
        /// <returns>Result of the operation or fallback value</returns>
        T ExecuteWithErrorHandling<T>(Func<T> operation, string source, T fallback = default);

        /// <summary>
        /// Executes an asynchronous operation with error handling
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="operation">Async operation to execute</param>
        /// <param name="source">Source of the operation</param>
        /// <param name="fallback">Fallback value if operation fails</param>
        /// <returns>Task with the result of the operation or fallback value</returns>
        Task<T> ExecuteWithErrorHandlingAsync<T>(Func<Task<T>> operation, string source, T fallback = default);
    }
}
