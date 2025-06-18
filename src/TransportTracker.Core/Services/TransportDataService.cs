using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TransportTracker.Core.Collections;
using TransportTracker.Core.Models;
using TransportTracker.Core.Services.Api;

namespace TransportTracker.Core.Services
{
    /// <summary>
    /// Service that manages transport data by integrating the API client with thread-safe collections
    /// to provide real-time data access for the UI
    /// </summary>
    public class TransportDataService : IDisposable
    {
        private readonly ITransportApiClient _apiClient;
        private readonly ILogger<TransportDataService> _logger;
        private readonly ThreadSafeDictionary<string, Route> _routes = new();
        private readonly ThreadSafeDictionary<string, Stop> _stops = new();
        private readonly ThreadSafeDictionary<string, Vehicle> _vehicles = new();
        private readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _disposeCts = new CancellationTokenSource();
        private Timer _vehicleUpdateTimer;
        private Timer _scheduleUpdateTimer;
        private bool _isInitialized;
        
        /// <summary>
        /// Event triggered when routes are refreshed
        /// </summary>
        public event EventHandler<IEnumerable<Route>> RoutesRefreshed;
        
        /// <summary>
        /// Event triggered when stops are refreshed
        /// </summary>
        public event EventHandler<IEnumerable<Stop>> StopsRefreshed;
        
        /// <summary>
        /// Event triggered when vehicles are refreshed
        /// </summary>
        public event EventHandler<IEnumerable<Vehicle>> VehiclesRefreshed;
        
        /// <summary>
        /// Gets the thread-safe dictionary of routes
        /// </summary>
        public IReadOnlyThreadSafeDictionary<string, Route> Routes => _routes;
        
        /// <summary>
        /// Gets the thread-safe dictionary of stops
        /// </summary>
        public IReadOnlyThreadSafeDictionary<string, Stop> Stops => _stops;
        
        /// <summary>
        /// Gets the thread-safe dictionary of vehicles
        /// </summary>
        public IReadOnlyThreadSafeDictionary<string, Vehicle> Vehicles => _vehicles;
        
        /// <summary>
        /// Gets or sets the API connection status
        /// </summary>
        public ApiConnectionStatus ConnectionStatus { get; private set; }
        
        /// <summary>
        /// Creates a new transport data service
        /// </summary>
        public TransportDataService(ITransportApiClient apiClient, ILogger<TransportDataService> logger)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Subscribe to API connection status changes
            _apiClient.ConnectionStatusChanged += OnApiConnectionStatusChanged;
            
            // Initialize update timers
            _vehicleUpdateTimer = new Timer(UpdateVehiclesCallback, null, Timeout.Infinite, Timeout.Infinite);
            _scheduleUpdateTimer = new Timer(UpdateSchedulesCallback, null, Timeout.Infinite, Timeout.Infinite);
            
            _logger.LogInformation("Transport data service initialized");
        }
        
        /// <summary>
        /// Handles API connection status changes
        /// </summary>
        private void OnApiConnectionStatusChanged(object sender, ApiConnectionStatus status)
        {
            ConnectionStatus = status;
            _logger.LogInformation("API connection status changed to {Status}", status);
            
            // If we're connected, start the update timers
            if (status == ApiConnectionStatus.Connected)
            {
                StartUpdateTimers();
            }
            else
            {
                StopUpdateTimers();
            }
        }
        
        /// <summary>
        /// Initializes the data service by loading initial data from the API
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
                return;
                
