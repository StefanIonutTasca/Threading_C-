using System;
using System.Collections.Generic;

namespace TransportTracker.Core.Error
{
    /// <summary>
    /// Recommended action to take when an error occurs
    /// </summary>
    public enum ErrorAction
    {
        /// <summary>
        /// Ignore the error and continue
        /// </summary>
        Ignore,
        
        /// <summary>
        /// Use a default or fallback value
        /// </summary>
        UseDefault,
        
        /// <summary>
        /// Retry the operation
        /// </summary>
        Retry,
        
        /// <summary>
        /// Rethrow the exception
        /// </summary>
        Rethrow,
        
        /// <summary>
        /// Critical error that should crash the application
        /// </summary>
        Crash
    }

    /// <summary>
    /// Record of an error that occurred
    /// </summary>
    public class ErrorRecord
    {
        /// <summary>
        /// Gets the timestamp when the error occurred
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// Gets the exception that was thrown
        /// </summary>
        public Exception Exception { get; }
        
        /// <summary>
        /// Gets the source of the error (component/module name)
        /// </summary>
        public string Source { get; }
        
        /// <summary>
        /// Gets additional context data about the error
        /// </summary>
        public object ContextData { get; }

        /// <summary>
        /// Creates a new instance of ErrorRecord
        /// </summary>
        /// <param name="timestamp">Timestamp when the error occurred</param>
        /// <param name="exception">Exception that was thrown</param>
        /// <param name="source">Source of the error</param>
        /// <param name="contextData">Additional context data</param>
        public ErrorRecord(DateTime timestamp, Exception exception, string source, object contextData = null)
        {
            Timestamp = timestamp;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            ContextData = contextData;
        }
    }

    /// <summary>
    /// Event arguments for error occurred events
    /// </summary>
    public class ErrorOccurredEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the error record
        /// </summary>
        public ErrorRecord Error { get; }
        
        /// <summary>
        /// Gets the recommended action
        /// </summary>
        public ErrorAction Action { get; }

        /// <summary>
        /// Creates a new instance of ErrorOccurredEventArgs
        /// </summary>
        /// <param name="error">Error record</param>
        /// <param name="action">Recommended action</param>
        public ErrorOccurredEventArgs(ErrorRecord error, ErrorAction action)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
            Action = action;
        }
    }

    /// <summary>
    /// Policy for handling specific types of errors
    /// </summary>
    public class ErrorPolicy
    {
        /// <summary>
        /// Gets the description of the policy
        /// </summary>
        public string Description { get; }
        
        /// <summary>
        /// Gets the default action to take
        /// </summary>
        public ErrorAction DefaultAction { get; }
        
        /// <summary>
        /// Gets the maximum number of retries if the action is Retry
        /// </summary>
        public int MaxRetries { get; }
        
        /// <summary>
        /// Gets the delay between retries
        /// </summary>
        public TimeSpan RetryDelay { get; }
        
        /// <summary>
        /// Gets additional options for the policy
        /// </summary>
        public Dictionary<string, object> Options { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Creates a new instance of ErrorPolicy
        /// </summary>
        /// <param name="description">Description of the policy</param>
        /// <param name="defaultAction">Default action to take</param>
        /// <param name="maxRetries">Maximum number of retries if action is Retry</param>
        /// <param name="retryDelay">Delay between retries</param>
        public ErrorPolicy(
            string description,
            ErrorAction defaultAction,
            int maxRetries = 1,
            TimeSpan? retryDelay = null)
        {
            Description = description ?? throw new ArgumentNullException(nameof(description));
            DefaultAction = defaultAction;
            MaxRetries = maxRetries;
            RetryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
        }

        /// <summary>
        /// Determines the action to take for a specific exception
        /// </summary>
        /// <param name="exception">Exception to handle</param>
        /// <param name="source">Source of the error</param>
        /// <returns>Recommended action</returns>
        public virtual ErrorAction DetermineAction(Exception exception, string source)
        {
            // Default implementation simply returns the default action
            // Derived classes can override this to provide more sophisticated logic
            return DefaultAction;
        }
    }
}
