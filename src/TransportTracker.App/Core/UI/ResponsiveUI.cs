using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TransportTracker.App.Core.UI
{
    /// <summary>
    /// Helper class for keeping the UI responsive during heavy operations
    /// </summary>
    public static class ResponsiveUI
    {
        /// <summary>
        /// The default time between yielding back to the UI thread
        /// </summary>
        private static readonly TimeSpan DefaultYieldDelay = TimeSpan.FromMilliseconds(100);
        
        /// <summary>
        /// Runs a long-running operation while periodically yielding to the UI thread
        /// </summary>
        /// <param name="action">The action to run</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <param name="yieldInterval">How frequently to yield to the UI thread</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task RunResponsivelyAsync(
            Action<IProgress<double>, CancellationToken> action, 
            IProgress<double> progress = null, 
            TimeSpan? yieldInterval = null,
            CancellationToken cancellationToken = default)
        {
            var delay = yieldInterval ?? DefaultYieldDelay;
            
            await Task.Run(() =>
            {
                var wrappedProgress = new YieldingProgress<double>(progress, delay);
                action(wrappedProgress, cancellationToken);
            }, cancellationToken);
        }
        
        /// <summary>
        /// Processes a collection while periodically yielding to the UI thread
        /// </summary>
        /// <param name="items">The items to process</param>
        /// <param name="processAction">The action to run for each item</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <param name="yieldInterval">How frequently to yield to the UI thread</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task ProcessCollectionResponsivelyAsync<T>(
            IReadOnlyList<T> items,
            Action<T> processAction,
            IProgress<double> progress = null,
            TimeSpan? yieldInterval = null,
            CancellationToken cancellationToken = default)
        {
            if (items == null || items.Count == 0)
                return;
                
            var delay = yieldInterval ?? DefaultYieldDelay;
            var lastYieldTime = DateTime.Now;
            var totalItems = items.Count;
            
            await Task.Run(() =>
            {
                for (int i = 0; i < totalItems; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Process the current item
                    processAction(items[i]);
                    
                    // Report progress
                    var progressValue = (double)i / totalItems;
                    progress?.Report(progressValue);
                    
                    // Yield to UI thread if needed
                    if (DateTime.Now - lastYieldTime > delay)
                    {
                        Task.Delay(1, cancellationToken).Wait(cancellationToken);
                        lastYieldTime = DateTime.Now;
                    }
                }
                
                // Report completion
                progress?.Report(1.0);
                
            }, cancellationToken);
        }
        
        /// <summary>
        /// Applies an action to batches of items while keeping the UI responsive
        /// </summary>
        /// <param name="items">The items to process</param>
        /// <param name="batchAction">The action to run on each batch</param>
        /// <param name="batchSize">The size of each batch</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <param name="yieldInterval">How frequently to yield to the UI thread</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task ProcessBatchesResponsivelyAsync<T>(
            IReadOnlyList<T> items,
            Action<List<T>> batchAction,
            int batchSize,
            IProgress<double> progress = null,
            TimeSpan? yieldInterval = null,
            CancellationToken cancellationToken = default)
        {
            if (items == null || items.Count == 0)
                return;
                
            var delay = yieldInterval ?? DefaultYieldDelay;
            var lastYieldTime = DateTime.Now;
            var totalItems = items.Count;
            var totalBatches = (int)Math.Ceiling((double)totalItems / batchSize);
            var currentBatch = 0;
            
            await Task.Run(() =>
            {
                for (int i = 0; i < totalItems; i += batchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Create the current batch
                    var batchEndIndex = Math.Min(i + batchSize, totalItems);
                    var batch = new List<T>();
                    
                    for (int j = i; j < batchEndIndex; j++)
                    {
                        batch.Add(items[j]);
                    }
                    
                    // Process the batch
                    batchAction(batch);
                    currentBatch++;
                    
                    // Report progress
                    var progressValue = (double)currentBatch / totalBatches;
                    progress?.Report(progressValue);
                    
                    // Yield to UI thread if needed
                    if (DateTime.Now - lastYieldTime > delay)
                    {
                        Task.Delay(1, cancellationToken).Wait(cancellationToken);
                        lastYieldTime = DateTime.Now;
                    }
                }
                
                // Report completion
                progress?.Report(1.0);
                
            }, cancellationToken);
        }
        
        /// <summary>
        /// Triggers UI updates at controlled intervals to prevent overloading the UI thread
        /// </summary>
        /// <param name="updateAction">The action to execute on the UI thread</param>
        /// <param name="throttleInterval">Minimum time between UI updates</param>
        /// <returns>A throttled action that ensures UI responsiveness</returns>
        public static Action<T> CreateThrottledUIAction<T>(Action<T> updateAction, TimeSpan? throttleInterval = null)
        {
            var interval = throttleInterval ?? TimeSpan.FromMilliseconds(250);
            var lastUpdateTime = DateTime.MinValue;
            var pendingUpdate = false;
            var latestValue = default(T);
            
            return value =>
            {
                latestValue = value;
                
                // If we're already scheduled for an update, just store the latest value
                if (pendingUpdate)
                    return;
                    
                var now = DateTime.Now;
                var timeSinceLastUpdate = now - lastUpdateTime;
                
                if (timeSinceLastUpdate >= interval)
                {
                    // Update immediately
                    pendingUpdate = true;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        updateAction(latestValue);
                        lastUpdateTime = DateTime.Now;
                        pendingUpdate = false;
                    });
                }
                else
                {
                    // Schedule an update after the remaining throttle time
                    pendingUpdate = true;
                    var delay = interval - timeSinceLastUpdate;
                    
                    Task.Delay(delay).ContinueWith(_ =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            updateAction(latestValue);
                            lastUpdateTime = DateTime.Now;
                            pendingUpdate = false;
                        });
                    });
                }
            };
        }
    }
    
    /// <summary>
    /// A progress reporter that yields to the UI thread periodically
    /// </summary>
    internal class YieldingProgress<T> : IProgress<T>
    {
        private readonly IProgress<T> _wrappedProgress;
        private readonly TimeSpan _yieldInterval;
        private DateTime _lastYieldTime = DateTime.Now;
        
        public YieldingProgress(IProgress<T> wrappedProgress, TimeSpan yieldInterval)
        {
            _wrappedProgress = wrappedProgress;
            _yieldInterval = yieldInterval;
        }
        
        public void Report(T value)
        {
            _wrappedProgress?.Report(value);
            
            var now = DateTime.Now;
            if (now - _lastYieldTime > _yieldInterval)
            {
                // Yield to the UI thread
                Task.Delay(1).Wait();
                _lastYieldTime = now;
            }
        }
    }
}
