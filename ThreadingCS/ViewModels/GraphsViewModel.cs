using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ThreadingCS.Models;
using ThreadingCS.Services;

namespace ThreadingCS.ViewModels
{
    public class GraphsViewModel : INotifyPropertyChanged
    {
        private readonly TransportApiService _apiService;
        private readonly DataProcessingService _processingService;
        private CancellationTokenSource _cancellationTokenSource;
        private Grid _barChartContainer;
        private List<TransportRoute> _routeData;
        
        private bool _isLoading;
        private bool _isGeneratingChart;
        private bool _hasData;
        private bool _hasRouteDistribution;
        private double _processingProgress;
        private int _processedRoutes;
        private string _statusMessage;
        private string _selectedChartType;
        
        public ObservableCollection<string> ChartTypes { get; } = new();
        public ObservableCollection<KeyValuePair<string, int>> RouteDistribution { get; } = new();

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
        
        public bool IsGeneratingChart
        {
            get => _isGeneratingChart;
            set
            {
                _isGeneratingChart = value;
                OnPropertyChanged();
            }
        }
        
        public bool HasData
        {
            get => _hasData;
            set
            {
                _hasData = value;
                OnPropertyChanged();
            }
        }
        
        public bool HasRouteDistribution
        {
            get => _hasRouteDistribution;
            set
            {
                _hasRouteDistribution = value;
                OnPropertyChanged();
            }
        }
        
        public double ProcessingProgress
        {
            get => _processingProgress;
            set
            {
                _processingProgress = value;
                OnPropertyChanged();
            }
        }
        
        public int ProcessedRoutes
        {
            get => _processedRoutes;
            set
            {
                _processedRoutes = value;
                OnPropertyChanged();
            }
        }
        
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
        
        public string SelectedChartType
        {
            get => _selectedChartType;
            set
            {
                _selectedChartType = value;
                OnPropertyChanged();
                RefreshChart();
            }
        }
        
        // Statistics properties
        public int StatsRouteCount { get; private set; }
        public double StatsAverageDuration { get; private set; }
        public string StatsShortestRoute { get; private set; }
        public string StatsLongestRoute { get; private set; }
        
        public GraphsViewModel()
        {
            _apiService = new TransportApiService();
            _processingService = new DataProcessingService();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Initialize chart types
            ChartTypes.Add("Travel Duration");
            ChartTypes.Add("Distance Distribution");
            ChartTypes.Add("Routes by Agency");
            
            SelectedChartType = ChartTypes[0];
            StatusMessage = "Ready to generate data";
        }
        
        public void SetBarChartContainer(Grid container)
        {
            _barChartContainer = container;
            
            // If we already have data, re-render the chart
            if (_hasData)
            {
                RefreshChart();
            }
        }
        
