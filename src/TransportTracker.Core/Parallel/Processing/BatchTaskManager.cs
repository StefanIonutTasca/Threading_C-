using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TransportTracker.Core.Threading.Events;

namespace TransportTracker.Core.Parallel.Processing
{
    /// <summary>
    /// Manages task-based batch processing operations with monitoring and coordination
    /// </summary>
    public class BatchTaskManager
    {
        private readonly ILogger _logger;
        private readonly IProgressReporter _progressReporter;
        private readonly BatchSizeOptimizer _batchSizeOptimizer;
        private readonly SemaphoreSlim _concurrencyLimiter;
        private readonly ConcurrentDictionary<Guid, BatchProcessingTask> _activeTasks = new();

        /// <summary>
        /// Gets the number of currently active batch processing tasks
        /// </summary>
        public int ActiveTaskCount => _activeTasks.Count;

        /// <summary>
        /// Creates a new instance of BatchTaskManager
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="progressReporter">Progress reporter</param>
        /// <param name="batchSizeOptimizer">Batch size optimizer</param>
        /// <param name="maxConcurrentBatches">Maximum concurrent batch operations</param>
        public BatchTaskManager(
            ILogger logger,
            IProgressReporter progressReporter = null,
            BatchSizeOptimizer batchSizeOptimizer = null,
            int maxConcurrentBatches = 4)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _progressReporter = progressReporter;
            _batchSizeOptimizer = batchSizeOptimizer;
            _concurrencyLimiter = new SemaphoreSlim(maxConcurrentBatches, maxConcurrentBatches);
            
            _logger.LogInformation($"BatchTaskManager initialized with max {maxConcurrentBatches} concurrent batches");
        }

        /// <summary>
        /// Schedules a batch processing task
        /// </summary>
        /// <typeparam name="TInput">Type of input items</typeparam>
        /// <typeparam name="TOutput">Type of output items</typeparam>
        /// <param name="taskName">Name of the task for identification and logging</param>
        /// <param name="items">Items to process</param>
        /// <param name="processor">Processing function</param>
        /// <param name="batchSize">Size of each batch (0 for automatic)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>ID of the scheduled task</returns>
        public Guid ScheduleBatchTask<TInput, TOutput>(
            string taskName,
            IEnumerable<TInput> items,
            Func<IEnumerable<TInput>, CancellationToken, IAsyncEnumerable<TOutput>> processor,
            int batchSize = 0, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(taskName)) throw new ArgumentNullException(nameof(taskName));
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (processor == null) throw new ArgumentNullException(nameof(processor));

            var taskId = Guid.NewGuid();
            var itemsList = items as IList<TInput> ?? items.ToList();
            int totalItems = itemsList.Count;
            
            // Determine batch size if not specified
            if (batchSize <= 0 && _batchSizeOptimizer != null)
            {
                batchSize = _batchSizeOptimizer.SuggestBatchSize(
                    totalItems, 
                    Environment.ProcessorCount);
            }
            else if (batchSize <= 0)
            {
                batchSize = Math.Max(1, totalItems / (Environment.ProcessorCount * 4));
                batchSize = Math.Min(1000, Math.Max(100, batchSize)); // Keep within reasonable limits
            }

