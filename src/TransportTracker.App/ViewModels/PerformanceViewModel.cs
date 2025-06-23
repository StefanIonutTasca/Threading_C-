using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using TransportTracker.App.Core.Diagnostics;
using TransportTracker.App.Core.MVVM;

namespace TransportTracker.App.ViewModels
{
    /// <summary>
    /// ViewModel for the performance monitoring page
    /// </summary>
    using TransportTracker.App.Core.MVVM;

public class PerformanceViewModel : BaseViewModel
    {
        private readonly PerformanceMonitor _performanceMonitor;
        private string _selectedCategory = "All";
        private int _activeThreadCount;
        private int _totalOperationCount;
        private string _uptime = "00:00:00";
        private bool _isRefreshing;
        
        /// <summary>
        /// Creates a new instance of the performance view model
        /// </summary>
        public PerformanceViewModel()
        {
            // Get performance monitor instance
            _performanceMonitor = PerformanceMonitor.Instance;
            
            // Initialize collections
            Metrics = new ObservableCollection<MetricViewModel>();
            Threads = new ObservableCollection<ThreadViewModel>();
            Categories = new ObservableCollection<string>(new[] 
            { 
                "All", 
                "NetworkIO", 
                "DataOperations", 
                "MapOperations", 
                "UserInterface", 
                "Other" 
            });
            
            // Initialize commands
            RefreshCommand = new Command(async () => await RefreshDataAsync());
            ResetMetricsCommand = new Command(ResetMetrics);
            
            // Subscribe to metrics updates
            _performanceMonitor.MetricsUpdated += OnMetricsUpdated;
            
            // Initial load
            _ = RefreshDataAsync();
            
            // Start periodic refresh in background
            StartPeriodicRefresh();
        }
        
        /// <summary>
        /// Command to refresh the data
        /// </summary>
        public ICommand RefreshCommand { get; }
        
        /// <summary>
        /// Command to reset all metrics
        /// </summary>
        public ICommand ResetMetricsCommand { get; }
        
        /// <summary>
        /// Collection of performance metrics
        /// </summary>
        public ObservableCollection<MetricViewModel> Metrics { get; }
        
        /// <summary>
        /// Collection of active threads
        /// </summary>
        public ObservableCollection<ThreadViewModel> Threads { get; }
        
        /// <summary>
        /// Available metric categories
        /// </summary>
        public ObservableCollection<string> Categories { get; }
        
