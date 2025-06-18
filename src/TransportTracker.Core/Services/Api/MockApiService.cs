using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TransportTracker.Core.Collections;
using TransportTracker.Core.Models;
using TransportTracker.Core.Services.Mock;

namespace TransportTracker.Core.Services.Api
{
    /// <summary>
    /// Mock implementation of the Transport API client for development and testing
    /// Uses thread-safe collections to simulate real API behavior with artificial delays
    /// </summary>
    public class MockApiService : ITransportApiClient
    {
        private readonly ILogger<MockApiService> _logger;
        private readonly ThreadSafeDictionary<string, Route> _routes = new();
        private readonly ThreadSafeDictionary<string, Stop> _stops = new();
        private readonly ThreadSafeDictionary<string, Vehicle> _vehicles = new();
        private readonly ThreadSafeDictionary<string, List<Schedule>> _schedules = new();
        private readonly MockDataGenerator _dataGenerator;
        private readonly Random _random = new Random();
        private readonly SemaphoreSlim _dataLock = new SemaphoreSlim(1, 1);
        private bool _dataInitialized;
        private string _apiKey;
        private ApiConnectionStatus _connectionStatus = ApiConnectionStatus.Disconnected;
        private readonly ApiUsageStatistics _apiUsageStatistics = new();
        
        /// <summary>
        /// Gets the API connection status
        /// </summary>
        public ApiConnectionStatus ConnectionStatus
        {
            get => _connectionStatus;
            private set
            {
                if (_connectionStatus != value)
                {
                    _connectionStatus = value;
                    ConnectionStatusChanged?.Invoke(this, _connectionStatus);
                }
            }
        }
        
        /// <summary>
        /// Event triggered when connection status changes
        /// </summary>
        public event EventHandler<ApiConnectionStatus> ConnectionStatusChanged;
        
        /// <summary>
        /// Creates a new mock API service
        /// </summary>
        /// <param name="logger">Logger for diagnostics</param>
        public MockApiService(ILogger<MockApiService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataGenerator = new MockDataGenerator(logger);
            _logger.LogInformation("Mock API service initialized");
        }
        
        /// <summary>
        /// Initializes the mock data if not already done
        /// </summary>
        private async Task EnsureDataInitializedAsync(CancellationToken cancellationToken = default)
        {
            if (_dataInitialized)
                return;
                
            await _dataLock.WaitAsync(cancellationToken);
            try
            {
                // Double-check after acquiring lock
                if (_dataInitialized)
                    return;
                    
                _logger.LogInformation("Initializing mock data");
                
                // Generate mock data
                var routes = _dataGenerator.GenerateRoutes(20);
                var stops = _dataGenerator.GenerateStops(100);
                var vehicles = _dataGenerator.GenerateVehicles(50);
                var schedules = _dataGenerator.GenerateSchedules(routes, vehicles);
                
                // Store in thread-safe dictionaries
                foreach (var route in routes)
                {
                    _routes[route.Id] = route;
                }
                
                foreach (var stop in stops)
                {
                    _stops[stop.Id] = stop;
                }
                
                foreach (var vehicle in vehicles)
                {
                    _vehicles[vehicle.Id] = vehicle;
                }
                
                // Group schedules by route
                foreach (var schedule in schedules)
                {
                    if (!_schedules.ContainsKey(schedule.RouteId))
                    {
                        _schedules[schedule.RouteId] = new List<Schedule>();
                    }
                    
                    _schedules[schedule.RouteId].Add(schedule);
                }
                
                _dataInitialized = true;
                _logger.LogInformation("Mock data initialized with {RouteCount} routes, {StopCount} stops, " +
                                     "{VehicleCount} vehicles, and {ScheduleCount} schedules",
                                     _routes.Count, _stops.Count, _vehicles.Count, schedules.Count());
            }
            finally
            {
                _dataLock.Release();
            }
        }
        
        /// <summary>
        /// Simulates a delay for API operations to mimic real network latency
        /// </summary>
        private async Task SimulateNetworkDelayAsync(CancellationToken cancellationToken)
        {
            // Simulate random network latency between 100-300ms
            var delay = _random.Next(100, 300);
            await Task.Delay(delay, cancellationToken);
            
            // Update API usage statistics
            _apiUsageStatistics.TotalApiCalls++;
            _apiUsageStatistics.CurrentPeriodCalls++;
            _apiUsageStatistics.SuccessfulCalls++;
            
            // Update average response time (simple moving average)
            if (_apiUsageStatistics.AverageResponseTimeMs == 0)
            {
                _apiUsageStatistics.AverageResponseTimeMs = delay;
            }
            else
            {
                _apiUsageStatistics.AverageResponseTimeMs = 
                    (_apiUsageStatistics.AverageResponseTimeMs * 0.7) + (delay * 0.3);
            }
        }
        
        /// <summary>
        /// Gets all routes from the API
        /// </summary>
        public async Task<IEnumerable<Route>> GetRoutesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            // Check authentication first
            if (_connectionStatus != ApiConnectionStatus.Connected)
            {
                _logger.LogWarning("Cannot get routes: Not connected to API");
                return Array.Empty<Route>();
            }
            