        public async Task GenerateChartDataAsync()
        {
            IsLoading = true;
            IsGeneratingChart = true;
            StatusMessage = "Generating large dataset for visualization...";
            ProcessingProgress = 0.1;
            
            try
            {
                await Task.Run(async () =>
                {
                    // Generate a large dataset for visualization
                    _routeData = _apiService.GenerateLargeDataset(100000);
                    ProcessedRoutes = _routeData.Count;
                    ProcessingProgress = 0.4;
                    
                    // Calculate statistics
                    await CalculateStatisticsAsync();
                    ProcessingProgress = 0.7;
                    
                    // Calculate route distribution by agency
                    await CalculateRouteDistributionAsync();
                    ProcessingProgress = 0.9;
                    
                    // Set flags to show data is available
                    HasData = true;
                });
                
                // Refresh the chart on the UI thread
                MainThread.BeginInvokeOnMainThread(() => {
                    RefreshChart();
                    StatusMessage = $"Generated visualization from {ProcessedRoutes:N0} routes";
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error generating data: {ex.Message}";
            }
            finally
            {
                ProcessingProgress = 1.0;
                IsLoading = false;
                IsGeneratingChart = false;
            }
        }
        
        public async Task StartLiveUpdatesAsync()
        {
            if (_cancellationTokenSource?.IsCancellationRequested ?? true)
            {
                _cancellationTokenSource = new CancellationTokenSource();
            }
            
            var token = _cancellationTokenSource.Token;
            
            // Generate initial data if we don't have any yet
            if (!HasData)
            {
                await GenerateChartDataAsync();
            }
            
            await Task.Run(async () =>
            {
                try
                {
                    StatusMessage = "Live data updates active";
                    
                    while (!token.IsCancellationRequested)
                    {
                        // Update some route data randomly to simulate real-time changes
                        await UpdateRandomRoutesAsync();
                        
                        // Recalculate statistics
                        await CalculateStatisticsAsync();
                        
                        // Update the chart
                        MainThread.BeginInvokeOnMainThread(() => {
                            RefreshChart();
                            StatusMessage = $"Updated at {DateTime.Now:HH:mm:ss}";
                        });
                        
                        // Wait before next update
                        await Task.Delay(3000, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when canceled
                }
                catch (Exception ex)
                {
                    MainThread.BeginInvokeOnMainThread(() => {
                        StatusMessage = $"Update error: {ex.Message}";
                    });
                }
            }, token);
        }
        
        public void StopLiveUpdates()
        {
            _cancellationTokenSource?.Cancel();
            StatusMessage = "Live updates stopped";
        }
        
        private async Task CalculateStatisticsAsync()
        {
            if (_routeData == null || !_routeData.Any())
                return;
                
            await Task.Run(() =>
            {
                // Use PLINQ for parallel calculation of statistics
                var stats = _routeData.AsParallel().Aggregate(
                    new {
                        Count = 0,
                        TotalDuration = 0.0,
                        Min = double.MaxValue,
                        MinRoute = "",
                        Max = double.MinValue,
                        MaxRoute = ""
                    },
                    (acc, route) => {
                        var newMin = Math.Min(acc.Min, route.Duration);
                        var newMax = Math.Max(acc.Max, route.Duration);
                        
                        return new {
                            Count = acc.Count + 1,
                            TotalDuration = acc.TotalDuration + route.Duration,
                            Min = newMin,
                            MinRoute = newMin < acc.Min ? route.RouteName : acc.MinRoute,
                            Max = newMax,
                            MaxRoute = newMax > acc.Max ? route.RouteName : acc.MaxRoute
                        };
                    },
                    acc => new {
                        acc.Count,
                        Average = acc.Count > 0 ? acc.TotalDuration / acc.Count : 0,
                        acc.Min,
                        acc.MinRoute,
                        acc.Max,
                        acc.MaxRoute
                    }
                );
                
                MainThread.BeginInvokeOnMainThread(() => {
                    StatsRouteCount = stats.Count;
                    StatsAverageDuration = stats.Average;
                    StatsShortestRoute = $"{stats.MinRoute} ({stats.Min:F1} min)";
                    StatsLongestRoute = $"{stats.MaxRoute} ({stats.Max:F1} min)";
                    OnPropertyChanged(nameof(StatsRouteCount));
                    OnPropertyChanged(nameof(StatsAverageDuration));
                    OnPropertyChanged(nameof(StatsShortestRoute));
                    OnPropertyChanged(nameof(StatsLongestRoute));
                });
            });
        }
        
        private async Task CalculateRouteDistributionAsync()
        {
            if (_routeData == null || !_routeData.Any())
                return;
                
            await Task.Run(() =>
            {
                // Use PLINQ to group and count routes by agency
                var distribution = _processingService.GroupRoutesByAgency(_routeData);
                
                // Sort by count descending
                var sortedDistribution = distribution
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(10) // Limit to top 10 agencies
                    .ToList();
                
                MainThread.BeginInvokeOnMainThread(() => {
                    RouteDistribution.Clear();
                    foreach (var item in sortedDistribution)
                    {
                        RouteDistribution.Add(item);
                    }
                    HasRouteDistribution = RouteDistribution.Any();
                });
            });
        }
        
        private async Task UpdateRandomRoutesAsync()
        {
            if (_routeData == null || !_routeData.Any())
                return;
                
            await Task.Run(() =>
            {
                // Update random routes to simulate real-time changes
                var random = new Random();
                int updateCount = random.Next(500, 2000); // Update between 0.5% and 2% of routes
                
                Parallel.For(0, updateCount, i =>
                {
                    int index = random.Next(_routeData.Count);
                    var route = _routeData[index];
                    
                    // Small random changes to duration and distance
                    route.Duration += (random.NextDouble() - 0.5) * 2; // +/- 1 minute
                    route.Distance += (random.NextDouble() - 0.5) * 0.2; // +/- 0.1 km
                    
                    // Ensure values remain positive
                    route.Duration = Math.Max(1, route.Duration);
                    route.Distance = Math.Max(0.1, route.Distance);
                });
            });
        }
        
        private void RefreshChart()
        {
            if (_barChartContainer == null || _routeData == null || !_routeData.Any())
                return;
                
            IsGeneratingChart = true;
            
            // Clear existing chart
            _barChartContainer.Clear();
            
            // Setup row definitions
            _barChartContainer.RowDefinitions.Clear();
            for (int i = 0; i < 5; i++)
            {
                _barChartContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            
            try
            {
                // Generate data for the selected chart type
                List<KeyValuePair<string, double>> chartData = new();
                
                if (SelectedChartType == "Travel Duration")
                {
                    // Group routes by duration ranges
                    var durationGroups = _routeData.AsParallel()
                        .GroupBy(r => (int)Math.Floor(r.Duration / 15) * 15) // Group in 15-minute intervals
                        .OrderBy(g => g.Key)
                        .Take(5)
                        .ToDictionary(g => g.Key, g => (double)g.Count());
                        
                    foreach (var group in durationGroups)
                    {
                        chartData.Add(new KeyValuePair<string, double>($"{group.Key}-{group.Key + 15} min", group.Value));
                    }
                }
                else if (SelectedChartType == "Distance Distribution")
                {
                    // Group routes by distance ranges
                    var distanceGroups = _routeData.AsParallel()
                        .GroupBy(r => (int)Math.Floor(r.Distance / 5) * 5) // Group in 5km intervals
                        .OrderBy(g => g.Key)
                        .Take(5)
                        .ToDictionary(g => g.Key, g => (double)g.Count());
                        
                    foreach (var group in distanceGroups)
                    {
                        chartData.Add(new KeyValuePair<string, double>($"{group.Key}-{group.Key + 5} km", group.Value));
                    }
                }
                else if (SelectedChartType == "Routes by Agency")
                {
                    // Group routes by agency
                    var agencyGroups = _routeData.AsParallel()
                        .GroupBy(r => r.AgencyName)
                        .OrderByDescending(g => g.Count())
                        .Take(5)
                        .ToDictionary(g => g.Key, g => (double)g.Count());
                        
                    foreach (var group in agencyGroups)
                    {
                        chartData.Add(new KeyValuePair<string, double>(group.Key, group.Value));
                    }
                }
                
                // Find the maximum value for scaling
                double maxValue = chartData.Any() ? chartData.Max(d => d.Value) : 0;
                
                // Create bars
                for (int i = 0; i < chartData.Count && i < 5; i++)
                {
                    // Add label
                    var label = new Label
                    {
                        Text = chartData[i].Key,
                        VerticalOptions = LayoutOptions.Center
                    };
                    _barChartContainer.Add(label, 0, i);
                    
                    // Create a Frame as the bar
                    var barValue = chartData[i].Value;
                    var barWidth = (barValue / maxValue) * 100;
                    
                    var barContainer = new Grid
                    {
                        HeightRequest = 25
                    };
                    
                    var bar = new BoxView
                    {
                        Color = GetChartColor(i),
                        WidthRequest = barWidth,
                        HorizontalOptions = LayoutOptions.Start
                    };
                    barContainer.Add(bar);
                    
                    var valueLabel = new Label
                    {
                        Text = $"{barValue:N0}",
                        TextColor = Colors.Black,
                        FontSize = 12,
                        HorizontalOptions = LayoutOptions.Start,
                        Margin = new Thickness(5, 0, 0, 0)
                    };
                    barContainer.Add(valueLabel);
                    
                    _barChartContainer.Add(barContainer, 1, i);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error rendering chart: {ex.Message}";
            }
            finally
            {
                IsGeneratingChart = false;
            }
        }
        
        private Color GetChartColor(int index)
        {
            // Return different colors based on index
            switch (index % 5)
            {
                case 0: return Color.FromArgb("#4285F4"); // Google Blue
                case 1: return Color.FromArgb("#EA4335"); // Google Red
                case 2: return Color.FromArgb("#FBBC05"); // Google Yellow
                case 3: return Color.FromArgb("#34A853"); // Google Green
                case 4: return Color.FromArgb("#9C27B0"); // Purple
                default: return Colors.Gray;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
