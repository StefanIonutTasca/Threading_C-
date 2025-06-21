# Threading Architecture

This document outlines the threading architecture used in the Real-Time Transport Tracker application.

## Overview

The application leverages advanced multi-threading capabilities to ensure responsive UI while handling intensive background operations including API polling, data processing, and caching. The threading model is designed to maximize CPU utilization while preventing thread starvation and deadlocks.

## Key Threading Components

### 1. Parallel Processing Framework

#### BatchProcessor

`BatchProcessor<TInput, TOutput>` provides efficient parallel processing of large datasets using PLINQ (Parallel LINQ).

Key features:
- Configurable batch size and degree of parallelism
- Progress reporting via `IProgress<ProgressReport>`
- Cancellation support
- Error handling with aggregated exceptions
- Thread-safe result aggregation

Usage example:
```csharp
var processor = new BatchProcessor<TravelRoute, RouteStatus>(_logger);

// Process 10,000 routes in batches of 500
var results = processor.ProcessBatches(
    allRoutes, 
    route => _apiClient.GetRouteStatus(route),
    500,
    new ParallelProcessingOptions 
    { 
        MaxDegreeOfParallelism = Environment.ProcessorCount,
        ProgressReporter = new Progress<ProgressReport>(OnProgressUpdated)
    });
```

#### BatchTaskManager

`BatchTaskManager` coordinates and manages long-running background tasks with the following capabilities:
- Concurrent task limiting to prevent thread pool starvation
- Task state tracking and progress reporting
- Cancellation support
- Task result persistence
- Thread synchronization using `SemaphoreSlim`

### 2. Thread-Safe Collections

The application uses specialized thread-safe collections including:
- `ConcurrentDictionary` for cache implementations and shared state
- `ConcurrentQueue` for background work items
- `BlockingCollection` for producer-consumer patterns

### 3. Thread Synchronization Primitives

Various synchronization primitives are used throughout the codebase:
- `ReaderWriterLockSlim` for data that is read frequently but written infrequently
- `SemaphoreSlim` for resource limiting and async-compatible synchronization
- `AsyncLock` implementation for async method synchronization
- `CountdownEvent` for coordinating parallel operations

### 4. Asynchronous Programming Model

The application heavily utilizes the Task-based Asynchronous Pattern (TAP):
- Async/await for non-blocking I/O operations
- Task continuations for workflow coordination
- Task factories for custom task scheduling

### 5. Thread Pool Management

Custom thread pool optimization includes:
- Monitoring thread pool utilization
- Work-stealing queue implementation
- Thread affinity for CPU-intensive operations
- Quality of Service monitoring through `ThreadingMetricsCollector`

### 6. Deadlock Prevention

The `ThreadDeadlockDetector` component monitors thread wait relationships to detect potential deadlocks:
- Resource acquisition tracking
- Wait-for graph analysis
- Timeout-based detection
- Diagnostic logging of thread dependencies

## Best Practices Implemented

1. **Thread Safety**: All shared state is protected by appropriate synchronization
2. **Cancellation**: All long-running operations support cancellation
3. **Progress Reporting**: Background operations provide progress updates
4. **Resource Management**: Threads and connections are properly managed to prevent leaks
5. **Exception Handling**: Unhandled exceptions in background threads are captured and logged

## Performance Considerations

- Thread pool threads are used for most background operations
- CPU-bound operations utilize `Task.Run` with custom scheduling
- I/O-bound operations use `ConfigureAwait(false)` to prevent context switching
- Thread affinity is used for UI updates

## Diagnostics and Monitoring

The application includes comprehensive threading diagnostics:
- `PerformanceMonitoringDashboard` for real-time thread metrics
- `DiagnosticLogger` for detailed thread operation logging
- Thread pool usage statistics and alerts

## Threading Patterns Used

1. **Producer-Consumer**: For processing data streams
2. **Parallel Pipeline**: For multi-stage data processing
3. **Work Stealing**: For balanced CPU utilization
4. **Reader-Writer Lock**: For optimized concurrent access to shared resources
