using System;
using System.Threading;
using System.Threading.Tasks;

namespace TransportTracker.Core.Services.Background
{
    /// <summary>
    /// Interface defining a background polling service that periodically fetches data
    /// from an external source and notifies subscribers of updates.
    /// </summary>
    public interface IBackgroundPollingService : IDisposable
    {
        /// <summary>
        /// Gets whether the service is currently running
        /// </summary>
        bool IsRunning { get; }
        
        /// <summary>
        /// Gets or sets the polling interval in milliseconds
        /// </summary>
        int PollingIntervalMs { get; set; }
        
        /// <summary>
        /// Gets or sets the minimum polling interval in milliseconds
        /// </summary>
        int MinPollingIntervalMs { get; set; }
        
        /// <summary>
        /// Gets or sets the maximum polling interval in milliseconds
        /// </summary>
        int MaxPollingIntervalMs { get; set; }
        
        /// <summary>
        /// Gets or sets whether adaptive polling is enabled
        /// </summary>
        bool AdaptivePollingEnabled { get; set; }
        
        /// <summary>
        /// Starts the background polling service
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to stop the service</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task StartAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Stops the background polling service
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task StopAsync();
        
        /// <summary>
        /// Manually triggers a poll operation outside of the regular polling interval
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task TriggerPollNowAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Event raised when new data is available
        /// </summary>
        event EventHandler<PollingEventArgs> DataAvailable;
        
        /// <summary>
        /// Event raised when polling fails
        /// </summary>
        event EventHandler<PollingErrorEventArgs> PollingError;
        
        /// <summary>
        /// Event raised when polling state changes (starting/stopping)
        /// </summary>
        event EventHandler<PollingStateChangedEventArgs> StateChanged;
    }
    
    /// <summary>
    /// Event arguments for the DataAvailable event
    /// </summary>
    public class PollingEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the timestamp when the data was polled
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// Gets the elapsed time taken to poll the data
        /// </summary>
        public TimeSpan ElapsedTime { get; }
        
        /// <summary>
        /// Gets the data size in bytes
        /// </summary>
        public long DataSizeBytes { get; }
        
        /// <summary>
        /// Gets whether the data has changed since the last poll
        /// </summary>
        public bool HasChanged { get; }
        
        /// <summary>
        /// Creates a new instance of the PollingEventArgs class
        /// </summary>
        /// <param name="timestamp">The timestamp when the data was polled</param>
        /// <param name="elapsedTime">The elapsed time taken to poll the data</param>
        /// <param name="dataSizeBytes">The data size in bytes</param>
        /// <param name="hasChanged">Whether the data has changed since the last poll</param>
        public PollingEventArgs(DateTime timestamp, TimeSpan elapsedTime, long dataSizeBytes, bool hasChanged)
        {
            Timestamp = timestamp;
            ElapsedTime = elapsedTime;
            DataSizeBytes = dataSizeBytes;
            HasChanged = hasChanged;
        }
    }
    
    /// <summary>
    /// Event arguments for the PollingError event
    /// </summary>
    public class PollingErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the exception that occurred during polling
        /// </summary>
        public Exception Exception { get; }
        
        /// <summary>
        /// Gets the timestamp when the error occurred
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// Gets the number of consecutive errors
        /// </summary>
        public int ConsecutiveErrorCount { get; }
        
        /// <summary>
        /// Gets whether the service will attempt to retry
        /// </summary>
        public bool WillRetry { get; }
        
        /// <summary>
        /// Creates a new instance of the PollingErrorEventArgs class
        /// </summary>
        /// <param name="exception">The exception that occurred during polling</param>
        /// <param name="timestamp">The timestamp when the error occurred</param>
        /// <param name="consecutiveErrorCount">The number of consecutive errors</param>
        /// <param name="willRetry">Whether the service will attempt to retry</param>
        public PollingErrorEventArgs(Exception exception, DateTime timestamp, int consecutiveErrorCount, bool willRetry)
        {
            Exception = exception;
            Timestamp = timestamp;
            ConsecutiveErrorCount = consecutiveErrorCount;
            WillRetry = willRetry;
        }
    }
    
    /// <summary>
    /// Event arguments for the StateChanged event
    /// </summary>
    public class PollingStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the previous state of the polling service
        /// </summary>
        public bool PreviousRunningState { get; }
        
        /// <summary>
        /// Gets the new state of the polling service
        /// </summary>
        public bool NewRunningState { get; }
        
        /// <summary>
        /// Gets the timestamp when the state changed
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// Gets the reason for the state change
        /// </summary>
        public string Reason { get; }
        
        /// <summary>
        /// Creates a new instance of the PollingStateChangedEventArgs class
        /// </summary>
        /// <param name="previousRunningState">The previous state of the polling service</param>
        /// <param name="newRunningState">The new state of the polling service</param>
        /// <param name="timestamp">The timestamp when the state changed</param>
        /// <param name="reason">The reason for the state change</param>
        public PollingStateChangedEventArgs(bool previousRunningState, bool newRunningState, DateTime timestamp, string reason)
        {
            PreviousRunningState = previousRunningState;
            NewRunningState = newRunningState;
            Timestamp = timestamp;
            Reason = reason;
        }
    }
}
