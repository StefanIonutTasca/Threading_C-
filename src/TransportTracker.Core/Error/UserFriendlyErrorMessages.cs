using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace TransportTracker.Core.Error
{
    /// <summary>
    /// Provides user-friendly error messages for application exceptions
    /// </summary>
    public class UserFriendlyErrorMessages
    {
        private readonly ConcurrentDictionary<Type, string> _exceptionMessages = new();
        private readonly ConcurrentDictionary<string, string> _errorCodeMessages = new();
        private readonly string _defaultMessage;
        private readonly string _defaultNetworkMessage;
        private readonly string _defaultSecurityMessage;
        
        private static readonly ThreadLocal<UserFriendlyErrorMessages> _instance = 
            new(() => new UserFriendlyErrorMessages());
            
        /// <summary>
        /// Gets the current instance of UserFriendlyErrorMessages for the current thread
        /// </summary>
        public static UserFriendlyErrorMessages Current => _instance.Value;

        /// <summary>
        /// Creates a new instance of UserFriendlyErrorMessages
        /// </summary>
        public UserFriendlyErrorMessages()
        {
            _defaultMessage = "An unexpected error occurred. Please try again later.";
            _defaultNetworkMessage = "Unable to connect to the server. Please check your internet connection.";
            _defaultSecurityMessage = "You don't have permission to perform this action.";
            
            RegisterDefaultMessages();
        }

        /// <summary>
        /// Gets a user-friendly message for an exception
        /// </summary>
        /// <param name="exception">Exception to get message for</param>
        /// <returns>User-friendly error message</returns>
        public string GetMessageForException(Exception exception)
        {
            if (exception == null)
                return _defaultMessage;
                
            Type exceptionType = exception.GetType();
            
            // Check for specific exception type
            while (exceptionType != null && exceptionType != typeof(object))
            {
                if (_exceptionMessages.TryGetValue(exceptionType, out string message))
                {
                    return FormatMessage(message, exception);
                }
                
                exceptionType = exceptionType.BaseType;
            }
            
            // Check for known patterns in the exception message
            if (IsNetworkRelated(exception))
                return _defaultNetworkMessage;
                
            if (IsSecurityRelated(exception))
                return _defaultSecurityMessage;
            
            // Fall back to default message
            return _defaultMessage;
        }

        /// <summary>
        /// Gets a user-friendly message for an error code
        /// </summary>
        /// <param name="errorCode">Error code to get message for</param>
        /// <returns>User-friendly error message</returns>
        public string GetMessageForErrorCode(string errorCode)
        {
            if (string.IsNullOrEmpty(errorCode))
                return _defaultMessage;
                
            return _errorCodeMessages.TryGetValue(errorCode, out string message)
                ? message
                : $"Error code: {errorCode}";
        }

        /// <summary>
        /// Registers a user-friendly message for an exception type
        /// </summary>
        /// <typeparam name="TException">Type of exception</typeparam>
        /// <param name="message">User-friendly message</param>
        public void RegisterExceptionMessage<TException>(string message) where TException : Exception
        {
            _exceptionMessages[typeof(TException)] = message ?? throw new ArgumentNullException(nameof(message));
        }

        /// <summary>
        /// Registers a user-friendly message for an error code
        /// </summary>
        /// <param name="errorCode">Error code</param>
        /// <param name="message">User-friendly message</param>
        public void RegisterErrorCodeMessage(string errorCode, string message)
        {
            if (string.IsNullOrEmpty(errorCode))
                throw new ArgumentNullException(nameof(errorCode));
                
            _errorCodeMessages[errorCode] = message ?? throw new ArgumentNullException(nameof(message));
        }

        /// <summary>
        /// Gets all registered exception message templates
        /// </summary>
        /// <returns>Dictionary of exception types and messages</returns>
        public IReadOnlyDictionary<Type, string> GetAllExceptionMessages()
        {
            return _exceptionMessages;
        }

        /// <summary>
        /// Gets all registered error code messages
        /// </summary>
        /// <returns>Dictionary of error codes and messages</returns>
        public IReadOnlyDictionary<string, string> GetAllErrorCodeMessages()
        {
            return _errorCodeMessages;
        }

        #region Private Methods
        
        private void RegisterDefaultMessages()
        {
            // Network related exceptions
            RegisterExceptionMessage<System.Net.WebException>(
                "Unable to connect to the server. Please check your internet connection and try again.");
            RegisterExceptionMessage<System.Net.Http.HttpRequestException>(
                "There was a problem communicating with the server. Please try again later.");
            RegisterExceptionMessage<System.Net.Sockets.SocketException>(
                "Network connection error. Please check your internet connection.");
            
            // File related exceptions
            RegisterExceptionMessage<System.IO.FileNotFoundException>(
                "A required file could not be found. Please reinstall the application.");
            RegisterExceptionMessage<System.IO.IOException>(
                "There was a problem accessing a file or resource.");
            
            // Data related exceptions
            RegisterExceptionMessage<System.Data.DataException>(
                "There was a problem processing the data. Please try again.");
            
            // Security related exceptions
            RegisterExceptionMessage<UnauthorizedAccessException>(
                "You don't have permission to perform this action.");
            RegisterExceptionMessage<System.Security.SecurityException>(
                "Security error. You may not have the required permissions.");
                
            // Format exceptions
            RegisterExceptionMessage<FormatException>(
                "Invalid data format detected. Please check your input and try again.");
            RegisterExceptionMessage<ArgumentException>(
                "Invalid input provided. Please check your input and try again.");
                
            // Threading exceptions
            RegisterExceptionMessage<System.Threading.ThreadInterruptedException>(
                "The operation was interrupted. Please try again.");
            RegisterExceptionMessage<System.Threading.Tasks.TaskCanceledException>(
                "The operation was canceled.");
                
            // Operation exceptions
            RegisterExceptionMessage<OperationCanceledException>(
                "The operation was canceled. Please try again if needed.");
            RegisterExceptionMessage<TimeoutException>(
                "The operation timed out. Please try again later.");
            
            // Custom exceptions
            RegisterExceptionMessage<CircuitBreakerOpenException>(
                "The service is temporarily unavailable. Please try again in a few minutes.");
            RegisterExceptionMessage<RetryLimitExceededException>(
                "The operation failed after multiple attempts. Please try again later.");
        }

        private string FormatMessage(string template, Exception exception)
        {
            // Simple formatting for now - could be expanded with more placeholders
            return template.Replace("{Message}", exception.Message);
        }

        private bool IsNetworkRelated(Exception exception)
        {
            string message = exception.Message.ToLower();
            return message.Contains("network") || 
                   message.Contains("connection") || 
                   message.Contains("socket") || 
                   message.Contains("http") || 
                   message.Contains("timeout") || 
                   message.Contains("server");
        }

        private bool IsSecurityRelated(Exception exception)
        {
            string message = exception.Message.ToLower();
            return message.Contains("permission") || 
                   message.Contains("access") || 
                   message.Contains("denied") || 
                   message.Contains("unauthorized") || 
                   message.Contains("forbidden") || 
                   message.Contains("security");
        }
        
        #endregion
    }
}
