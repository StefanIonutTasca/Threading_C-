using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using TransportTracker.App.Core.Diagnostics;
using TransportTracker.App.Models;
using TransportTracker.App.Models.Statistics;
using TransportTracker.App.Services;

namespace TransportTracker.App.ViewModels
{
    public class TransportStatisticsViewModel : BaseViewModel
    {
        private readonly ITransportApiService _apiService;
        private readonly ICacheManager _cacheManager;
        private List<TransportVehicle> _vehicleData;
        private List<RouteInfo> _routeData;
        private List<TransportStop> _stopData;
        private bool _isCalculating;
        private bool _hasData;
        private bool _useRealData;
        private DateTime _lastUpdated;
        private TransportMetricsCollection _currentMetrics;
        
        public TransportStatisticsViewModel(ITransportApiService apiService = null, ICacheManager cacheManager = null)
        {
            Title = "Transport Statistics";
            
            // Initialize services (use injected or create new)
            _apiService = apiService;
            _cacheManager = cacheManager;
            
            // Initialize collections for statistics
            TransportTypeStats = new ObservableCollection<TransportTypeMetric>();
            ActivityOverTime = new ObservableCollection<TimeSeriesDataPoint>();
            RoutePopularity = new ObservableCollection<RoutePopularityMetric>();
            
            // Initialize commands
            RefreshCommand = new Command(async () => await RefreshDataAsync(true));
            ToggleDataSourceCommand = new Command(ToggleDataSource);
            
            // Initial load
            Task.Run(async () => await RefreshDataAsync(false));
        }
        
        public ObservableCollection<TransportTypeMetric> TransportTypeStats { get; }
        
        public ObservableCollection<TimeSeriesDataPoint> ActivityOverTime { get; }
        
        public ObservableCollection<RoutePopularityMetric> RoutePopularity { get; }
        
        public ICommand RefreshCommand { get; }
        
        public ICommand ToggleDataSourceCommand { get; }
        
        public bool IsCalculating
        {
            get => _isCalculating;
            set => SetProperty(ref _isCalculating, value);
        }
        
        public bool HasData
        {
            get => _hasData;
            set => SetProperty(ref _hasData, value);
        }
        
        public bool UseRealData
        {
            get => _useRealData;
            set => SetProperty(ref _useRealData, value);
        }
        
        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set => SetProperty(ref _lastUpdated, value, () => OnPropertyChanged(nameof(LastUpdatedText)));
        }
        
        public string LastUpdatedText => LastUpdated != DateTime.MinValue 
            ? $"Last Updated: {LastUpdated:HH:mm:ss}" 
            : "Not updated yet";
            
        public TransportMetricsCollection CurrentMetrics
        {
            get => _currentMetrics;
            set => SetProperty(ref _currentMetrics, value);
        }
        
        /// <summary>
        /// Refresh statistics data
        /// </summary>
        private async Task RefreshDataAsync(bool forceRefresh)
        {
            if (IsCalculating)
                return;
                
            try
            {
                IsCalculating = true;
                
                using (PerformanceMonitor.Instance.StartOperation("StatisticsRefresh"))
                {
                    // Load data either from API or generate mock data
                    await LoadDataAsync(forceRefresh);
                    
                    // Update metrics from data
                    if (_vehicleData != null && _vehicleData.Any())
                    {
                        // Calculate metrics
                        var metricsCalculator = new TransportMetricsCalculator();
                        var metrics = await Task.Run(() => 
                            metricsCalculator.CalculateMetrics(_vehicleData, _routeData, _stopData));
                            
                        CurrentMetrics = metrics;
                        
                        // Update UI collections
                        TransportTypeStats.Clear();
                        foreach (var stat in metrics.TransportTypeMetrics)
                        {
                            TransportTypeStats.Add(stat);
                        }
                        
                        ActivityOverTime.Clear();
                        foreach (var dataPoint in metrics.ActivityByHour)
                        {
                            ActivityOverTime.Add(dataPoint);
                        }
                        
                        RoutePopularity.Clear();
                        foreach (var route in metrics.PopularRoutes.Take(5))
                        {
                            RoutePopularity.Add(route);
                        }
                        
                        // Mark that we have data
                        HasData = true;
                        LastUpdated = DateTime.Now;
                    }
                    else
                    {
                        HasData = false;
                    }
                }
            }
            catch (Exception ex)
            {
                PerformanceMonitor.Instance.RecordFailure("StatisticsRefresh", ex);
                System.Diagnostics.Debug.WriteLine($"Error refreshing statistics: {ex.Message}");
            }
            finally
            {
                IsCalculating = false;
            }
        }
        
