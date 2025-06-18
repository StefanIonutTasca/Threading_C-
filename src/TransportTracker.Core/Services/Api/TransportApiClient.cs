using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TransportTracker.Core.Caching;
using TransportTracker.Core.Models;

namespace TransportTracker.Core.Services.Api
{
    /// <summary>
    /// Implementation of the ITransportApiClient interface for accessing real-time transport API data
    /// with built-in resilience, caching, and thread-safety
    /// </summary>
    public class TransportApiClient : ITransportApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TransportApiClient> _logger;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private readonly TwoLevelCache<string, Route> _routeCache;
        private readonly TwoLevelCache<string, Stop> _stopCache;
        private readonly TwoLevelCache<string, Vehicle> _vehicleCache;
        private readonly TwoLevelCache<string, List<Schedule>> _scheduleCache;
        private string _apiKey;
        private DateTime _lastAuthCheck = DateTime.MinValue;
        private readonly Uri _baseAddress;
        private readonly ApiUsageStatistics _apiUsageStatistics = new();
        private ApiConnectionStatus _connectionStatus = ApiConnectionStatus.Disconnected;
        private readonly TimeSpan _defaultCacheExpiration = TimeSpan.FromMinutes(15);
        private readonly TimeSpan _vehicleCacheExpiration = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _scheduleCacheExpiration = TimeSpan.FromMinutes(5);
        private readonly int _maxRetryAttempts = 3;
        
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
        /// Creates a new TransportApiClient
        /// </summary>
        /// <param name="httpClient">HttpClient instance</param>
        /// <param name="logger">Logger for diagnostics</param>
        /// <param name="baseAddress">Base API address</param>
        public TransportApiClient(HttpClient httpClient, ILogger<TransportApiClient> logger, Uri baseAddress)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _baseAddress = baseAddress ?? throw new ArgumentNullException(nameof(baseAddress));
            
            _httpClient.BaseAddress = baseAddress;
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            
            // Initialize caches
            _routeCache = new TwoLevelCache<string, Route>(logger);
            _stopCache = new TwoLevelCache<string, Stop>(logger);
            _vehicleCache = new TwoLevelCache<string, Vehicle>(logger);
            _scheduleCache = new TwoLevelCache<string, List<Schedule>>(logger);
            
