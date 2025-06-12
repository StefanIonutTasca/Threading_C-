using System;
using System.Threading;
using System.Threading.Tasks;

namespace TransportTracker.Core.Threading
{
    /// <summary>
    /// Factory interface for creating and managing threads in the application.
    /// Provides an abstraction over thread creation to ensure proper configuration and management.
    /// </summary>
    public interface IThreadFactory
    {
        /// <summary>
        /// Creates a new thread with the specified delegate and parameters
        /// </summary>
        /// <param name="threadStart">The method that executes on the thread</param>
        /// <param name="name">Optional name for the thread (useful for debugging)</param>
        /// <param name="isBackground">Whether the thread is a background thread</param>
        /// <param name="priority">Thread priority</param>
        /// <returns>The created thread instance</returns>
        Thread CreateThread(ThreadStart threadStart, string name = null, bool isBackground = true, ThreadPriority priority = ThreadPriority.Normal);
        
        /// <summary>
        /// Creates a new thread with the specified parameterized delegate
        /// </summary>
        /// <param name="paramThreadStart">The method that executes on the thread</param>
        /// <param name="name">Optional name for the thread (useful for debugging)</param>
        /// <param name="isBackground">Whether the thread is a background thread</param>
        /// <param name="priority">Thread priority</param>
        /// <returns>The created thread instance</returns>
        Thread CreateThread(ParameterizedThreadStart paramThreadStart, string name = null, bool isBackground = true, ThreadPriority priority = ThreadPriority.Normal);
        
        /// <summary>
        /// Creates a task that runs on a dedicated thread rather than the thread pool
        /// </summary>
        /// <param name="action">The action to run</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the operation</returns>
        Task CreateDedicatedTask(Action action, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Creates a task that runs on a dedicated thread rather than the thread pool
        /// </summary>
        /// <typeparam name="TResult">The type of the result</typeparam>
        /// <param name="function">The function to run</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the operation with a result</returns>
        Task<TResult> CreateDedicatedTask<TResult>(Func<TResult> function, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Register a thread for tracking and management within the application
        /// </summary>
        /// <param name="thread">The thread to register</param>
        /// <param name="category">The category/group this thread belongs to</param>
        /// <returns>A unique identifier for the registered thread</returns>
        Guid RegisterThread(Thread thread, string category = null);
        
        /// <summary>
        /// Unregisters a thread from the tracking system
        /// </summary>
        /// <param name="threadId">The unique identifier of the registered thread</param>
        void UnregisterThread(Guid threadId);
    }
}