            // Create batch processing task
            var batchTask = new BatchProcessingTask(
                taskId,
                taskName,
                async (linkedCts) =>
                {
                    using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken, linkedCts.Token);
                    
                    var token = linkedTokenSource.Token;
                    var stopwatch = Stopwatch.StartNew();
                    int processedItems = 0;
                    var results = new ConcurrentBag<TOutput>();
                    
                    try
                    {
                        // Wait for a slot in the concurrency limiter
                        _logger.LogDebug($"Batch task '{taskName}' ({taskId}) waiting for execution slot");
                        await _concurrencyLimiter.WaitAsync(token);
                        
                        _logger.LogInformation($"Starting batch task '{taskName}' ({taskId}) " +
                                             $"for {totalItems} items with batch size {batchSize}");

                        // Create batches
                        var batches = new List<List<TInput>>();
                        for (int i = 0; i < totalItems; i += batchSize)
                        {
                            batches.Add(itemsList.Skip(i).Take(batchSize).ToList());
                        }

                        // Process batches
                        var tasks = new List<Task>();
                        foreach (var batch in batches)
                        {
                            token.ThrowIfCancellationRequested();
                            
                            var batchTask = Task.Run(async () =>
                            {
                                // Process batch and collect results
                                await foreach (var result in processor(batch, token))
                                {
                                    results.Add(result);
                                    
                                    // Update progress
                                    Interlocked.Increment(ref processedItems);
                                    double progress = (double)processedItems / totalItems;
                                    
                                    _progressReporter?.ReportProgress(
                                        progress, 
                                        $"Task '{taskName}': Processed {processedItems} of {totalItems} items");
                                }
                            }, token);
                            
                            tasks.Add(batchTask);
                        }

                        // Wait for all batch tasks to complete
                        await Task.WhenAll(tasks);
                        
                        stopwatch.Stop();
                        _logger.LogInformation(
                            $"Completed batch task '{taskName}' ({taskId}) in {stopwatch.ElapsedMilliseconds}ms. " +
                            $"Processed {processedItems} items with {batches.Count} batches");
                        
                        return results.ToList();
                    }
                    catch (OperationCanceledException)
                    {
                        stopwatch.Stop();
                        _logger.LogWarning(
                            $"Batch task '{taskName}' ({taskId}) was canceled after {stopwatch.ElapsedMilliseconds}ms. " +
                            $"Processed {processedItems} of {totalItems} items");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        _logger.LogError(
                            ex, 
                            $"Error in batch task '{taskName}' ({taskId}) after {stopwatch.ElapsedMilliseconds}ms. " +
                            $"Processed {processedItems} of {totalItems} items");
                        throw;
                    }
                    finally
                    {
                        // Record performance metrics if available
                        if (_batchSizeOptimizer != null)
                        {
                            _batchSizeOptimizer.RecordPerformanceMetrics(
                                batchSize, 
                                stopwatch.ElapsedMilliseconds,
                                processedItems);
                        }
                        
                        _concurrencyLimiter.Release();
                    }
                });

            // Store and start the task
            _activeTasks[taskId] = batchTask;
            batchTask.Start();
            
            return taskId;
        }

        /// <summary>
        /// Gets the status of a batch processing task
        /// </summary>
        /// <param name="taskId">ID of the task</param>
        /// <returns>Status of the batch task</returns>
        public BatchTaskStatus GetTaskStatus(Guid taskId)
        {
            if (_activeTasks.TryGetValue(taskId, out var task))
            {
                return new BatchTaskStatus(
                    task.Id,
                    task.Name,
                    task.Status,
                    task.Progress,
                    task.ProgressMessage,
                    task.StartTime,
                    task.Error);
            }
            
            return null;
        }

        /// <summary>
        /// Gets the result of a completed batch processing task
        /// </summary>
        /// <typeparam name="TOutput">Type of output items</typeparam>
        /// <param name="taskId">ID of the task</param>
        /// <returns>Task result with processed items</returns>
        public async Task<IEnumerable<TOutput>> GetTaskResultAsync<TOutput>(Guid taskId)
        {
            if (_activeTasks.TryGetValue(taskId, out var task))
            {
                try
                {
                    var result = await task.GetResultAsync<TOutput>();
                    
                    // Remove completed task from active tasks
                    if (task.IsCompleted)
                    {
                        _activeTasks.TryRemove(taskId, out _);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error retrieving result for batch task {taskId}");
                    throw;
                }
            }
            
            throw new KeyNotFoundException($"Batch task {taskId} not found");
        }

        /// <summary>
        /// Cancels a batch processing task
        /// </summary>
        /// <param name="taskId">ID of the task</param>
        /// <returns>True if the task was found and canceled</returns>
        public bool CancelTask(Guid taskId)
        {
            if (_activeTasks.TryGetValue(taskId, out var task))
            {
                _logger.LogInformation($"Canceling batch task '{task.Name}' ({taskId})");
                task.Cancel();
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Gets all active batch processing tasks
        /// </summary>
        /// <returns>Collection of active batch task statuses</returns>
        public IEnumerable<BatchTaskStatus> GetAllTaskStatuses()
        {
            return _activeTasks.Values
                .Select(task => new BatchTaskStatus(
                    task.Id,
                    task.Name,
                    task.Status,
                    task.Progress,
                    task.ProgressMessage,
                    task.StartTime,
                    task.Error))
                .ToList();
        }

        /// <summary>
        /// Cancels all active batch processing tasks
        /// </summary>
        public void CancelAllTasks()
        {
            _logger.LogWarning($"Canceling all {_activeTasks.Count} active batch tasks");
            
            foreach (var task in _activeTasks.Values)
            {
                task.Cancel();
            }
        }

        /// <summary>
        /// Internal class representing a batch processing task with state
        /// </summary>
        private class BatchProcessingTask
        {
            private readonly CancellationTokenSource _cts = new();
            private readonly Task<object> _task;
            private readonly Func<CancellationTokenSource, Task<object>> _taskFactory;
            
            public Guid Id { get; }
            public string Name { get; }
            public DateTime StartTime { get; private set; }
            public BatchTaskState Status { get; private set; } = BatchTaskState.Scheduled;
            public double Progress { get; set; }
            public string ProgressMessage { get; set; }
            public Exception Error { get; private set; }
            
            public bool IsCompleted => 
                Status == BatchTaskState.Completed || 
                Status == BatchTaskState.Faulted || 
                Status == BatchTaskState.Canceled;
            
            public BatchProcessingTask(
                Guid id, 
                string name,
                Func<CancellationTokenSource, Task<object>> taskFactory)
            {
                Id = id;
                Name = name;
                _taskFactory = taskFactory;
                
                // Create task but don't start it yet
                _task = new Task<object>(async () =>
                {
                    Status = BatchTaskState.Running;
                    StartTime = DateTime.Now;
                    
                    try
                    {
                        var result = await _taskFactory(_cts);
                        Status = BatchTaskState.Completed;
                        Progress = 1.0;
                        return result;
                    }
                    catch (OperationCanceledException)
                    {
                        Status = BatchTaskState.Canceled;
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Status = BatchTaskState.Faulted;
                        Error = ex;
                        throw;
                    }
                });
            }
            
            public void Start()
            {
                _task.Start();
            }
            
            public void Cancel()
            {
                if (!IsCompleted)
                {
                    _cts.Cancel();
                    Status = BatchTaskState.Canceled;
                }
            }
            
            public async Task<IEnumerable<T>> GetResultAsync<T>()
            {
                return (IEnumerable<T>)await _task;
            }
        }
    }

    /// <summary>
    /// Status of a batch processing task
    /// </summary>
    public class BatchTaskStatus
    {
        /// <summary>
        /// Gets the task ID
        /// </summary>
        public Guid Id { get; }
        
        /// <summary>
        /// Gets the task name
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// Gets the current task state
        /// </summary>
        public BatchTaskState State { get; }
        
        /// <summary>
        /// Gets the progress (0.0 to 1.0)
        /// </summary>
        public double Progress { get; }
        
        /// <summary>
        /// Gets the progress message
        /// </summary>
        public string ProgressMessage { get; }
        
        /// <summary>
        /// Gets the start time
        /// </summary>
        public DateTime StartTime { get; }
        
        /// <summary>
        /// Gets the error if faulted
        /// </summary>
        public Exception Error { get; }
        
        /// <summary>
        /// Creates a new BatchTaskStatus instance
        /// </summary>
        public BatchTaskStatus(
            Guid id, 
            string name, 
            BatchTaskState state, 
            double progress, 
            string progressMessage, 
            DateTime startTime, 
            Exception error = null)
        {
            Id = id;
            Name = name;
            State = state;
            Progress = progress;
            ProgressMessage = progressMessage;
            StartTime = startTime;
            Error = error;
        }
    }

    /// <summary>
    /// State of a batch processing task
    /// </summary>
    public enum BatchTaskState
    {
        /// <summary>
        /// Task is scheduled but not started
        /// </summary>
        Scheduled,
        
        /// <summary>
        /// Task is running
        /// </summary>
        Running,
        
        /// <summary>
        /// Task has completed successfully
        /// </summary>
        Completed,
        
        /// <summary>
        /// Task was canceled
        /// </summary>
        Canceled,
        
        /// <summary>
        /// Task failed with an error
        /// </summary>
        Faulted
    }
}