            await _refreshLock.WaitAsync(cancellationToken);
            try
            {
                // Double-check after acquiring lock
                if (_isInitialized)
                    return;
                
                _logger.LogInformation("Initializing transport data service");
                
                // Connect to the API with a test API key (for simplicity)
                // In a real app, you would get this from secure storage or user input
                var authenticated = await _apiClient.AuthenticateAsync("test-api-key", cancellationToken);
                
                if (!authenticated)
                {
                    _logger.LogWarning("Failed to authenticate with API");
                    return;
                }
                
                // Load initial data
                await Task.WhenAll(
                    RefreshRoutesAsync(cancellationToken),
                    RefreshStopsAsync(cancellationToken),
                    RefreshVehiclesAsync(cancellationToken)
                );
                
                _isInitialized = true;
                _logger.LogInformation("Transport data service initialized with {RouteCount} routes, {StopCount} stops, and {VehicleCount} vehicles", 
                    _routes.Count, _stops.Count, _vehicles.Count);
                
                // Start regular updates
                StartUpdateTimers();
            }
            finally
            {
                _refreshLock.Release();
            }
        }
        
        /// <summary>
        /// Starts the update timers for regular data refreshes
        /// </summary>
        private void StartUpdateTimers()
        {
            // Update vehicles every 5 seconds
            _vehicleUpdateTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(5));
            
            // Update schedules every 30 seconds
            _scheduleUpdateTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(30));
            
            _logger.LogDebug("Started data update timers");
        }
        
        /// <summary>
        /// Stops the update timers
        /// </summary>
        private void StopUpdateTimers()
        {
            _vehicleUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _scheduleUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            _logger.LogDebug("Stopped data update timers");
        }
        
        /// <summary>
        /// Timer callback to update vehicles
        /// </summary>
        private async void UpdateVehiclesCallback(object state)
        {
            try
            {
                await RefreshVehiclesAsync(_disposeCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing vehicles");
            }
        }
        
        /// <summary>
        /// Timer callback to update schedules
        /// </summary>
        private async void UpdateSchedulesCallback(object state)
        {
            try
            {
                // For each route, refresh its schedules
                foreach (var routeId in _routes.Keys)
                {
                    if (_disposeCts.IsCancellationRequested)
                        return;
                        
                    await RefreshSchedulesForRouteAsync(routeId, _disposeCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing schedules");
            }
        }
        
        /// <summary>
        /// Refreshes route data from the API
        /// </summary>
        public async Task RefreshRoutesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Refreshing routes from API");
                
                var routes = await _apiClient.GetRoutesAsync(true, cancellationToken);
                
                if (routes == null || !routes.Any())
                {
                    _logger.LogWarning("No routes returned from API");
                    return;
                }
                
                // Update thread-safe dictionary with new route data
                _routes.UpdateAll(routes.ToDictionary(r => r.Id));
                
                // Notify subscribers
                RoutesRefreshed?.Invoke(this, routes);
                
                _logger.LogInformation("Refreshed {Count} routes from API", routes.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing routes from API");
                throw;
            }
        }
        
        /// <summary>
        /// Refreshes stop data from the API
        /// </summary>
        public async Task RefreshStopsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Refreshing stops from API");
                
                var stops = await _apiClient.GetStopsAsync(true, cancellationToken);
                
                if (stops == null || !stops.Any())
                {
                    _logger.LogWarning("No stops returned from API");
                    return;
                }
                
                // Update thread-safe dictionary with new stop data
                _stops.UpdateAll(stops.ToDictionary(s => s.Id));
                
                // Notify subscribers
                StopsRefreshed?.Invoke(this, stops);
                
                _logger.LogInformation("Refreshed {Count} stops from API", stops.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing stops from API");
                throw;
            }
        }
        
        /// <summary>
        /// Refreshes vehicle data from the API
        /// </summary>
        public async Task RefreshVehiclesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Refreshing vehicles from API");
                
                var vehicles = await _apiClient.GetVehiclesAsync(true, cancellationToken);
                
                if (vehicles == null || !vehicles.Any())
                {
                    _logger.LogWarning("No vehicles returned from API");
                    return;
                }
                
                // Update thread-safe dictionary with new vehicle data
                _vehicles.UpdateAll(vehicles.ToDictionary(v => v.Id));
                
                // Notify subscribers
                VehiclesRefreshed?.Invoke(this, vehicles);
                
                _logger.LogInformation("Refreshed {Count} vehicles from API", vehicles.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing vehicles from API");
                throw;
            }
        }
        
        /// <summary>
        /// Refreshes schedules for a specific route
        /// </summary>
        public async Task RefreshSchedulesForRouteAsync(string routeId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(routeId))
            {
                throw new ArgumentNullException(nameof(routeId));
            }
            
            try
            {
                _logger.LogDebug("Refreshing schedules for route {RouteId} from API", routeId);
                
                // Get schedules for the next 24 hours
                var startTime = DateTime.Now;
                var endTime = startTime.AddHours(24);
                
                var schedules = await _apiClient.GetSchedulesForRouteAsync(
                    routeId, startTime, endTime, true, cancellationToken);
                
                if (schedules == null || !schedules.Any())
                {
                    _logger.LogWarning("No schedules returned for route {RouteId} from API", routeId);
                    return;
                }
                
                // Update the route's schedule collection
                if (_routes.TryGetValue(routeId, out var route))
                {
                    route.Schedules = schedules.ToList();
                    _routes.NotifyItemUpdated(routeId, route);
                    
                    _logger.LogInformation("Updated {Count} schedules for route {RouteId}", 
                        schedules.Count(), routeId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing schedules for route {RouteId} from API", routeId);
                throw;
            }
        }
        
        /// <summary>
        /// Gets API usage statistics
        /// </summary>
        public ApiUsageStatistics GetApiUsageStatistics() => _apiClient.GetApiUsageStatistics();
        
        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            // Cancel any ongoing operations
            _disposeCts.Cancel();
            
            // Dispose timers
            _vehicleUpdateTimer?.Dispose();
            _scheduleUpdateTimer?.Dispose();
            
            // Dispose semaphore
            _refreshLock?.Dispose();
            
            // Dispose cancellation token source
            _disposeCts.Dispose();
            
            // Unsubscribe from events
            if (_apiClient != null)
            {
                _apiClient.ConnectionStatusChanged -= OnApiConnectionStatusChanged;
            }
            
            _logger.LogInformation("Transport data service disposed");
        }
    }
}