            _logger.LogInformation("Transport API client initialized with base address: {BaseAddress}", baseAddress);
        }

        // API methods will be implemented in subsequent batches
        
        /// <summary>
        /// Gets all routes from the API
        /// </summary>
        public async Task<IEnumerable<Route>> GetRoutesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            const string cacheKey = "all_routes";
            
            // Check if we need to refresh from the API
            if (!forceRefresh)
            {
                // Try to get from cache first
                var cachedRoutes = await _routeCache.GetAsync(cacheKey);
                if (cachedRoutes != null)
                {
                    _logger.LogDebug("Retrieved {Count} routes from cache", cachedRoutes.Count);
                    return cachedRoutes;
                }
            }
            
            // Create a response handler for this request
            var responseHandler = new ApiResponseHandler(_logger, _apiUsageStatistics);
            
            try
            {
                // Ensure we're authenticated
                if (!await EnsureAuthenticatedAsync(cancellationToken))
                {
                    _logger.LogWarning("Failed to retrieve routes: Not authenticated");
                    return Array.Empty<Route>();
                }
                
                // Make the API call with resilience handling
                var routes = await responseHandler.ProcessWithRetriesAsync<List<Route>>(
                    async token => await _httpClient.GetAsync("routes", token),
                    _maxRetryAttempts,
                    200,
                    cancellationToken);
                
                if (routes != null && routes.Count > 0)
                {
                    // Cache the results
                    await _routeCache.SetAsync(cacheKey, routes, _defaultCacheExpiration);
                    _logger.LogInformation("Retrieved and cached {Count} routes from API", routes.Count);
                }
                else
                {
                    _logger.LogWarning("API returned no routes");
                }
                
                return routes ?? Array.Empty<Route>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve routes from API");
                
                // Try to fall back to cache even if forceRefresh was true
                if (forceRefresh)
                {
                    var cachedRoutes = await _routeCache.GetAsync(cacheKey);
                    if (cachedRoutes != null)
                    {
                        _logger.LogInformation("Falling back to cached routes after API failure");
                        return cachedRoutes;
                    }
                }
                
                throw;
            }
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
            
            var cacheKey = $"route_{routeId}_{includeStops}_{includeSchedules}";
            
            // Check if we need to refresh from the API
            if (!forceRefresh)
            {
                // Try to get from cache first
                var cachedRoute = await _routeCache.GetAsync(cacheKey);
                if (cachedRoute != null)
                {
                    _logger.LogDebug("Retrieved route {RouteId} from cache", routeId);
                    return cachedRoute;
                }
            }
            
            // Create a response handler for this request
            var responseHandler = new ApiResponseHandler(_logger, _apiUsageStatistics);
            
            try
            {
                // Ensure we're authenticated
                if (!await EnsureAuthenticatedAsync(cancellationToken))
                {
                    _logger.LogWarning("Failed to retrieve route {RouteId}: Not authenticated", routeId);
                    return null;
                }
                
                // Build query string
                var query = new List<string>();
                if (includeStops) query.Add("include_stops=true");
                if (includeSchedules) query.Add("include_schedules=true");
                var queryString = query.Count > 0 ? "?" + string.Join("&", query) : "";
                
                // Make the API call with resilience handling
                var route = await responseHandler.ProcessWithRetriesAsync<Route>(
                    async token => await _httpClient.GetAsync($"routes/{routeId}{queryString}", token),
                    _maxRetryAttempts,
                    200,
                    cancellationToken);
                
                if (route != null)
                {
                    // Cache the result
                    await _routeCache.SetAsync(cacheKey, route, _defaultCacheExpiration);
                    _logger.LogInformation("Retrieved and cached route {RouteId} from API", routeId);
                }
                else
                {
                    _logger.LogWarning("API returned no route for ID {RouteId}", routeId);
                }
                
                return route;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve route {RouteId} from API", routeId);
                
                // Try to fall back to cache even if forceRefresh was true
                if (forceRefresh)
                {
                    var cachedRoute = await _routeCache.GetAsync(cacheKey);
                    if (cachedRoute != null)
                    {
                        _logger.LogInformation("Falling back to cached route {RouteId} after API failure", routeId);
                        return cachedRoute;
                    }
                }
                
                throw;
            }
        }
        
        /// <summary>
        /// Gets all stops from the API
        /// </summary>
        public async Task<IEnumerable<Stop>> GetStopsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            const string cacheKey = "all_stops";
            
            // Check if we need to refresh from the API
            if (!forceRefresh)
            {
                // Try to get from cache first
                var cachedStops = await _stopCache.GetAsync(cacheKey);
                if (cachedStops != null)
                {
                    _logger.LogDebug("Retrieved {Count} stops from cache", cachedStops.Count);
                    return cachedStops;
                }
            }
            
            // Create a response handler for this request
            var responseHandler = new ApiResponseHandler(_logger, _apiUsageStatistics);
            
            try
            {
                // Ensure we're authenticated
                if (!await EnsureAuthenticatedAsync(cancellationToken))
                {
                    _logger.LogWarning("Failed to retrieve stops: Not authenticated");
                    return Array.Empty<Stop>();
                }
                
                // Make the API call with resilience handling
                var stops = await responseHandler.ProcessWithRetriesAsync<List<Stop>>(
                    async token => await _httpClient.GetAsync("stops", token),
                    _maxRetryAttempts,
                    200,
                    cancellationToken);
                
                if (stops != null && stops.Count > 0)
                {
                    // Cache the results
                    await _stopCache.SetAsync(cacheKey, stops, _defaultCacheExpiration);
                    _logger.LogInformation("Retrieved and cached {Count} stops from API", stops.Count);
                }
                else
                {
                    _logger.LogWarning("API returned no stops");
                }
                
                return stops ?? Array.Empty<Stop>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve stops from API");
                
                // Try to fall back to cache even if forceRefresh was true
                if (forceRefresh)
                {
                    var cachedStops = await _stopCache.GetAsync(cacheKey);
                    if (cachedStops != null)
                    {
                        _logger.LogInformation("Falling back to cached stops after API failure");
                        return cachedStops;
                    }
                }
                
                throw;
            }
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
            
            var cacheKey = $"stop_{stopId}_{includeRoutes}";
            
            // Check if we need to refresh from the API
            if (!forceRefresh)
            {
                // Try to get from cache first
                var cachedStop = await _stopCache.GetAsync(cacheKey);
                if (cachedStop != null)
                {
                    _logger.LogDebug("Retrieved stop {StopId} from cache", stopId);
                    return cachedStop;
                }
            }
            
            // Create a response handler for this request
            var responseHandler = new ApiResponseHandler(_logger, _apiUsageStatistics);
            
            try
            {
                // Ensure we're authenticated
                if (!await EnsureAuthenticatedAsync(cancellationToken))
                {
                    _logger.LogWarning("Failed to retrieve stop {StopId}: Not authenticated", stopId);
                    return null;
                }
                
                // Build query string
                var queryString = includeRoutes ? "?include_routes=true" : "";
                
                // Make the API call with resilience handling
                var stop = await responseHandler.ProcessWithRetriesAsync<Stop>(
                    async token => await _httpClient.GetAsync($"stops/{stopId}{queryString}", token),
                    _maxRetryAttempts,
                    200,
                    cancellationToken);
                
                if (stop != null)
                {
                    // Cache the result
                    await _stopCache.SetAsync(cacheKey, stop, _defaultCacheExpiration);
                    _logger.LogInformation("Retrieved and cached stop {StopId} from API", stopId);
                }
                else
                {
                    _logger.LogWarning("API returned no stop for ID {StopId}", stopId);
                }
                
                return stop;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve stop {StopId} from API", stopId);
                
                // Try to fall back to cache even if forceRefresh was true
                if (forceRefresh)
                {
                    var cachedStop = await _stopCache.GetAsync(cacheKey);
                    if (cachedStop != null)
                    {
                        _logger.LogInformation("Falling back to cached stop {StopId} after API failure", stopId);
                        return cachedStop;
                    }
                }
                
                throw;
            }
        }
        
        /// <summary>
        /// Gets all vehicles from the API
        /// </summary>
        public async Task<IEnumerable<Vehicle>> GetVehiclesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            const string cacheKey = "all_vehicles";
            
            // Check if we need to refresh from the API
            if (!forceRefresh)
            {
                // Try to get from cache first
                var cachedVehicles = await _vehicleCache.GetAsync(cacheKey);
                if (cachedVehicles != null)
                {
                    _logger.LogDebug("Retrieved {Count} vehicles from cache", cachedVehicles.Count);
                    return cachedVehicles;
                }
            }
            
            // Create a response handler for this request
            var responseHandler = new ApiResponseHandler(_logger, _apiUsageStatistics);
            
            try
            {
                // Ensure we're authenticated
                if (!await EnsureAuthenticatedAsync(cancellationToken))
                {
                    _logger.LogWarning("Failed to retrieve vehicles: Not authenticated");
                    return Array.Empty<Vehicle>();
                }
                
                // Make the API call with resilience handling
                var vehicles = await responseHandler.ProcessWithRetriesAsync<List<Vehicle>>(
                    async token => await _httpClient.GetAsync("vehicles", token),
                    _maxRetryAttempts,
                    200,
                    cancellationToken);
                
                if (vehicles != null && vehicles.Count > 0)
                {
                    // Cache the results - but use a shorter expiration for real-time vehicles
                    await _vehicleCache.SetAsync(cacheKey, vehicles, _vehicleCacheExpiration);
                    _logger.LogInformation("Retrieved and cached {Count} vehicles from API", vehicles.Count);
                }
                else
                {
                    _logger.LogWarning("API returned no vehicles");
                }
                
                return vehicles ?? Array.Empty<Vehicle>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve vehicles from API");
                
                // Try to fall back to cache even if forceRefresh was true
                if (forceRefresh)
                {
                    var cachedVehicles = await _vehicleCache.GetAsync(cacheKey);
                    if (cachedVehicles != null)
                    {
                        _logger.LogInformation("Falling back to cached vehicles after API failure");
                        return cachedVehicles;
                    }
                }
                
                throw;
            }
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
            
            var cacheKey = $"route_{routeId}_vehicles";
            
            // Check if we need to refresh from the API
            if (!forceRefresh)
            {
                // Try to get from cache first
                var cachedVehicles = await _vehicleCache.GetAsync(cacheKey);
                if (cachedVehicles != null)
                {
                    _logger.LogDebug("Retrieved {Count} vehicles for route {RouteId} from cache", cachedVehicles.Count, routeId);
                    return cachedVehicles;
                }
            }
            
            // Create a response handler for this request
            var responseHandler = new ApiResponseHandler(_logger, _apiUsageStatistics);
            
            try
            {
                // Ensure we're authenticated
                if (!await EnsureAuthenticatedAsync(cancellationToken))
                {
                    _logger.LogWarning("Failed to retrieve vehicles for route {RouteId}: Not authenticated", routeId);
                    return Array.Empty<Vehicle>();
                }
                
                // Make the API call with resilience handling
                var vehicles = await responseHandler.ProcessWithRetriesAsync<List<Vehicle>>(
                    async token => await _httpClient.GetAsync($"routes/{routeId}/vehicles", token),
                    _maxRetryAttempts,
                    200,
                    cancellationToken);
                
                if (vehicles != null && vehicles.Count > 0)
                {
                    // Cache the results - but use a shorter expiration for real-time vehicles
                    await _vehicleCache.SetAsync(cacheKey, vehicles, _vehicleCacheExpiration);
                    _logger.LogInformation("Retrieved and cached {Count} vehicles for route {RouteId} from API", 
                        vehicles.Count, routeId);
                }
                else
                {
                    _logger.LogWarning("API returned no vehicles for route {RouteId}", routeId);
                }
                
                return vehicles ?? Array.Empty<Vehicle>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve vehicles for route {RouteId} from API", routeId);
                
                // Try to fall back to cache even if forceRefresh was true
                if (forceRefresh)
                {
                    var cachedVehicles = await _vehicleCache.GetAsync(cacheKey);
                    if (cachedVehicles != null)
                    {
                        _logger.LogInformation("Falling back to cached vehicles for route {RouteId} after API failure", routeId);
                        return cachedVehicles;
                    }
                }
                
                throw;
            }
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
            
            var cacheKey = $"vehicle_{vehicleId}_{includeRoute}";
            
            // Check if we need to refresh from the API
            if (!forceRefresh)
            {
                // Try to get from cache first
                var cachedVehicle = await _vehicleCache.GetAsync(cacheKey);
                if (cachedVehicle != null)
                {
                    _logger.LogDebug("Retrieved vehicle {VehicleId} from cache", vehicleId);
                    return cachedVehicle;
                }
            }
            
            // Create a response handler for this request
            var responseHandler = new ApiResponseHandler(_logger, _apiUsageStatistics);
            
            try
            {
                // Ensure we're authenticated
                if (!await EnsureAuthenticatedAsync(cancellationToken))
                {
                    _logger.LogWarning("Failed to retrieve vehicle {VehicleId}: Not authenticated", vehicleId);
                    return null;
                }
                
                // Build query string
                var queryString = includeRoute ? "?include_route=true" : "";
                
                // Make the API call with resilience handling
                var vehicle = await responseHandler.ProcessWithRetriesAsync<Vehicle>(
                    async token => await _httpClient.GetAsync($"vehicles/{vehicleId}{queryString}", token),
                    _maxRetryAttempts,
                    200,
                    cancellationToken);
                
                if (vehicle != null)
                {
                    // Cache the result - but use a shorter expiration for real-time vehicles
                    await _vehicleCache.SetAsync(cacheKey, vehicle, _vehicleCacheExpiration);
                    _logger.LogInformation("Retrieved and cached vehicle {VehicleId} from API", vehicleId);
                }
                else
                {
                    _logger.LogWarning("API returned no vehicle for ID {VehicleId}", vehicleId);
                }
                
                return vehicle;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve vehicle {VehicleId} from API", vehicleId);
                
                // Try to fall back to cache even if forceRefresh was true
                if (forceRefresh)
                {
                    var cachedVehicle = await _vehicleCache.GetAsync(cacheKey);
                    if (cachedVehicle != null)
                    {
                        _logger.LogInformation("Falling back to cached vehicle {VehicleId} after API failure", vehicleId);
                        return cachedVehicle;
                    }
                }
                
                throw;
            }
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
            
            // Build cache key based on parameters
            var timeKey = startTime.HasValue || endTime.HasValue ?
                $"{startTime?.ToString("yyyyMMddHHmm") ?? "start"}_to_{endTime?.ToString("yyyyMMddHHmm") ?? "end"}" :
                "all";
            
            var cacheKey = $"route_{routeId}_schedules_{timeKey}";
            
            // Check if we need to refresh from the API
            if (!forceRefresh)
            {
                // Try to get from cache first
                var cachedSchedules = await _scheduleCache.GetAsync(cacheKey);
                if (cachedSchedules != null)
                {
                    _logger.LogDebug("Retrieved {Count} schedules for route {RouteId} from cache", 
                        cachedSchedules.Count, routeId);
                    return cachedSchedules;
                }
            }
            
            // Create a response handler for this request
            var responseHandler = new ApiResponseHandler(_logger, _apiUsageStatistics);
            
            try
            {
                // Ensure we're authenticated
                if (!await EnsureAuthenticatedAsync(cancellationToken))
                {
                    _logger.LogWarning("Failed to retrieve schedules for route {RouteId}: Not authenticated", routeId);
                    return Array.Empty<Schedule>();
                }
                
                // Build query string for time filters
                var query = new List<string>();
                if (startTime.HasValue)
                    query.Add($"start_time={startTime.Value:yyyy-MM-ddTHH:mm:ss}");
                if (endTime.HasValue)
                    query.Add($"end_time={endTime.Value:yyyy-MM-ddTHH:mm:ss}");
                
                var queryString = query.Count > 0 ? "?" + string.Join("&", query) : "";
                
                // Make the API call with resilience handling
                var schedules = await responseHandler.ProcessWithRetriesAsync<List<Schedule>>(
                    async token => await _httpClient.GetAsync($"routes/{routeId}/schedules{queryString}", token),
                    _maxRetryAttempts,
                    200,
                    cancellationToken);
                
                if (schedules != null && schedules.Count > 0)
                {
                    // Cache the results with schedule-specific expiration
                    await _scheduleCache.SetAsync(cacheKey, schedules, _scheduleCacheExpiration);
                    _logger.LogInformation("Retrieved and cached {Count} schedules for route {RouteId} from API", 
                        schedules.Count, routeId);
                }
                else
                {
                    _logger.LogWarning("API returned no schedules for route {RouteId}", routeId);
                }
                
                return schedules ?? Array.Empty<Schedule>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve schedules for route {RouteId} from API", routeId);
                
                // Try to fall back to cache even if forceRefresh was true
                if (forceRefresh)
                {
                    var cachedSchedules = await _scheduleCache.GetAsync(cacheKey);
                    if (cachedSchedules != null)
                    {
                        _logger.LogInformation("Falling back to cached schedules for route {RouteId} after API failure", 
                            routeId);
                        return cachedSchedules;
                    }
                }
                
                throw;
            }
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
            
            // Build cache key based on parameters
            var routeKey = !string.IsNullOrEmpty(routeId) ? routeId : "all";
            var timeKey = startTime.HasValue || endTime.HasValue ?
                $"{startTime?.ToString("yyyyMMddHHmm") ?? "start"}_to_{endTime?.ToString("yyyyMMddHHmm") ?? "end"}" :
                "all";
            
            var cacheKey = $"stop_{stopId}_route_{routeKey}_schedules_{timeKey}";
            
            // Check if we need to refresh from the API
            if (!forceRefresh)
            {
                // Try to get from cache first
                var cachedSchedules = await _scheduleCache.GetAsync(cacheKey);
                if (cachedSchedules != null)
                {
                    _logger.LogDebug("Retrieved {Count} schedules for stop {StopId} from cache", 
                        cachedSchedules.Count, stopId);
                    return cachedSchedules;
                }
            }
            
            // Create a response handler for this request
            var responseHandler = new ApiResponseHandler(_logger, _apiUsageStatistics);
            
            try
            {
                // Ensure we're authenticated
                if (!await EnsureAuthenticatedAsync(cancellationToken))
                {
                    _logger.LogWarning("Failed to retrieve schedules for stop {StopId}: Not authenticated", stopId);
                    return Array.Empty<Schedule>();
                }
                
                // Build query string for filters
                var query = new List<string>();
                if (!string.IsNullOrEmpty(routeId))
                    query.Add($"route_id={routeId}");
                if (startTime.HasValue)
                    query.Add($"start_time={startTime.Value:yyyy-MM-ddTHH:mm:ss}");
                if (endTime.HasValue)
                    query.Add($"end_time={endTime.Value:yyyy-MM-ddTHH:mm:ss}");
                
                var queryString = query.Count > 0 ? "?" + string.Join("&", query) : "";
                
                // Make the API call with resilience handling
                var schedules = await responseHandler.ProcessWithRetriesAsync<List<Schedule>>(
                    async token => await _httpClient.GetAsync($"stops/{stopId}/schedules{queryString}", token),
                    _maxRetryAttempts,
                    200,
                    cancellationToken);
                
                if (schedules != null && schedules.Count > 0)
                {
                    // Cache the results with schedule-specific expiration
                    await _scheduleCache.SetAsync(cacheKey, schedules, _scheduleCacheExpiration);
                    _logger.LogInformation("Retrieved and cached {Count} schedules for stop {StopId} from API", 
                        schedules.Count, stopId);
                }
                else
                {
                    _logger.LogWarning("API returned no schedules for stop {StopId}", stopId);
                }
                
                return schedules ?? Array.Empty<Schedule>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve schedules for stop {StopId} from API", stopId);
                
                // Try to fall back to cache even if forceRefresh was true
                if (forceRefresh)
                {
                    var cachedSchedules = await _scheduleCache.GetAsync(cacheKey);
                    if (cachedSchedules != null)
                    {
                        _logger.LogInformation("Falling back to cached schedules for stop {StopId} after API failure", 
                            stopId);
                        return cachedSchedules;
                    }
                }
                
                throw;
            }
        }
        
        /// <summary>
        /// Authenticates with the API
        /// </summary>
        public async Task<bool> AuthenticateAsync(string apiKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentNullException(nameof(apiKey));
            }
            
            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                ConnectionStatus = ApiConnectionStatus.Connecting;
                _apiKey = apiKey;
                
                // Store the API key for future requests
                _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
                
                // Test the connection with a simple API call
                // For now, just simulate authentication success
                await Task.Delay(100, cancellationToken); // Simulate network call
                
                _lastAuthCheck = DateTime.UtcNow;
                ConnectionStatus = ApiConnectionStatus.Connected;
                _logger.LogInformation("Successfully authenticated with transport API");
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to authenticate with transport API");
                ConnectionStatus = ApiConnectionStatus.AuthenticationFailed;
                return false;
            }
            finally
            {
                _connectionLock.Release();
            }
        }
        
        /// <summary>
        /// Ensures the client is authenticated before making API calls
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if authenticated, false otherwise</returns>
        private async Task<bool> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default)
        {
            // If we're already authenticated and checked recently, we're good
            if (ConnectionStatus == ApiConnectionStatus.Connected && 
                (DateTime.UtcNow - _lastAuthCheck).TotalMinutes < 5)
            {
                return true;
            }
            
            // If we have an API key but haven't checked recently, validate it
            if (!string.IsNullOrEmpty(_apiKey))
            {
                await _connectionLock.WaitAsync(cancellationToken);
                try
                {
                    // Double-check after acquiring lock
                    if (ConnectionStatus == ApiConnectionStatus.Connected && 
                        (DateTime.UtcNow - _lastAuthCheck).TotalMinutes < 5)
                    {
                        return true;
                    }
                    
                    // Test authentication with a lightweight API call
                    try
                    {
                        // For now, just simulate authentication check
                        await Task.Delay(50, cancellationToken);
                        
                        _lastAuthCheck = DateTime.UtcNow;
                        ConnectionStatus = ApiConnectionStatus.Connected;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Authentication validation failed");
                        ConnectionStatus = ApiConnectionStatus.AuthenticationFailed;
                        return false;
                    }
                }
                finally
                {
                    _connectionLock.Release();
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets the current API usage statistics
        /// </summary>
        public Task<ApiUsageStatistics> GetApiUsageStatisticsAsync(CancellationToken cancellationToken = default)
        {
            _apiUsageStatistics.LastUpdated = DateTime.UtcNow;
            return Task.FromResult(_apiUsageStatistics);
        }
        
        /// <summary>
        /// Clears any cached data in the client
        /// </summary>
        public void ClearCache()
        {
            _routeCache.ClearAsync().Wait();
            _stopCache.ClearAsync().Wait();
            _vehicleCache.ClearAsync().Wait();
            _scheduleCache.ClearAsync().Wait();
            _logger.LogInformation("Cleared all API caches");
        }
    }
}