            await EnsureDataInitializedAsync(cancellationToken);
            await SimulateNetworkDelayAsync(cancellationToken);
            
            return _routes.Values.ToList();
        }
        
        /// <summary>
        /// Gets a specific route by ID
        /// </summary>
        public async Task<Route> GetRouteAsync(string routeId, bool includeStops = false, bool includeSchedules = false, 
            bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(routeId))
            {
                throw new ArgumentNullException(nameof(routeId));
            }
            
            // Check authentication first
            if (_connectionStatus != ApiConnectionStatus.Connected)
            {
                _logger.LogWarning("Cannot get route {RouteId}: Not connected to API", routeId);
                return null;
            }
            
            await EnsureDataInitializedAsync(cancellationToken);
            await SimulateNetworkDelayAsync(cancellationToken);
            
            if (_routes.TryGetValue(routeId, out var route))
            {
                // Clone the route to avoid modification issues
                var result = route.Clone();
                
                // Add related data if requested
                if (includeStops)
                {
                    // Simulate including stop details
                    foreach (var routeStop in result.Stops)
                    {
                        if (_stops.TryGetValue(routeStop.StopId, out var stop))
                        {
                            routeStop.StopDetails = stop.Clone();
                        }
                    }
                }
                
                if (includeSchedules)
                {
                    if (_schedules.TryGetValue(routeId, out var scheduleList))
                    {
                        result.Schedules = scheduleList.Select(s => s.Clone()).ToList();
                    }
                }
                
                return result;
            }
            
