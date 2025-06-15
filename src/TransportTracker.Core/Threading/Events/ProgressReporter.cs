using System;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Threading.Events
{
    /// <summary>
    /// Standard implementation of progress reporting for long-running operations
    /// </summary>
    public class ProgressReporter : IProgressReporter
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        
        /// <summary>
        /// Event raised when progress is reported
        /// </summary>
        public event EventHandler<ProgressEventArgs> ProgressChanged;
        
        /// <summary>
        /// Creates a new instance of ProgressReporter
        /// </summary>
        /// <param name="logger">Logger for diagnostic information</param>
        /// <param name="operationName">Name of the operation being tracked</param>
        public ProgressReporter(ILogger logger, string operationName = "Operation")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationName = operationName ?? "Operation";
        }
        
        /// <summary>
        /// Reports the progress of an operation
        /// </summary>
        /// <param name="percentComplete">The percentage complete (0.0 to 1.0)</param>
        /// <param name="message">Optional message describing the current operation state</param>
        public void ReportProgress(double percentComplete, string message = null)
        {
            // Clamp the percentage to valid range
            percentComplete = Math.Clamp(percentComplete, 0.0, 1.0);
            
            // Log the progress
            _logger.LogDebug(
                "{OperationName}: {PercentComplete:P0} complete. {Message}",
                _operationName,
                percentComplete,
                message ?? string.Empty);
                
            // Raise the event
            OnProgressChanged(new ProgressEventArgs(percentComplete, message));
        }
        
        /// <summary>
        /// Raises the ProgressChanged event
        /// </summary>
        /// <param name="e">Event arguments</param>
        protected virtual void OnProgressChanged(ProgressEventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }
    }
}