        /// <summary>
        /// Load underlying data for statistics calculations
        /// </summary>
        private async Task LoadDataAsync(bool forceRefresh)
        {
            try
            {
                if (_useRealData && _apiService != null)
                {
                    // Use real API with caching
                    _vehicleData = await _apiService.GetVehiclesAsync(null, forceRefresh);
                    _routeData = await _apiService.GetRoutesAsync(null, forceRefresh);
                    _stopData = await _apiService.GetStopsAsync(null, forceRefresh);
                }
                else
                {
                    // Generate mock data for testing
                    _vehicleData = GenerateMockVehicleData();
                    _routeData = GenerateMockRouteData();
                    _stopData = GenerateMockStopData();
                }
            }
            catch (Exception ex)
            {
                PerformanceMonitor.Instance.RecordFailure("LoadStatisticsData", ex);
                System.Diagnostics.Debug.WriteLine($"Error loading statistics data: {ex.Message}");
                
                // Fall back to mock data
                _vehicleData = GenerateMockVehicleData();
                _routeData = GenerateMockRouteData();
                _stopData = GenerateMockStopData();
            }
        }
        
        /// <summary>
        /// Toggle between real API data and mock data
        /// </summary>
        private void ToggleDataSource()
        {
            UseRealData = !UseRealData;
            Task.Run(async () => await RefreshDataAsync(true));
        }
        
        // Mock data generators
        private List<TransportVehicle> GenerateMockVehicleData()
        {
            var random = new Random();
            var vehicles = new List<TransportVehicle>();
            string[] types = { "Bus", "Train", "Tram", "Subway", "Ferry" };
            
            // Generate 100 random vehicles of different types
            for (int i = 0; i < 100; i++)
            {
                var type = types[random.Next(types.Length)];
                var speed = random.Next(20, 80);
                var occupancy = random.Next(5, 100);
                var delay = random.Next(-5, 15);
                
                vehicles.Add(new TransportVehicle
                {
                    Id = $"VEH{i}",
                    RouteId = $"R{random.Next(1, 20)}",
                    Type = type,
                    Speed = speed,
                    Occupancy = occupancy,
                    DelayMinutes = delay,
                    LastUpdated = DateTime.Now.AddMinutes(-random.Next(0, 60)),
                    Status = delay > 5 ? "Delayed" : "On Time"
                });
            }
            
            return vehicles;
        }
        
        private List<RouteInfo> GenerateMockRouteData()
        {
            var random = new Random();
            var routes = new List<RouteInfo>();
            string[] types = { "Bus", "Train", "Tram", "Subway", "Ferry" };
            
            // Generate 20 routes
            for (int i = 1; i <= 20; i++)
            {
                var type = types[random.Next(types.Length)];
                routes.Add(new RouteInfo
                {
                    Id = $"R{i}",
                    Name = $"Route {i}",
                    Type = type,
                    Color = $"#{random.Next(0x1000000):X6}",
                    Distance = random.Next(5, 50)
                });
            }
            
            return routes;
        }
        
        private List<TransportStop> GenerateMockStopData()
        {
            var random = new Random();
            var stops = new List<TransportStop>();
            
            // Generate 50 stops distributed across the 20 routes
            for (int i = 1; i <= 50; i++)
            {
                var routeId = $"R{random.Next(1, 21)}";
                stops.Add(new TransportStop
                {
                    Id = $"S{i}",
                    Name = $"Stop {i}",
                    RouteId = routeId,
                    IsAccessible = random.Next(2) == 1,
                    HasShelter = random.Next(2) == 1
                });
            }
            
            return stops;
        }
    }
}
