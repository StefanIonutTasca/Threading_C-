using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TransportTracker.App.Core.Processing;
using TransportTracker.App.Models;
using TransportTracker.App.Views.Maps.Clustering;
using TransportTracker.App.Views.Maps.Overlays;

namespace TransportTracker.App.ViewModels
{
    public class MapViewModel : BaseViewModel
    {
        private bool _isDataLoaded;
        private DateTime _lastUpdated;
        private int _vehicleCount;
        private bool _isRefreshing;
        private string _selectedMapType = "Street";
        private Dictionary<string, bool> _transportFilters;
        private Map _map;
        private VehicleClusterManager _clusterManager;
        private RouteOverlayManager _routeManager;
        private HeatmapManager _heatmapManager;
        private TransportBatchService _batchService;
        private CancellationTokenSource _processingCts;
        private bool _showRoutes = true;
        private bool _enableClustering = false;
        private bool _showStops = true;
        private bool _showHeatmap = false;
        private double _heatmapOpacity = 0.7;
        private string _selectedRouteId;
        private BatchProcessingProgress _processingProgress;

        public MapViewModel()
        {
            // Set view model properties
            Title = "Map";
            Icon = "map_icon.png";
            
            // Initialize commands
            RefreshCommand = CreateAsyncCommand(RefreshData);
            ChangeMapTypeCommand = CreateCommand<string>(OnMapTypeChanged);
            ToggleFilterCommand = CreateCommand<string>(OnFilterToggled);
            ZoomToUserLocationCommand = CreateAsyncCommand(ZoomToUserLocationAsync);
            ToggleRoutesCommand = new Command(OnToggleRoutes);
            ToggleClusteringCommand = new Command(OnToggleClustering);
            ToggleStopsCommand = new Command(OnToggleStops);
            ToggleHeatmapCommand = new Command(OnToggleHeatmap);
            RouteSelectedCommand = new Command<string>(OnRouteSelected);
            CancelProcessingCommand = new Command(OnCancelProcessing);
            
            // Initialize batch service with progress reporting
            _batchService = new TransportBatchService(new Progress<BatchProcessingProgress>(p => ProcessingProgress = p));
            
            // Initialize filters for transport types
            _transportFilters = new Dictionary<string, bool>
            {
                { "Bus", true },
                { "Train", true },
                { "Tram", true },
                { "Subway", true },
                { "Ferry", true }
            };
            
            // Initialize collections
            VehiclePins = new ObservableRangeCollection<Pin>();
            Routes = new ObservableRangeCollection<RouteInfo>();
            Stops = new ObservableRangeCollection<TransportStop>();
        }

        public bool IsDataLoaded
        {
            get => _isDataLoaded;
            set => SetProperty(ref _isDataLoaded, value);
        }

        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set => SetProperty(ref _lastUpdated, value, () => OnPropertyChanged(nameof(LastUpdatedText)));
        }

        public string LastUpdatedText => LastUpdated != DateTime.MinValue 
            ? $"Last Updated: {LastUpdated:HH:mm:ss}" 
            : "Not updated yet";

        public int VehicleCount
        {
            get => _vehicleCount;
            set => SetProperty(ref _vehicleCount, value, () => OnPropertyChanged(nameof(VehicleCountText)));
        }

        public string VehicleCountText => $"{VehicleCount} vehicles visible";

        public string SelectedMapType
        {
            get => _selectedMapType;
            set => SetProperty(ref _selectedMapType, value);
        }

        public Dictionary<string, bool> TransportFilters
        {
            get => _transportFilters;
            set => SetProperty(ref _transportFilters, value);
        }

        public bool GetFilter(string transportType)
        {
            if (_transportFilters.TryGetValue(transportType, out bool value))
            {
                return value;
            }
            return true; // Default to showing all types
        }

