using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using ThreadingCS.Models;
using ThreadingCS.Services;

namespace ThreadingCS.ViewModels
{
    public class VehicleMapInfo
    {
        public string VehicleId { get; set; }
        public string RouteName { get; set; }
        public string LastUpdatedText { get; set; }
        public string DirectionText { get; set; }
    }

    // Custom vehicle position data for the simulated map
    public class VehiclePosition
    {
        public string Id { get; set; }
        public float X { get; set; } // Position on canvas
        public float Y { get; set; } // Position on canvas
        public bool IsHighlighted { get; set; }
        public string RouteId { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class MapViewModel : BaseViewModel
    {
        private readonly TransportApiService _apiService;
        private readonly DataProcessingService _processingService;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly List<VehiclePosition> _vehiclePositions = new();
        private readonly Dictionary<string, VehiclePosition> _vehicleMap = new();
        
        private bool _isLoading;
        private bool _isVehicleSelected;
        private double _mapLoadingProgress;
        private string _statusMessage;
        private VehicleMapInfo _selectedVehicle;
        private int _activeVehiclesCount;
        private double _updatesPerSecond;
        private int _activeThreadsCount;
        private bool _isLiveUpdateRunning;
        private Stopwatch _performanceStopwatch = new Stopwatch();
        private int _updateCounter;

        public ObservableCollection<TransportRoute> ActiveRoutes { get; set; } = new ObservableCollection<TransportRoute>();
        
        public bool IsLiveUpdateRunning => _isLiveUpdateRunning;

        public int ActiveVehiclesCount
        {
            get => _activeVehiclesCount;
            set => SetProperty(ref _activeVehiclesCount, value);
        }

        public double UpdatesPerSecond
        {
            get => _updatesPerSecond;
            set => SetProperty(ref _updatesPerSecond, value);
        }

        public int ActiveThreadsCount
        {
            get => _activeThreadsCount;
            set => SetProperty(ref _activeThreadsCount, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public double MapLoadingProgress
        {
            get => _mapLoadingProgress;
            set => SetProperty(ref _mapLoadingProgress, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsVehicleSelected
        {
            get => _isVehicleSelected;
            set => SetProperty(ref _isVehicleSelected, value);
        }

        public VehicleMapInfo SelectedVehicle
        {
            get => _selectedVehicle;
            set
            {
                if (SetProperty(ref _selectedVehicle, value))
                {
                    IsVehicleSelected = value != null;
                }
            }
        }

        public Command ViewRouteDetailsCommand { get; }

        public MapViewModel()
        {
            _apiService = new TransportApiService();
            _processingService = new DataProcessingService();
            ViewRouteDetailsCommand = new Command(ExecuteViewRouteDetails);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void InitializeSimulatedMap()
        {
            IsLoading = true;
            StatusMessage = "Loading simulated transport data...";
            MapLoadingProgress = 0.1;
        }

        public void StartLiveUpdates()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
            
            _cancellationTokenSource = new CancellationTokenSource();
            _isLiveUpdateRunning = true;
            _performanceStopwatch.Start();
            _updateCounter = 0;
            
            Task.Run(async () => await GenerateAndUpdateVehiclesAsync(_cancellationTokenSource.Token));
        }

        public void StopLiveUpdates()
        {
            _isLiveUpdateRunning = false;
            _performanceStopwatch.Stop();
            _cancellationTokenSource?.Cancel();
        }
        
        public List<VehiclePosition> GetVehiclePositions()
        {
            return _vehiclePositions;
        }
        
        private async Task GenerateSimulatedRoutesAsync()
        {
            try
            {
                // Clear active routes on UI thread
                MainThread.BeginInvokeOnMainThread(() => { ActiveRoutes.Clear(); });
                
                // Use consistent simulated data instead of API calls for reliability
                StatusMessage = "Generating simulated transport data...";
                
                // Generate a consistent set of routes and vehicles using PLINQ
                await Task.Run(() =>
                {
                    // Always generate exactly 3 routes with consistent vehicles
                    var routes = new List<TransportRoute>();
                    
                    // Define fixed route colors for consistency
                    var routeColors = new[] { "#FF0000", "#0000FF", "#00FF00" };
                    var routeNames = new[] { "Red Line", "Blue Express", "Green Route" };
                    
                    // Create 3 routes with consistent properties
                    for (int i = 0; i < 3; i++)
                    {
                        var route = new TransportRoute
                        {
                            RouteId = $"R{i+1}",
                            RouteName = routeNames[i],
                            Color = routeColors[i],
                            Vehicles = new List<Vehicle>()
                        };
                        
                        // Generate exactly 4 vehicles for each route using PLINQ
                        var vehiclesPerRoute = 4;
                        var vehicles = Enumerable.Range(1, vehiclesPerRoute)
                            .AsParallel()
                            .WithDegreeOfParallelism(Environment.ProcessorCount)
                            .Select(j =>
                            {
                                return new Vehicle
                                {
                                    VehicleId = $"V{i+1}-{j}",
                                    RouteId = route.RouteId,
                                    // Use fixed starting positions that are well-distributed
                                    Latitude = 51.5 + ((double)i / 50.0),
                                    Longitude = -0.1 + ((double)j / 50.0),
                                    Bearing = (i * 90 + j * 45) % 360,
                                    LastUpdated = DateTime.Now
                                };
                            })
                            .ToList();
                        
                        route.Vehicles = vehicles;
                        routes.Add(route);
                        
                        // Add to observable collection on UI thread
                        MainThread.BeginInvokeOnMainThread(() => { ActiveRoutes.Add(route); });
                    }
                    
                    return routes;
                });
                
                // Always show consistent status message
                StatusMessage = "Loaded 3 routes with 12 vehicles";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating simulated routes: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
        }
        
        private async Task GenerateAndUpdateVehiclesAsync(CancellationToken token)
        {
            // Generate some simulated routes
            await GenerateSimulatedRoutesAsync();
            
            MapLoadingProgress = 0.5;

            try
            {
                // Now use the generated routes to create vehicle positions
                MapLoadingProgress = 0.8;

                // Create simulated vehicle positions
                CreateSimulatedVehiclePositions(ActiveRoutes.ToList());
                MapLoadingProgress = 1.0;

                IsLoading = false;
                StatusMessage = $"Loaded {ActiveRoutes.Count} routes with {_vehiclePositions.Count} vehicles";
                ActiveVehiclesCount = _vehiclePositions.Count;
                ActiveThreadsCount = Environment.ProcessorCount;
                
                // Start the vehicle update loop
                await StartVehicleUpdateLoopAsync(token);
            }
            catch (Exception ex)
            {
                IsLoading = false;
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                MapLoadingProgress = 1.0;
                IsLoading = false;
            }
        }
        
        private void CreateSimulatedVehiclePositions(List<TransportRoute> routes)
        {
            _vehiclePositions.Clear();
            _vehicleMap.Clear();
            
            // Create a vehicle position for each vehicle in each route
            var random = new Random(42); // Fixed seed for consistent results
            var canvasWidth = 1000f; // Simulated canvas width
            var canvasHeight = 600f; // Simulated canvas height
            
            foreach (var route in routes)
            {
                foreach (var vehicle in route.Vehicles)
                {
                    // Create a position on our simulated canvas
                    var position = new VehiclePosition
                    {
                        Id = vehicle.VehicleId,
                        X = random.Next(50, (int)canvasWidth - 50),
                        Y = random.Next(50, (int)canvasHeight - 50),
                        IsHighlighted = false,
                        RouteId = route.RouteId,
                        LastUpdated = DateTime.Now
                    };
                    
                    _vehiclePositions.Add(position);
                    _vehicleMap[vehicle.VehicleId] = position;
                }
            }
        }
        
        private async Task StartVehicleUpdateLoopAsync(CancellationToken token)
        {
            var random = new Random();
            
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Every 5 seconds, make multiple parallel API calls to update vehicle positions
                    if (_updateCounter % 100 == 0) // Approximately every 5 seconds (50ms * 100)
                    {
                        try
                        {
                            // Get all active route IDs
                            var routeIds = ActiveRoutes.Select(r => r.RouteId).ToList();
                            
                            if (routeIds.Count > 0)
                            {
                                // Make multiple parallel API calls to get vehicle updates
                                StatusMessage = "Fetching real-time vehicle updates...";
                                var vehicleUpdates = await _apiService.GetVehicleUpdatesAsync(routeIds);
                                
                                // Update vehicle positions based on API response
                                int updatedVehicles = 0;
                                
                                foreach (var kvp in vehicleUpdates)
                                {
                                    var routeId = kvp.Key;
                                    var vehicles = kvp.Value;
                                    
                                    foreach (var vehicle in vehicles)
                                    {
                                        if (_vehicleMap.TryGetValue(vehicle.VehicleId, out var position))
                                        {
                                            // Update existing vehicle position
                                            position.LastUpdated = DateTime.Now;
                                            updatedVehicles++;
                                        }
                                    }
                                }
                                
                                if (updatedVehicles > 0)
                                {
                                    StatusMessage = $"Updated {updatedVehicles} vehicles from API";
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error updating vehicles from API: {ex.Message}");
                            // Continue with simulated updates on error
                        }
                    }
                    
                    // Update vehicle positions using PLINQ for parallel processing
                    _vehiclePositions.AsParallel()
                        .WithDegreeOfParallelism(Environment.ProcessorCount)
                        .ForAll(vehicle => 
                        {
                            // Move each vehicle randomly in the simulated map
                            vehicle.X += random.Next(-5, 6);
                            vehicle.Y += random.Next(-5, 6);
                            
                            // Keep within bounds
                            vehicle.X = Math.Clamp(vehicle.X, 20, 980);
                            vehicle.Y = Math.Clamp(vehicle.Y, 20, 580);
                            
                            vehicle.LastUpdated = DateTime.Now;
                        });
                    
                    // Update performance metrics
                    _updateCounter++;
                    if (_performanceStopwatch.ElapsedMilliseconds > 1000)
                    {
                        UpdatesPerSecond = (double)_updateCounter / (_performanceStopwatch.ElapsedMilliseconds / 1000.0);
                        _updateCounter = 0;
                        _performanceStopwatch.Restart();
                    }
                    
                    // Wait before next update
                    await Task.Delay(50, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, ignore
            }
        }

        public async Task StartRealTimeUpdatesAsync()
        {
            // This is now simplified to just initialize and start live updates for the custom visualization
            InitializeSimulatedMap();
            await Task.Delay(500); // Brief delay to allow UI to update
            StartLiveUpdates();
            StatusMessage = "Live vehicle updates started";
        }

        public void StopRealTimeUpdates()
        {
            StopLiveUpdates();
            StatusMessage = "Real-time updates stopped";
        }

        private async Task AddVehiclePinsAsync(List<TransportRoute> routes)
        {
            // This method is kept for backward compatibility but no longer used with our custom visualization
            // Instead, we now use the CreateSimulatedVehiclePositions method
            await Task.CompletedTask;
        }

        private async Task UpdateVehiclePositionsAsync(TransportRoute route, CancellationToken token)
        {
            if (route.Vehicles == null) return;
            var random = new Random();
            
            foreach (var vehicle in route.Vehicles)
            {
                if (token.IsCancellationRequested) return;

                // Update the vehicle data
                vehicle.Latitude += (random.NextDouble() - 0.5) * 0.0005;
                vehicle.Longitude += (random.NextDouble() - 0.5) * 0.0005;
                vehicle.Bearing = (vehicle.Bearing + random.Next(-10, 10)) % 360;
                vehicle.LastUpdated = DateTime.Now;

                // Update our custom vehicle position if it exists
                if (_vehicleMap.TryGetValue(vehicle.VehicleId, out var position))
                {
                    // Move randomly on the canvas
                    position.X += random.Next(-3, 4);
                    position.Y += random.Next(-3, 4);
                    
                    // Keep within canvas bounds
                    position.X = Math.Clamp(position.X, 20, 980);
                    position.Y = Math.Clamp(position.Y, 20, 580);
                    
                    position.LastUpdated = DateTime.Now;
                    
                    // Highlight if this is the selected vehicle
                    position.IsHighlighted = (IsVehicleSelected && SelectedVehicle?.VehicleId == vehicle.VehicleId);

                    // If this is the selected vehicle, update the info
                    if (position.IsHighlighted)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            SelectedVehicle = new VehicleMapInfo
                            {
                                VehicleId = vehicle.VehicleId,
                                RouteName = route.RouteName,
                                LastUpdatedText = $"Updated: {vehicle.LastUpdated:HH:mm:ss}",
                                DirectionText = $"{vehicle.Bearing}° ({GetDirectionFromBearing(vehicle.Bearing)})"
                            };
                        });
                    }
                }
            }
        }

        private void ExecuteViewRouteDetails()
        {
            // In a real app, this would navigate to a route details page
            StatusMessage = $"Viewing details for {SelectedVehicle.RouteName}";
        }

        private string GetDirectionFromBearing(double bearing)
        {
            var directions = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW", "N" };
            return directions[(int)Math.Round(bearing / 45)];
        }

        // PropertyChanged event and OnPropertyChanged method are now inherited from BaseViewModel
    }
}
