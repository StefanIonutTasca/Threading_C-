using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TransportTracker.App.Core.Diagnostics;

namespace TransportTracker.App.Core.UI
{
    /// <summary>
    /// Helper class for common UI operations with performance monitoring
    /// </summary>
    public static class UIUtilities
    {
        /// <summary>
        /// Executes an action on the UI thread with performance tracking
        /// </summary>
        /// <param name="action">The action to execute on the UI thread</param>
        /// <param name="operationName">Name of the operation for performance tracking</param>
        public static void ExecuteOnUIThread(Action action, string operationName = null)
        {
            if (action == null)
                return;
                
            var opName = operationName ?? "UI_Operation";
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                using (PerformanceMonitor.Instance.StartOperation(opName))
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        PerformanceMonitor.Instance.RecordFailure(opName, ex);
                        System.Diagnostics.Debug.WriteLine($"UI operation '{opName}' failed: {ex.Message}");
                    }
                }
            });
        }
        
        /// <summary>
        /// Executes an asynchronous task on the UI thread with performance tracking
        /// </summary>
        /// <param name="task">The task to execute on the UI thread</param>
        /// <param name="operationName">Name of the operation for performance tracking</param>
        public static async Task ExecuteOnUIThreadAsync(Func<Task> task, string operationName = null)
        {
            if (task == null)
                return;
                
            var opName = operationName ?? "UI_Async_Operation";
            var tcs = new TaskCompletionSource<bool>();
            
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                using (PerformanceMonitor.Instance.StartOperation(opName))
                {
                    try
                    {
                        await task();
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        PerformanceMonitor.Instance.RecordFailure(opName, ex);
                        System.Diagnostics.Debug.WriteLine($"Async UI operation '{opName}' failed: {ex.Message}");
                        tcs.SetException(ex);
                    }
                }
            });
            
            await tcs.Task;
        }
        
        /// <summary>
        /// Throttles a UI update to prevent too many rapid updates
        /// </summary>
        /// <typeparam name="T">The type of data being passed to the update action</typeparam>
        /// <param name="updateAction">The UI update action to throttle</param>
        /// <param name="intervalMs">Minimum interval between updates in milliseconds</param>
        /// <returns>A throttled action that will not execute more frequently than the specified interval</returns>
        public static Action<T> ThrottleUIUpdate<T>(Action<T> updateAction, int intervalMs = 100)
        {
            var lastUpdateTime = DateTime.MinValue;
            var isPending = false;
            var latestValue = default(T);
            
            return value =>
            {
                latestValue = value;
                
                if (isPending)
                    return;
                    
                var now = DateTime.Now;
                var elapsed = now - lastUpdateTime;
                
                if (elapsed.TotalMilliseconds >= intervalMs)
                {
                    // Execute immediately
                    isPending = true;
                    
                    ExecuteOnUIThread(() =>
                    {
                        updateAction(latestValue);
                        lastUpdateTime = DateTime.Now;
                        isPending = false;
                    });
                }
                else
                {
                    // Schedule for later execution
                    isPending = true;
                    var delayMs = intervalMs - (int)elapsed.TotalMilliseconds;
                    
                    Task.Delay(delayMs).ContinueWith(_ =>
                    {
                        ExecuteOnUIThread(() =>
                        {
                            updateAction(latestValue);
                            lastUpdateTime = DateTime.Now;
                            isPending = false;
                        });
                    });
                }
            };
        }
        
        /// <summary>
        /// Performs a batch update of multiple UI elements to reduce UI thread load
        /// </summary>
        /// <typeparam name="T">The type of items to update</typeparam>
        /// <param name="items">The items to update</param>
        /// <param name="updateAction">The update action to apply to each item</param>
        /// <param name="batchSize">Size of each batch</param>
        /// <param name="delayBetweenBatchesMs">Delay between batches in milliseconds</param>
        /// <param name="operationName">Name of the operation for performance tracking</param>
        public static async Task BatchUIUpdateAsync<T>(IReadOnlyList<T> items, 
            Action<T> updateAction, 
            int batchSize = 50, 
            int delayBetweenBatchesMs = 50,
            string operationName = null)
        {
            if (items == null || items.Count == 0 || updateAction == null)
                return;
                
            var opName = operationName ?? "BatchUpdate_UI";
            var totalItems = items.Count;
            var totalBatches = (int)Math.Ceiling((double)totalItems / batchSize);
            
            using (PerformanceMonitor.Instance.StartOperation(opName))
            {
                for (int i = 0; i < totalItems; i += batchSize)
                {
                    var endIndex = Math.Min(i + batchSize, totalItems);
                    var batchItems = new List<T>();
                    
                    // Collect items for this batch
                    for (int j = i; j < endIndex; j++)
                    {
                        batchItems.Add(items[j]);
                    }
                    
                    // Process this batch on the UI thread
                    await ExecuteOnUIThreadAsync(async () =>
                    {
                        foreach (var item in batchItems)
                        {
                            updateAction(item);
                        }
                        
                        // Small delay to let other UI operations happen
                        await Task.Delay(1);
                    }, $"{opName}_Batch_{i/batchSize + 1}");
                    
                    // Delay between batches if not the last batch
                    if (i + batchSize < totalItems)
                    {
                        await Task.Delay(delayBetweenBatchesMs);
                    }
                }
            }
        }
    }
}