        public void SetFilter(string transportType, bool value)
        {
            if (_transportFilters.ContainsKey(transportType))
            {
                _transportFilters[transportType] = value;
                OnPropertyChanged(nameof(TransportFilters));
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ChangeMapTypeCommand { get; }
        public ICommand ToggleFilterCommand { get; }
        public ICommand ZoomToUserLocationCommand { get; }
        public ICommand ToggleRoutesCommand { get; }
        public ICommand ToggleClusteringCommand { get; }
        public ICommand ToggleStopsCommand { get; }
        public ICommand ToggleHeatmapCommand { get; }
        public ICommand RouteSelectedCommand { get; }
        public ICommand CancelProcessingCommand { get; }
        
        public ObservableRangeCollection<Pin> VehiclePins { get; private set; }
        public ObservableRangeCollection<RouteInfo> Routes { get; private set; }
        public ObservableRangeCollection<TransportStop> Stops { get; private set; }
        
        public Location UserLocation { get; private set; }
        
        public bool ShowRoutes
        {
            get => _showRoutes;
            set => SetProperty(ref _showRoutes, value);
        }
        
        public bool EnableClustering
        {
            get => _enableClustering;
            set => SetProperty(ref _enableClustering, value);
        }
        
        public bool ShowStops
        {
            get => _showStops;
            set => SetProperty(ref _showStops, value);
        }
        
        public bool ShowHeatmap
        {
            get => _showHeatmap;
            set
            {
                if (SetProperty(ref _showHeatmap, value) && _heatmapManager != null)
                {
                    _heatmapManager.IsVisible = value;
                }
            }
        }
        
        public double HeatmapOpacity
        {
            get => _heatmapOpacity;
            set
            {
                if (SetProperty(ref _heatmapOpacity, value) && _heatmapManager != null)
                {
                    _heatmapManager.Opacity = value;
                }
            }
        }
        
        public BatchProcessingProgress ProcessingProgress
        {
            get => _processingProgress;
            private set => SetProperty(ref _processingProgress, value);
        }
        
        public string SelectedRouteId
        {
            get => _selectedRouteId;
            set => SetProperty(ref _selectedRouteId, value);
        }

        public override async Task InitializeAsync()
        {
            if (IsInitialized)
                return;
                
            await RefreshData();
            IsInitialized = true;
        }
        
        public void SetMap(Map map)
        {
            _map = map;
            
            // Initialize managers with the map
            _clusterManager = new VehicleClusterManager(_map);
            _routeManager = new RouteOverlayManager(_map);
            _heatmapManager = new HeatmapManager(_map)
            {
                IsVisible = ShowHeatmap,
                Opacity = HeatmapOpacity
            };
            
            // Subscribe to heatmap progress updates
            _heatmapManager.ProgressChanged += (sender, progress) => ProcessingProgress = progress;
        }
        
        private async Task RefreshDataAsync()
        {
            try
            {
                IsBusy = true;
                
                // Cancel any ongoing processing
                OnCancelProcessing();
                _processingCts = new CancellationTokenSource();
                
                // Generate test data for now
                var vehicles = GenerateMockVehicleData(20);
                var routes = GenerateMockRoutes();
                var stops = GenerateMockStops(routes);
                
                // Use batch processing for data updates when available
                if (_batchService != null)
                {
                    try
                    {                        
                        // Process vehicles in batches (here we're not transforming the data, just demonstrating batch processing)
                        vehicles = (await _batchService.ProcessVehiclesAsync(
                            vehicles, 
                            v => {
                                // Apply any processing logic here
                                return v;
                            },
                            _processingCts.Token
                        )).ToList();
                    }
                    catch (OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine("Vehicle processing was cancelled");
                    }
                }
                
                // Update map pins
                if (EnableClustering && _clusterManager != null)
                {
                    ApplyClustering(vehicles);
                }
                else
                {
                    UpdateMapPins(vehicles);
                }
                
                // Update routes and stops if enabled
                if (ShowRoutes && _routeManager != null)
                {
                    UpdateRoutes(routes, stops);
                }
                else if (_routeManager != null)
                {
                    _routeManager.ClearAll();
                }
                
                // Update heatmap if enabled
                if (ShowHeatmap && _heatmapManager != null)
                {
                    await UpdateHeatmapAsync(vehicles, stops);
                }
                
                // Update observable collections for binding
                Routes.Clear();
                foreach (var route in routes)
                {
                    Routes.Add(route);
                }
                
                Stops.Clear();
                foreach (var stop in stops)
                {
                    Stops.Add(stop);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing map data: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        private async Task RefreshData()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                IsRefreshing = true;
                
                await RefreshDataAsync();
                
                IsDataLoaded = true;
                LastUpdated = DateTime.Now;
                VehicleCount = 20;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing data: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }
        
        private List<TransportVehicle> GenerateMockVehicleData(int count)
        {
            var random = new Random();
            var vehicles = new List<TransportVehicle>();
            
            var types = new[] { "Bus", "Train", "Tram", "Subway", "Ferry" };
            var statuses = new[] { "On Time", "Delayed", "Out of Service", "Arriving", "Departing" };
            
            for (int i = 1; i <= count; i++)
            {
                var type = types[random.Next(types.Length)];
                
                // Only add vehicles of types that are not filtered out
                if (!GetFilter(type))
                    continue;
                    
                vehicles.Add(new TransportVehicle
                {
                    Id = Guid.NewGuid().ToString(),
                    Number = $"{type[0]}-{random.Next(100, 999)}",
                    Type = type,
                    Route = $"Route {random.Next(1, 30)}",
                    NextStop = $"Stop {random.Next(1, 20)}",
                    Status = statuses[random.Next(statuses.Length)],
                    Capacity = random.Next(10, 250),
                    Occupancy = random.Next(1, 250),
                    LastUpdated = DateTime.Now.AddMinutes(-random.Next(1, 30)),
                    Latitude = 51.5 + (random.NextDouble() - 0.5) * 0.1,
                    Longitude = -0.12 + (random.NextDouble() - 0.5) * 0.1,
                    Speed = random.Next(0, 120)
                });
            }
            
            return vehicles;
        }
        
        private void UpdateMapPins(List<TransportVehicle> vehicles)
        {
            // Clear existing pins if not using clustering
            if (!EnableClustering || _clusterManager == null)
            {
                VehiclePins.Clear();
                
                // Add a pin for each vehicle
                foreach (var vehicle in vehicles)
                {
                    if (TransportFilters.TryGetValue(vehicle.Type, out bool isVisible) && isVisible)
                    {
                        var vehiclePin = new TransportVehicle
                        {
                            Id = vehicle.Id,
                            Number = vehicle.Number,
                            Type = vehicle.Type,
                            Route = vehicle.Route,
                            NextStop = vehicle.NextStop,
                            Status = vehicle.Status,
                            Capacity = vehicle.Capacity,
                            Occupancy = vehicle.Occupancy,
                            LastUpdated = vehicle.LastUpdated,
                            Speed = vehicle.Speed,
                            Label = vehicle.Number,
                            Address = $"{vehicle.Route} - {vehicle.Status}",
                            Location = new Location(vehicle.Latitude, vehicle.Longitude)
                        };
                        
                        VehiclePins.Add(vehiclePin);
                    }
                }
            }
        }
        
        private void OnMapTypeChanged(string mapType)
        {
            if (!string.IsNullOrEmpty(mapType))
            {
                SelectedMapType = mapType;
            }
        }
        
        private void OnFilterToggled(string transportType)
        {
            if (string.IsNullOrEmpty(transportType))
                return;
                
            // Toggle filter status
            SetFilter(transportType, !GetFilter(transportType));
            
            // Refresh data with the new filter
            RefreshCommand.Execute(null);
        }
        
        private async Task ZoomToUserLocationAsync()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium);
                var location = await Geolocation.GetLocationAsync(request);
                
                if (location != null)
                {
                    UserLocation = new Location(location.Latitude, location.Longitude);
                    OnPropertyChanged(nameof(UserLocation));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unable to get location: {ex.Message}");
                // In a real app, we'd show a user-friendly error message
            }
        }
        
        private void ApplyClustering(List<TransportVehicle> vehicles)
        {
            if (_map == null || _clusterManager == null)
                return;
                
            // Convert regular vehicle models to TransportVehicle pins
            var vehiclePins = vehicles.Where(v => TransportFilters.TryGetValue(v.Type, out bool isVisible) && isVisible)
                .Select(v => new TransportVehicle
                {
                    Id = v.Id,
                    Number = v.Number,
                    Type = v.Type,
                    Route = v.Route,
                    NextStop = v.NextStop,
                    Status = v.Status,
                    Capacity = v.Capacity,
                    Occupancy = v.Occupancy,
                    LastUpdated = v.LastUpdated,
                    Speed = v.Speed,
                    Label = v.Number,
                    Address = $"{v.Route} - {v.Status}",
                    Location = new Location(v.Latitude, v.Longitude)
                })
                .ToList();
                
            // Update the cluster manager with the new vehicles
            _clusterManager.UpdateClusters(vehiclePins);
            _clusterManager.ApplyClustersToMap(true);
        }
        
        private List<RouteInfo> GenerateMockRoutes()
        {
            var random = new Random();
            var routes = new List<RouteInfo>();
            
            var types = new[] { "Bus", "Train", "Tram", "Subway", "Ferry" };
            var origins = new[] { "Central Station", "North Terminal", "South Terminal", "East Plaza", "West Plaza", "Downtown", "Airport" };
            var destinations = new[] { "University", "Business District", "Shopping Mall", "Stadium", "Hospital", "Airport", "Tech Park" };
            
            for (int i = 1; i <= 10; i++)
            {
                var type = types[random.Next(types.Length)];
                
                // Only add routes of types that are not filtered out
                if (!GetFilter(type))
                    continue;
                    
                var origin = origins[random.Next(origins.Length)];
                var destination = destinations[random.Next(destinations.Length)];
                
                // Avoid same origin and destination
                while (destination == origin)
                    destination = destinations[random.Next(destinations.Length)];
                
                routes.Add(new RouteInfo
                {
                    Id = $"{type.ToLower()}-{i}",
                    Name = $"{type} {i}",
                    Code = $"{type[0]}{i}",
                    Type = type,
                    Origin = origin,
                    Destination = destination,
                    FrequencyMinutes = random.Next(5, 30),
                    IsActive = random.NextDouble() > 0.1, // 90% active
                    IsBidirectional = random.NextDouble() > 0.2, // 80% bidirectional
                    VehicleCount = random.Next(1, 10),
                    IsVisible = true
                });
            }
            
            return routes;
        }
        
        private List<TransportStop> GenerateMockStops(List<RouteInfo> routes)
        {
            var random = new Random();
            var stops = new List<TransportStop>();
            var stopNames = new[] { "Main St", "Park Ave", "Central", "Broadway", "Market St", "Station Rd",
                                "University", "Hospital", "Stadium", "Airport", "Plaza", "Mall", "Harbor" };
            
            foreach (var route in routes)
            {
                // Generate 3-8 stops per route
                int stopCount = random.Next(3, 9);
                
                for (int i = 0; i < stopCount; i++)
                {
                    var stopName = $"{stopNames[random.Next(stopNames.Length)]} {random.Next(1, 50)}";
                    
                    // Calculate a position along the route (simple linear interpolation between endpoints)
                    double progress = (double)i / (stopCount - 1);
                    var lat = 51.5 + (random.NextDouble() - 0.5) * 0.1;
                    var lon = -0.12 + (random.NextDouble() - 0.5) * 0.1;
                    
                    var stop = new TransportStop(
                        $"{route.Type.ToLower()}-stop-{route.Id}-{i}",
                        new Location(lat, lon),
                        stopName,
                        route.Type,
                        new List<string> { route.Id }
                    );
                    
                    // Set the next arrival time randomly
                    var nextArrival = DateTime.Now.AddMinutes(random.Next(1, 30));
                    stop.UpdateNextArrival(nextArrival);
                    
                    // Set accessibility and levels randomly
                    stop.IsAccessible = random.NextDouble() > 0.2; // 80% accessible
                    stop.HasMultipleLevels = random.NextDouble() > 0.7; // 30% multi-level
                    
                    stops.Add(stop);
                }
            }
            
            return stops;
        }
        
        private void UpdateRoutes(List<RouteInfo> routes, List<TransportStop> stops)
        {
            if (_map == null || _routeManager == null)
                return;
                
            // Clear existing routes first
            _routeManager.ClearAll();
            
            foreach (var route in routes)
            {              
                if (!TransportFilters.TryGetValue(route.Type, out bool isVisible) || !isVisible)
                    continue;
                    
                // Find stops for this route
                var routeStops = stops.Where(s => s.Routes.Contains(route.Id)).ToList();
                
                if (routeStops.Count < 2)
                    continue; // Need at least 2 stops to make a route
                    
                // Create a path between stops (could be enhanced with actual road paths in a real app)
                var path = routeStops.Select(s => s.Location).ToList();
                
                // Add some intermediate points to make the route more realistic
                var enhancedPath = EnhanceRoutePath(path);
                
                // Add the route to the map
                _routeManager.AddOrUpdateRoute(
                    route.Id,
                    route.Name,
                    enhancedPath,
                    null, // Let the manager assign a color
                    5f,   // Line width
                    false // Not dashed
                );
                
                // Add stops to the route
                _routeManager.AddStopsToRoute(routeStops, route.Id);
                
                // Highlight the selected route if any
                if (route.Id == SelectedRouteId)
                {
                    _routeManager.HighlightRoute(route.Id, true);
                }
                
                // Hide routes that should be hidden
                if (!route.IsVisible)
                {
                    _routeManager.ToggleRouteVisibility(route.Id, false);
                }
            }
        }
        
        private void UpdateStops(List<TransportStop> stops)
        {
            if (_map == null)
                return;
                
            // Stops are already added through the UpdateRoutes method
            // This method could be extended to handle stops that aren't associated with any routes
        }
        
        private List<Location> EnhanceRoutePath(List<Location> stops)
        {
            if (stops.Count < 2)
                return stops;
                
            var random = new Random();
            var enhancedPath = new List<Location>();
            
            // For each pair of stops, add some intermediate points with slight randomness
            for (int i = 0; i < stops.Count - 1; i++)
            {                
                var start = stops[i];
                var end = stops[i + 1];
                
                enhancedPath.Add(start);
                
                // Add 1-3 intermediate points
                int points = random.Next(1, 4);
                
                for (int j = 1; j <= points; j++)
                {
                    // Linear interpolation with some randomness
                    double progress = (double)j / (points + 1);
                    
                    double lat = start.Latitude + (end.Latitude - start.Latitude) * progress;
                    double lon = start.Longitude + (end.Longitude - start.Longitude) * progress;
                    
                    // Add some randomness to make it look like a real route
                    lat += (random.NextDouble() - 0.5) * 0.002;
                    lon += (random.NextDouble() - 0.5) * 0.002;
                    
                    enhancedPath.Add(new Location(lat, lon));
                }
            }
            
            // Add the final stop
            enhancedPath.Add(stops.Last());
            
            return enhancedPath;
        }
        
        private void OnToggleRoutes()
        {
            ShowRoutes = !ShowRoutes;
            
            if (_map != null && _routeManager != null)
            {
                if (ShowRoutes)
                {
                    // Refresh the routes
                    var routes = GenerateMockRoutes();
                    var stops = GenerateMockStops(routes);
                    UpdateRoutes(routes, stops);
                }
                else
                {
                    // Clear all routes
                    _routeManager.ClearAll();
                }
            }
        }
        
        private void OnToggleClustering()
        {
            EnableClustering = !EnableClustering;
            
            // Refresh the map to apply or remove clustering
            RefreshCommand.Execute(null);
        }
        
        private void OnToggleStops()
        {
            ShowStops = !ShowStops;
            
            if (_map != null && _routeManager != null)
            {
                // For each route, toggle the visibility of its stops
                foreach (var route in Routes)
                {
                    _routeManager.ToggleRouteVisibility(route.Id, route.IsVisible, ShowStops);
                }
            }
        }
        
        private void OnRouteSelected(string routeId)
        {
            if (string.IsNullOrEmpty(routeId))
                return;
                
            // If selecting the already selected route, deselect it
            if (routeId == SelectedRouteId)
            {
                SelectedRouteId = null;
                
                if (_routeManager != null)
                {
                    _routeManager.HighlightRoute(routeId, false);
                }
                
                return;
            }
            
            // Otherwise, select the new route and highlight it
            SelectedRouteId = routeId;
            
            if (_routeManager != null)
            {
                // Unhighlight any previously selected route
                foreach (var route in Routes)
                {
                    if (route.Id != routeId && route.IsSelected)
                    {
                        _routeManager.HighlightRoute(route.Id, false);
                        route.IsSelected = false;
                    }
                }
                
                // Highlight the selected route
                _routeManager.HighlightRoute(routeId, true);
                
                // Update the IsSelected property on the route
                var selectedRoute = Routes.FirstOrDefault(r => r.Id == routeId);
                if (selectedRoute != null)
                {
                    selectedRoute.IsSelected = true;
                }
            }
        }
        
        /// <summary>
        /// Toggles the heatmap overlay visibility
        /// </summary>
        private void OnToggleHeatmap()
        {
            ShowHeatmap = !ShowHeatmap;
            
            if (_heatmapManager != null)
            {
                _heatmapManager.IsVisible = ShowHeatmap;
                
                // Update the heatmap data if showing
                if (ShowHeatmap)
                {
                    var vehicles = GenerateMockVehicleData(20);
                    var routes = Routes.ToList();
                    var stops = Stops.ToList();
                    
                    Task.Run(() => UpdateHeatmapAsync(vehicles, stops));
                }
            }
        }
        
        /// <summary>
        /// Cancels any ongoing batch processing operations
        /// </summary>
        private void OnCancelProcessing()
        {
            _processingCts?.Cancel();
            _processingCts?.Dispose();
            _processingCts = null;
            
            if (_heatmapManager != null)
            {
                _heatmapManager.CancelUpdates();
            }
        }
        
        /// <summary>
        /// Updates the heatmap with vehicle and stop data
        /// </summary>
        private async Task UpdateHeatmapAsync(List<TransportVehicle> vehicles, List<TransportStop> stops)
        {
            if (_heatmapManager == null || !ShowHeatmap)
                return;
                
            try
            {
                // Use the heatmap manager to update with combined data
                await _heatmapManager.UpdateFromCombinedDataAsync(
                    vehicles,
                    stops,
                    0.008,
                    _processingCts?.Token ?? CancellationToken.None
                );
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("Heatmap update was cancelled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating heatmap: {ex.Message}");
            }
        }
    }
}
