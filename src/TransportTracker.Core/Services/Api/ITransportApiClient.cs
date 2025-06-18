using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TransportTracker.Core.Models;

namespace TransportTracker.Core.Services.Api
{
    /// <summary>
    /// Interface for a transport API client that provides access to real-time transport data
    /// with thread-safe operations and resilience features
    /// </summary>
    public interface ITransportApiClient
    {
        /// <summary>
        /// Gets the API connection status
        /// </summary>
        ApiConnectionStatus ConnectionStatus { get; }
        
        /// <summary>
        /// Event triggered when connection status changes
        /// </summary>
        event EventHandler<ApiConnectionStatus> ConnectionStatusChanged;
        
        /// <summary>
        /// Gets all routes from the API
        /// </summary>
        /// <param name="forceRefresh">Forces a refresh from API instead of using cache</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Collection of routes</returns>
        Task<IEnumerable<Route>> GetRoutesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets a specific route by ID
        /// </summary>
        /// <param name="routeId">The ID of the route</param>
        /// <param name="includeStops">Whether to include full stop details</param>
        /// <param name="includeSchedules">Whether to include schedule information</param>
        /// <param name="forceRefresh">Forces a refresh from API instead of using cache</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The route or null if not found</returns>
        Task<Route> GetRouteAsync(string routeId, bool includeStops = false, bool includeSchedules = false, 
            bool forceRefresh = false, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets all stops from the API
        /// </summary>
        /// <param name="forceRefresh">Forces a refresh from API instead of using cache</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Collection of stops</returns>
        Task<IEnumerable<Stop>> GetStopsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets a specific stop by ID
        /// </summary>
        /// <param name="stopId">The ID of the stop</param>
        /// <param name="includeRoutes">Whether to include route information</param>
        /// <param name="forceRefresh">Forces a refresh from API instead of using cache</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The stop or null if not found</returns>
        Task<Stop> GetStopAsync(string stopId, bool includeRoutes = false,
            bool forceRefresh = false, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets all vehicles from the API
        /// </summary>
        /// <param name="forceRefresh">Forces a refresh from API instead of using cache</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Collection of vehicles</returns>
        Task<IEnumerable<Vehicle>> GetVehiclesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets all vehicles for a specific route
        /// </summary>
        /// <param name="routeId">The ID of the route</param>
        /// <param name="forceRefresh">Forces a refresh from API instead of using cache</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Collection of vehicles on the specified route</returns>
        Task<IEnumerable<Vehicle>> GetVehiclesByRouteAsync(string routeId, bool forceRefresh = false, 
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets a specific vehicle by ID
        /// </summary>
        /// <param name="vehicleId">The ID of the vehicle</param>
        /// <param name="includeRoute">Whether to include route information</param>
        /// <param name="forceRefresh">Forces a refresh from API instead of using cache</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The vehicle or null if not found</returns>
        Task<Vehicle> GetVehicleAsync(string vehicleId, bool includeRoute = false,
            bool forceRefresh = false, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets schedules for a route
        /// </summary>
        /// <param name="routeId">The ID of the route</param>
        /// <param name="startTime">Optional start time filter</param>
        /// <param name="endTime">Optional end time filter</param>
        /// <param name="forceRefresh">Forces a refresh from API instead of using cache</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Collection of schedules for the route</returns>
        Task<IEnumerable<Schedule>> GetSchedulesForRouteAsync(string routeId, 
            DateTime? startTime = null, DateTime? endTime = null,
            bool forceRefresh = false, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets schedules for a stop
        /// </summary>
        /// <param name="stopId">The ID of the stop</param>
        /// <param name="routeId">Optional route ID filter</param>
        /// <param name="startTime">Optional start time filter</param>
        /// <param name="endTime">Optional end time filter</param>
        /// <param name="forceRefresh">Forces a refresh from API instead of using cache</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Collection of schedules for the stop</returns>
        Task<IEnumerable<Schedule>> GetSchedulesForStopAsync(string stopId, string routeId = null,
            DateTime? startTime = null, DateTime? endTime = null,
            bool forceRefresh = false, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Authenticates with the API
        /// </summary>
        /// <param name="apiKey">API key for authentication</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if authentication succeeded</returns>
        Task<bool> AuthenticateAsync(string apiKey, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the current API usage statistics
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>API usage statistics</returns>
        Task<ApiUsageStatistics> GetApiUsageStatisticsAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Clears any cached data in the client
        /// </summary>
        void ClearCache();
    }
    
    /// <summary>
    /// Represents the connection status to the transport API
    /// </summary>
    public enum ApiConnectionStatus
    {
        /// <summary>
        /// Not connected to the API
        /// </summary>
        Disconnected,
        
        /// <summary>
        /// Currently connecting to the API
        /// </summary>
        Connecting,
        
        /// <summary>
        /// Connected to the API
        /// </summary>
        Connected,
        
        /// <summary>
        /// Connection is experiencing issues
        /// </summary>
        Degraded,
        
        /// <summary>
        /// Authentication failed
        /// </summary>
        AuthenticationFailed,
        
        /// <summary>
        /// API rate limit exceeded
        /// </summary>
        RateLimitExceeded
    }
    
    /// <summary>
    /// Statistics about API usage
    /// </summary>
    public class ApiUsageStatistics
    {
        /// <summary>
        /// Total number of API calls made
        /// </summary>
        public int TotalApiCalls { get; set; }
        
        /// <summary>
        /// API calls made in the current period (usually daily)
        /// </summary>
        public int CurrentPeriodCalls { get; set; }
        
        /// <summary>
        /// Maximum API calls allowed in the current period
        /// </summary>
        public int RateLimitPerPeriod { get; set; }
        
        /// <summary>
        /// When the current rate limit period resets
        /// </summary>
        public DateTime RateLimitResetTime { get; set; }
        
        /// <summary>
        /// Percentage of rate limit used
        /// </summary>
        public double RateLimitPercentageUsed => 
            RateLimitPerPeriod > 0 ? (double)CurrentPeriodCalls / RateLimitPerPeriod * 100 : 0;
        
        /// <summary>
        /// Number of successful API calls
        /// </summary>
        public int SuccessfulCalls { get; set; }
        
        /// <summary>
        /// Number of failed API calls
        /// </summary>
        public int FailedCalls { get; set; }
        
        /// <summary>
        /// Average response time in milliseconds
        /// </summary>
        public double AverageResponseTimeMs { get; set; }
        
        /// <summary>
        /// When statistics were last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
