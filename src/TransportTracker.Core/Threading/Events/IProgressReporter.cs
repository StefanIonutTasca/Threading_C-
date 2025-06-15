using System;

namespace TransportTracker.Core.Threading.Events
{
    /// <summary>
    /// Interface for reporting progress of long-running operations
    /// </summary>
    public interface IProgressReporter
    {
        /// <summary>
        /// Reports the progress of an operation
        /// </summary>
        /// <param name="percentComplete">The percentage complete (0.0 to 1.0)</param>
        /// <param name="message">Optional message describing the current operation state</param>
        void ReportProgress(double percentComplete, string message = null);
        
        /// <summary>
        /// Event raised when progress is reported
        /// </summary>
        event EventHandler<ProgressEventArgs> ProgressChanged;
    }
    
    /// <summary>
    /// Event arguments for progress reporting
    /// </summary>
    public class ProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the percentage complete (0.0 to 1.0)
        /// </summary>
        public double PercentComplete { get; }
        
        /// <summary>
        /// Gets the optional message describing the current operation state
        /// </summary>
        public string Message { get; }
        
        /// <summary>
        /// Creates a new instance of ProgressEventArgs
        /// </summary>
        /// <param name="percentComplete">The percentage complete (0.0 to 1.0)</param>
        /// <param name="message">Optional message describing the current operation state</param>
        public ProgressEventArgs(double percentComplete, string message = null)
        {
            PercentComplete = Math.Clamp(percentComplete, 0.0, 1.0);
            Message = message;
        }
    }
}