        /// <summary>
        /// Currently selected category filter
        /// </summary>
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    _ = RefreshDataAsync();
                }
            }
        }
        
        /// <summary>
        /// Number of active threads
        /// </summary>
        public int ActiveThreadCount
        {
            get => _activeThreadCount;
            set => SetProperty(ref _activeThreadCount, value);
        }
        
        /// <summary>
        /// Total number of operations being tracked
        /// </summary>
        public int TotalOperationCount
        {
            get => _totalOperationCount;
            set => SetProperty(ref _totalOperationCount, value);
        }
        
        /// <summary>
        /// Application uptime
        /// </summary>
        public string Uptime
        {
            get => _uptime;
            set => SetProperty(ref _uptime, value);
        }
        
        /// <summary>
        /// Whether the view is currently refreshing
        /// </summary>
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }
        
        /// <summary>
        /// Refreshes the displayed data
        /// </summary>
        private async Task RefreshDataAsync()
        {
            if (IsRefreshing)
                return;
                
            try
            {
                IsRefreshing = true;
                
                // Offload data fetching to background thread
                await Task.Run(() =>
                {
                    // Update summary stats
                    ActiveThreadCount = _performanceMonitor.ThreadCount;
                    TotalOperationCount = _performanceMonitor.OperationCount;
                    Uptime = _performanceMonitor.Uptime.ToString(@"hh\:mm\:ss");
                    
                    // Update metrics based on selected category
                    IEnumerable<PerformanceMetric> metrics;
                    if (SelectedCategory == "All")
                    {
                        metrics = _performanceMonitor.Metrics;
                    }
                    else
                    {
                        Enum.TryParse<MetricCategory>(SelectedCategory, out var category);
                        metrics = _performanceMonitor.GetMetricsByCategory(category);
                    }
                    
                    // Convert to view models
                    var metricViewModels = metrics
                        .OrderByDescending(m => m.LastExecutionTime)
                        .Select(m => new MetricViewModel(m))
                        .ToList();
                    
                    // Update threads
                    var threadViewModels = _performanceMonitor.ThreadDetails
                        .Where(t => t.Status != ThreadStatus.Completed)
                        .Select(t => new ThreadViewModel(t))
                        .ToList();
                    
                    // UI updates must happen on UI thread
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        // Update metrics
                        Metrics.Clear();
                        foreach (var metric in metricViewModels)
                        {
                            Metrics.Add(metric);
                        }
                        
                        // Update threads
                        Threads.Clear();
                        foreach (var thread in threadViewModels)
                        {
                            Threads.Add(thread);
                        }
                    });
                });
            }
            finally
            {
                IsRefreshing = false;
            }
        }
        
        /// <summary>
        /// Resets all metrics
        /// </summary>
        private void ResetMetrics()
        {
            _performanceMonitor.ResetMetrics();
            _ = RefreshDataAsync();
        }
        
        /// <summary>
        /// Handles metrics update events
        /// </summary>
        private void OnMetricsUpdated(object sender, MetricsUpdatedEventArgs e)
        {
            // Schedule a refresh
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await RefreshDataAsync();
            });
        }
        
        /// <summary>
        /// Starts a periodic background refresh
        /// </summary>
        private void StartPeriodicRefresh()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(5000); // Update every 5 seconds
                    await RefreshDataAsync();
                }
            });
        }
    }
    
    /// <summary>
    /// View model for a performance metric
    /// </summary>
    public class MetricViewModel : BaseViewModel
    {
        /// <summary>
        /// Creates a new metric view model
        /// </summary>
        public MetricViewModel(PerformanceMetric metric)
        {
            OperationName = metric.OperationName;
            Category = metric.Category.ToString();
            ExecutionCount = metric.ExecutionCount;
            FailureCount = metric.FailureCount;
            AverageExecutionTime = $"{metric.AverageExecutionTime.TotalMilliseconds:F1} ms";
            MaxExecutionTime = $"{metric.MaxExecutionTime.TotalMilliseconds:F1} ms";
            LastExecutionTime = metric.LastExecutionTime.ToString("HH:mm:ss");
            HasError = metric.LastError != null;
            
            // Set status color based on execution time
            if (metric.AverageExecutionTime.TotalMilliseconds > 1000)
                StatusColor = "Red";
            else if (metric.AverageExecutionTime.TotalMilliseconds > 500)
                StatusColor = "Orange";
            else if (metric.AverageExecutionTime.TotalMilliseconds > 100)
                StatusColor = "Yellow";
            else
                StatusColor = "Green";
        }
        
        /// <summary>
        /// Name of the operation
        /// </summary>
        public string OperationName { get; }
        
        /// <summary>
        /// Category of the metric
        /// </summary>
        public string Category { get; }
        
        /// <summary>
        /// Number of executions
        /// </summary>
        public int ExecutionCount { get; }
        
        /// <summary>
        /// Number of failures
        /// </summary>
        public int FailureCount { get; }
        
        /// <summary>
        /// Average execution time
        /// </summary>
        public string AverageExecutionTime { get; }
        
        /// <summary>
        /// Maximum execution time
        /// </summary>
        public string MaxExecutionTime { get; }
        
        /// <summary>
        /// Time of last execution
        /// </summary>
        public string LastExecutionTime { get; }
        
        /// <summary>
        /// Whether this operation has an error
        /// </summary>
        public bool HasError { get; }
        
        /// <summary>
        /// Color indicating the status
        /// </summary>
        public string StatusColor { get; }
    }
    
    /// <summary>
    /// View model for thread information
    /// </summary>
    public class ThreadViewModel : BaseViewModel
    {
        /// <summary>
        /// Creates a new thread view model
        /// </summary>
        public ThreadViewModel(ThreadInfo threadInfo)
        {
            ThreadId = threadInfo.ThreadId;
            Name = threadInfo.Name;
            Category = threadInfo.Category.ToString();
            Status = threadInfo.Status.ToString();
            CurrentOperation = threadInfo.CurrentOperation ?? "None";
            
            // Calculate duration
            var duration = DateTime.Now - threadInfo.StartTime;
            Duration = $"{duration.TotalMinutes:F1} min";
            
            // Determine status color
            switch (threadInfo.Status)
            {
                case ThreadStatus.Running:
                    StatusColor = "Green";
                    break;
                case ThreadStatus.Waiting:
                    StatusColor = "Yellow";
                    break;
                case ThreadStatus.Blocked:
                    StatusColor = "Red";
                    break;
                default:
                    StatusColor = "Gray";
                    break;
            }
        }
        
        /// <summary>
        /// Thread identifier
        /// </summary>
        public int ThreadId { get; }
        
        /// <summary>
        /// Thread name
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// Thread category
        /// </summary>
        public string Category { get; }
        
        /// <summary>
        /// Current thread status
        /// </summary>
        public string Status { get; }
        
        /// <summary>
        /// Current operation being performed
        /// </summary>
        public string CurrentOperation { get; }
        
        /// <summary>
        /// How long the thread has been running
        /// </summary>
        public string Duration { get; }
        
        /// <summary>
        /// Color indicating the status
        /// </summary>
        public string StatusColor { get; }
    }
}
