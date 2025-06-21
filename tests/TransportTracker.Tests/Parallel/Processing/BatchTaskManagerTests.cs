using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TransportTracker.Core.Parallel.Processing;
using Xunit;

namespace TransportTracker.Tests.Parallel.Processing
{
    /// <summary>
    /// Unit tests for BatchTaskManager class
    /// </summary>
    public class BatchTaskManagerTests
    {
        private readonly Mock<ILogger<BatchTaskManager>> _loggerMock;
        private readonly BatchTaskManager _batchTaskManager;

        public BatchTaskManagerTests()
        {
            _loggerMock = new Mock<ILogger<BatchTaskManager>>();
            _batchTaskManager = new BatchTaskManager(_loggerMock.Object);
        }

        [Fact]
        public async Task ScheduleBatchTask_WithValidTask_ExecutesSuccessfully()
        {
            // Arrange
            string taskId = "test-task-1";
            int processedItems = 0;
            
            Func<IProgress<ProgressReport>, CancellationToken, Task<BatchTaskResult>> taskAction = 
                async (progress, token) => 
                {
                    for (int i = 0; i < 100; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        Interlocked.Increment(ref processedItems);
                        
                        // Report progress
                        progress.Report(new ProgressReport
                        {
                            ItemsProcessed = i + 1,
                            TotalItems = 100,
                            PercentComplete = (i + 1) * 100f / 100
                        });
                        
                        await Task.Delay(5, token);
                    }
                    
                    return new BatchTaskResult
                    {
                        Success = true,
                        ItemsProcessed = 100
                    };
                };
                
            // Act
            await _batchTaskManager.ScheduleBatchTask(taskId, "Test task", taskAction);
            
            // Wait for task to complete
            var status = await _batchTaskManager.WaitForTaskCompletion(taskId, TimeSpan.FromSeconds(10));
            
            // Assert
            Assert.Equal(BatchTaskStatus.Completed, status);
            Assert.Equal(100, processedItems);
            
            var result = await _batchTaskManager.GetTaskResult(taskId);
            Assert.True(result.Success);
            Assert.Equal(100, result.ItemsProcessed);
        }

        [Fact]
        public async Task CancelTask_StopsTaskExecution()
        {
            // Arrange
            string taskId = "test-task-cancel";
            int processedItems = 0;
            
            Func<IProgress<ProgressReport>, CancellationToken, Task<BatchTaskResult>> taskAction = 
                async (progress, token) => 
                {
                    for (int i = 0; i < 100; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        Interlocked.Increment(ref processedItems);
                        
                        // Report progress
                        progress.Report(new ProgressReport
                        {
                            ItemsProcessed = i + 1,
                            TotalItems = 100,
                            PercentComplete = (i + 1) * 100f / 100
                        });
                        
                        await Task.Delay(20, token);
                    }
                    
                    return new BatchTaskResult
                    {
                        Success = true,
                        ItemsProcessed = 100
                    };
                };
                
            // Act
            await _batchTaskManager.ScheduleBatchTask(taskId, "Test task for cancellation", taskAction);
            
            // Wait a bit for task to start
            await Task.Delay(100);
            
            // Cancel the task
            bool cancelResult = await _batchTaskManager.CancelTask(taskId);
            
            // Wait for task to complete cancellation
            var status = await _batchTaskManager.WaitForTaskCompletion(taskId, TimeSpan.FromSeconds(5));
            
            // Assert
            Assert.True(cancelResult);
            Assert.Equal(BatchTaskStatus.Canceled, status);
            Assert.True(processedItems < 100); // Should not have processed all items
            
            var taskState = await _batchTaskManager.GetTaskState(taskId);
            Assert.Equal(BatchTaskStatus.Canceled, taskState.Status);
        }

        [Fact]
        public async Task GetAllTasks_ReturnsAllTaskStates()
        {
            // Arrange
            // Schedule multiple tasks
            for (int i = 0; i < 3; i++)
            {
                string taskId = $"multiple-task-{i}";
                await _batchTaskManager.ScheduleBatchTask(
                    taskId,
                    $"Multiple task {i}",
                    async (progress, token) => 
                    {
                        // Simple task that completes quickly
                        await Task.Delay(10, token);
                        return new BatchTaskResult { Success = true, ItemsProcessed = 1 };
                    });
            }
            
            // Act
            // Wait for tasks to complete
            await Task.Delay(100);
            
            var allTasks = await _batchTaskManager.GetAllTasks();
            
            // Assert
            Assert.Equal(3, allTasks.Count);
            Assert.Contains(allTasks, t => t.TaskId == "multiple-task-0");
            Assert.Contains(allTasks, t => t.TaskId == "multiple-task-1");
            Assert.Contains(allTasks, t => t.TaskId == "multiple-task-2");
            
            // All tasks should be completed
            Assert.All(allTasks, task => Assert.Equal(BatchTaskStatus.Completed, task.Status));
        }

        [Fact]
        public async Task ConcurrencyLimit_RestrictsParallelExecution()
        {
            // Arrange
            var taskStartTimes = new Dictionary<string, DateTime>();
            var taskEndTimes = new Dictionary<string, DateTime>();
            
            // Create a batch task manager with concurrency limit of 2
            var limitedManager = new BatchTaskManager(_loggerMock.Object, 2);
            
            // Schedule several long-running tasks
            for (int i = 0; i < 4; i++)
            {
                string taskId = $"concurrent-task-{i}";
                
                await limitedManager.ScheduleBatchTask(
                    taskId,
                    $"Concurrent task {i}",
                    async (progress, token) => 
                    {
                        taskStartTimes[taskId] = DateTime.Now;
                        
                        // Task that runs for about 200ms
                        await Task.Delay(200, token);
                        
                        taskEndTimes[taskId] = DateTime.Now;
                        return new BatchTaskResult { Success = true };
                    });
            }
            
            // Act
            // Wait for all tasks to complete
            await Task.Delay(1000);
            
            // Assert
            // Check that no more than 2 tasks were running in parallel
            // We can do this by analyzing the start/end times
            var task0Start = taskStartTimes["concurrent-task-0"];
            var task0End = taskEndTimes["concurrent-task-0"];
            var task1Start = taskStartTimes["concurrent-task-1"];
            var task1End = taskEndTimes["concurrent-task-1"];
            var task2Start = taskStartTimes["concurrent-task-2"];
            
            // First two tasks should start immediately
            Assert.True(task1Start.Subtract(task0Start).TotalMilliseconds < 100);
            
            // Third task should wait until one of the first two completes
            Assert.True(task2Start >= task0End || task2Start >= task1End);
        }

        [Fact]
        public async Task TaskWithError_ReportsFailure()
        {
            // Arrange
            string taskId = "error-task";
            var expectedException = new InvalidOperationException("Test exception");
            
            Func<IProgress<ProgressReport>, CancellationToken, Task<BatchTaskResult>> taskAction = 
                (progress, token) => 
                {
                    throw expectedException;
                };
                
            // Act
            await _batchTaskManager.ScheduleBatchTask(taskId, "Task with error", taskAction);
            
            // Wait for task to complete
            var status = await _batchTaskManager.WaitForTaskCompletion(taskId, TimeSpan.FromSeconds(5));
            
            // Assert
            Assert.Equal(BatchTaskStatus.Failed, status);
            
            var taskState = await _batchTaskManager.GetTaskState(taskId);
            Assert.Equal(BatchTaskStatus.Failed, taskState.Status);
            Assert.Contains("Test exception", taskState.ErrorMessage);
        }
    }
}