            return null;
        }
        
        /// <summary>
        /// Gets all stops from the API
        /// </summary>
        public async Task<IEnumerable<Stop>> GetStopsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            // Check authentication first
            if (_connectionStatus != ApiConnectionStatus.Connected)
            {
                _logger.LogWarning("Cannot get stops: Not connected to API");
                return Array.Empty<Stop>();
            }
            
            await EnsureDataInitializedAsync(cancellationToken);
            await SimulateNetworkDelayAsync(cancellationToken);
            
            return _stops.Values.Select(s => s.Clone()).ToList();
        }
        
        /// <summary>
        /// Gets a specific stop by ID
        /// </summary>
        public async Task<Stop> GetStopAsync(string stopId, bool includeRoutes = false,
            bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(stopId))
            {
                throw new ArgumentNullException(nameof(stopId));
            }
            
            // Check authentication first
            if (_connectionStatus != ApiConnectionStatus.Connected)
            {
                _logger.LogWarning("Cannot get stop {StopId}: Not connected to API", stopId);
                return null;
            }
            
            await EnsureDataInitializedAsync(cancellationToken);
            await SimulateNetworkDelayAsync(cancellationToken);
            
            if (_stops.TryGetValue(stopId, out var stop))
            {
                var result = stop.Clone();
                
                if (includeRoutes)
                {
                    // Find routes that include this stop
                    result.Routes = _routes.Values
                        .Where(r => r.Stops.Any(rs => rs.StopId == stopId))
                        .Select(r => r.Clone())
                        .ToList();
                }
                
                return result;
            }
            
            return null;
        }
        
        /// <summary>
        /// Gets all vehicles from the API
        /// </summary>
        public async Task<IEnumerable<Vehicle>> GetVehiclesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            // Check authentication first
            if (_connectionStatus != ApiConnectionStatus.Connected)
            {
                _logger.LogWarning("Cannot get vehicles: Not connected to API");
                return Array.Empty<Vehicle>();
            }
            
            await EnsureDataInitializedAsync(cancellationToken);
            await SimulateNetworkDelayAsync(cancellationToken);
            
            // For vehicles, we'll update their positions each time to simulate movement
            _dataGenerator.UpdateVehiclePositions(_vehicles.Values);
            
            return _vehicles.Values.Select(v => v.Clone()).ToList();
        }
        
        /// <summary>
        /// Gets all vehicles for a specific route
        /// </summary>
        public async Task<IEnumerable<Vehicle>> GetVehiclesByRouteAsync(string routeId, bool forceRefresh = false, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(routeId))
            {
                throw new ArgumentNullException(nameof(routeId));
            }
            
            // Check authentication first
            if (_connectionStatus != ApiConnectionStatus.Connected)
            {
                _logger.LogWarning("Cannot get vehicles for route {RouteId}: Not connected to API", routeId);
                return Array.Empty<Vehicle>();
            }
            
            await EnsureDataInitializedAsync(cancellationToken);
            await SimulateNetworkDelayAsync(cancellationToken);
            
            // Update vehicle positions to simulate movement
            _dataGenerator.UpdateVehiclePositions(_vehicles.Values);
            
            // Get vehicles assigned to this route
            return _vehicles.Values
                .Where(v => v.RouteId == routeId)
                .Select(v => v.Clone())
                .ToList();
        }
        
        /// <summary>
        /// Gets a specific vehicle by ID
        /// </summary>
        public async Task<Vehicle> GetVehicleAsync(string vehicleId, bool includeRoute = false,
            bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(vehicleId))
            {
                throw new ArgumentNullException(nameof(vehicleId));
            }
            
            // Check authentication first
            if (_connectionStatus != ApiConnectionStatus.Connected)
            {
                _logger.LogWarning("Cannot get vehicle {VehicleId}: Not connected to API", vehicleId);
                return null;
            }
            
            await EnsureDataInitializedAsync(cancellationToken);
            await SimulateNetworkDelayAsync(cancellationToken);
            
            if (_vehicles.TryGetValue(vehicleId, out var vehicle))
            {
                // Update position before returning
                _dataGenerator.UpdateVehiclePosition(vehicle);
                var result = vehicle.Clone();
                
                if (includeRoute && !string.IsNullOrEmpty(result.RouteId) && 
                    _routes.TryGetValue(result.RouteId, out var route))
                {
                    result.Route = route.Clone();
                }
                
                return result;
            }
            
            return null;
        }

        /// <summary>
        /// Gets schedules for a route
        /// </summary>
        public async Task<IEnumerable<Schedule>> GetSchedulesForRouteAsync(string routeId, 
            DateTime? startTime = null, DateTime? endTime = null,
            bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(routeId))
            {
                throw new ArgumentNullException(nameof(routeId));
            }
            
            // Check authentication first
            if (_connectionStatus != ApiConnectionStatus.Connected)
            {
                _logger.LogWarning("Cannot get schedules for route {RouteId}: Not connected to API", routeId);
                return Array.Empty<Schedule>();
            }
            
            await EnsureDataInitializedAsync(cancellationToken);
            await SimulateNetworkDelayAsync(cancellationToken);
            
            if (!_schedules.TryGetValue(routeId, out var schedules))
            {
                return Array.Empty<Schedule>();
            }
            
            var result = schedules.AsEnumerable();
            
            // Apply time filters if specified
            if (startTime.HasValue)
            {
                result = result.Where(s => s.DepartureTime >= startTime.Value);
            }
            
            if (endTime.HasValue)
            {
                result = result.Where(s => s.DepartureTime <= endTime.Value);
            }
            
            return result.Select(s => s.Clone()).ToList();
        }
        
        /// <summary>
        /// Gets schedules for a stop
        /// </summary>
        public async Task<IEnumerable<Schedule>> GetSchedulesForStopAsync(string stopId, string routeId = null,
            DateTime? startTime = null, DateTime? endTime = null,
            bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(stopId))
            {
                throw new ArgumentNullException(nameof(stopId));
            }
            
            // Check authentication first
            if (_connectionStatus != ApiConnectionStatus.Connected)
            {
                _logger.LogWarning("Cannot get schedules for stop {StopId}: Not connected to API", stopId);
                return Array.Empty<Schedule>();
            }
            
            await EnsureDataInitializedAsync(cancellationToken);
            await SimulateNetworkDelayAsync(cancellationToken);
            
            // Find all schedules that include this stop
            // This is a bit inefficient in a real-world scenario but works for our mock service
            var allSchedules = new List<Schedule>();
            
            foreach (var scheduleList in _schedules.Values)
            {
                foreach (var schedule in scheduleList)
                {
                    // Check if this schedule has the stop in its route
                    if (_routes.TryGetValue(schedule.RouteId, out var route) && 
                        route.Stops.Any(s => s.StopId == stopId))
                    {
                        allSchedules.Add(schedule);
                    }
                }
            }
            
            var result = allSchedules.AsEnumerable();
            
            // Apply route filter if specified
            if (!string.IsNullOrEmpty(routeId))
            {
                result = result.Where(s => s.RouteId == routeId);
            }
            
            // Apply time filters if specified
            if (startTime.HasValue)
            {
                result = result.Where(s => s.DepartureTime >= startTime.Value);
            }
            
            if (endTime.HasValue)
            {
                result = result.Where(s => s.DepartureTime <= endTime.Value);
            }
            
            return result.Select(s => s.Clone()).ToList();
        }
        
        /// <summary>
        /// Gets API usage statistics
        /// </summary>
        public ApiUsageStatistics GetApiUsageStatistics() => _apiUsageStatistics;
        
        /// <summary>
        /// Authenticates with the API using the provided key
        /// </summary>
        public async Task<bool> AuthenticateAsync(string apiKey, CancellationToken cancellationToken = default)
        {
            // Simulate network delay
            await Task.Delay(_random.Next(200, 500), cancellationToken);
            
            // For mock service, accept any non-empty API key
            if (!string.IsNullOrEmpty(apiKey))
            {
                _apiKey = apiKey;
                ConnectionStatus = ApiConnectionStatus.Connected;
                _logger.LogInformation("Successfully authenticated with API");
                return true;
            }
            
            ConnectionStatus = ApiConnectionStatus.AuthenticationFailed;
            _logger.LogWarning("Authentication failed: Invalid API key");
            return false;
        }
        
        /// <summary>
        /// Disconnects from the API
        /// </summary>
        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            _apiKey = null;
            ConnectionStatus = ApiConnectionStatus.Disconnected;
            _logger.LogInformation("Disconnected from API");
            return Task.CompletedTask;
        }
    }
}
