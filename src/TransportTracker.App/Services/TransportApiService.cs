using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TransportTracker.App.Core.Diagnostics;
using TransportTracker.App.Models;
using TransportTracker.App.Services.Caching;

namespace TransportTracker.App.Services
{
    /// <summary>
    /// Service for interacting with transport API endpoints
    /// </summary>
    public class TransportApiService : ITransportApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ICacheManager _cacheManager;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(15);
        
        // API endpoints (would normally be in configuration)
        private const string BaseApiUrl = "https://api.transportdata.org/v1/";
        private const string VehiclesEndpoint = "vehicles";
        private const string RoutesEndpoint = "routes";
        private const string StopsEndpoint = "stops";
        private const string StatusEndpoint = "status";
        
        // Default API request parameters
        private string _defaultApiKey = null;
        private string _defaultCity = "London";
        
        public TransportApiService(ICacheManager cacheManager)
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(BaseApiUrl);
            _httpClient.Timeout = _defaultTimeout;
            
            _cacheManager = cacheManager;
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }
        
        /// <summary>
        /// Set API authentication key
        /// </summary>
        public void SetApiKey(string apiKey)
        {
            _defaultApiKey = apiKey;
            _httpClient.DefaultRequestHeaders.Clear();
            
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            }
        }
        
        /// <summary>
        /// Set the city for API requests
        /// </summary>
        public void SetCity(string city)
        {
            _defaultCity = city;
        }
        
        /// <summary>
        /// Get transport vehicles with caching and retry support
        /// </summary>
        public async Task<List<TransportVehicle>> GetVehiclesAsync(
            string transportType = null, 
            bool bypassCache = false,
            CancellationToken cancellationToken = default)
        {
            string cacheKey = $"vehicles_{transportType ?? "all"}_{_defaultCity}";
            
            // Try to get from cache if not bypassing
            if (!bypassCache)
            {
                var cachedData = _cacheManager.Get<List<TransportVehicle>>(cacheKey);
                if (cachedData != null)
                {
                    return cachedData;
                }
            }
            
            // Build API request URL
            string endpoint = VehiclesEndpoint;
            if (!string.IsNullOrEmpty(transportType))
            {
                endpoint += $"?type={transportType}";
            }
            
            // Add city parameter
            endpoint += (endpoint.Contains("?") ? "&" : "?") + $"city={_defaultCity}";
            
            using (PerformanceMonitor.Instance.StartOperation("Api_GetVehicles"))
            {
                try
                {
                    // Make the API request with retry support
                    var vehicles = await MakeApiRequestWithRetryAsync<List<TransportVehicle>>(
                        endpoint, 
                        3, // Max retry attempts
                        cancellationToken
                    );
                    
                    // Cache the result if successful
                    if (vehicles != null)
                    {
                        // Cache for 1 minute (vehicles move frequently)
                        _cacheManager.Set(cacheKey, vehicles, TimeSpan.FromMinutes(1));
                    }
                    
                    return vehicles ?? new List<TransportVehicle>();
                }
                catch (Exception ex)
                {
                    PerformanceMonitor.Instance.RecordFailure("Api_GetVehicles", ex);
                    System.Diagnostics.Debug.WriteLine($"Error getting vehicles: {ex.Message}");
                    
                    // Return empty list on error
                    return new List<TransportVehicle>();
                }
            }
        }
        
        /// <summary>
        /// Get transport routes with caching and retry support
        /// </summary>
        public async Task<List<RouteInfo>> GetRoutesAsync(
            string transportType = null,
            bool bypassCache = false,
            CancellationToken cancellationToken = default)
        {
            string cacheKey = $"routes_{transportType ?? "all"}_{_defaultCity}";
            
            // Try to get from cache if not bypassing
            if (!bypassCache)
            {
                var cachedData = _cacheManager.Get<List<RouteInfo>>(cacheKey);
                if (cachedData != null)
                {
                    return cachedData;
                }
            }
            
            // Build API request URL
            string endpoint = RoutesEndpoint;
            if (!string.IsNullOrEmpty(transportType))
            {
                endpoint += $"?type={transportType}";
            }
            
            // Add city parameter
            endpoint += (endpoint.Contains("?") ? "&" : "?") + $"city={_defaultCity}";
            
            using (PerformanceMonitor.Instance.StartOperation("Api_GetRoutes"))
            {
                try
                {
                    // Make the API request with retry support
                    var routes = await MakeApiRequestWithRetryAsync<List<RouteInfo>>(
                        endpoint, 
                        3, // Max retry attempts
                        cancellationToken
                    );
                    
                    // Cache the result if successful
                    if (routes != null)
                    {
                        // Cache for 30 minutes (routes change less frequently)
                        _cacheManager.Set(cacheKey, routes, TimeSpan.FromMinutes(30));
                    }
                    
                    return routes ?? new List<RouteInfo>();
                }
                catch (Exception ex)
                {
                    PerformanceMonitor.Instance.RecordFailure("Api_GetRoutes", ex);
                    System.Diagnostics.Debug.WriteLine($"Error getting routes: {ex.Message}");
                    
                    // Return empty list on error
                    return new List<RouteInfo>();
                }
            }
        }
        
        /// <summary>
        /// Get transport stops with caching and retry support
        /// </summary>
        public async Task<List<TransportStop>> GetStopsAsync(
            string routeId = null,
            bool bypassCache = false,
            CancellationToken cancellationToken = default)
        {
            string cacheKey = $"stops_{routeId ?? "all"}_{_defaultCity}";
            
            // Try to get from cache if not bypassing
            if (!bypassCache)
            {
                var cachedData = _cacheManager.Get<List<TransportStop>>(cacheKey);
                if (cachedData != null)
                {
                    return cachedData;
                }
            }
            
            // Build API request URL
            string endpoint = StopsEndpoint;
            if (!string.IsNullOrEmpty(routeId))
            {
                endpoint += $"?routeId={routeId}";
            }
            
            // Add city parameter
            endpoint += (endpoint.Contains("?") ? "&" : "?") + $"city={_defaultCity}";
            
            using (PerformanceMonitor.Instance.StartOperation("Api_GetStops"))
            {
                try
                {
                    // Make the API request with retry support
                    var stops = await MakeApiRequestWithRetryAsync<List<TransportStop>>(
                        endpoint, 
                        3, // Max retry attempts
                        cancellationToken
                    );
                    
                    // Cache the result if successful
                    if (stops != null)
                    {
                        // Cache for 1 hour (stops rarely change)
                        _cacheManager.Set(cacheKey, stops, TimeSpan.FromHours(1));
                    }
                    
                    return stops ?? new List<TransportStop>();
                }
                catch (Exception ex)
                {
                    PerformanceMonitor.Instance.RecordFailure("Api_GetStops", ex);
                    System.Diagnostics.Debug.WriteLine($"Error getting stops: {ex.Message}");
                    
                    // Return empty list on error
                    return new List<TransportStop>();
                }
            }
        }
        
        /// <summary>
        /// Check API service status
        /// </summary>
        public async Task<bool> CheckApiStatusAsync(
            CancellationToken cancellationToken = default)
        {
            using (PerformanceMonitor.Instance.StartOperation("Api_CheckStatus"))
            {
                try
                {
                    var response = await _httpClient.GetAsync(StatusEndpoint, cancellationToken);
                    return response.IsSuccessStatusCode;
                }
                catch
                {
                    return false;
                }
            }
        }
        
        /// <summary>
        /// Make an API request with automatic retries on failure
        /// </summary>
        private async Task<T> MakeApiRequestWithRetryAsync<T>(
            string endpoint,
            int maxRetries,
            CancellationToken cancellationToken) where T : class
        {
            int attempts = 0;
            Exception lastException = null;
            
            while (attempts < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    attempts++;
                    
                    using (PerformanceMonitor.Instance.StartOperation($"Api_Request_{endpoint}"))
                    {
                        // Make the HTTP request
                        var response = await _httpClient.GetAsync(endpoint, cancellationToken);
                        
                        // Check if successful
                        if (response.IsSuccessStatusCode)
                        {
                            // Deserialize the response content
                            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken);
                        }
                        
                        // If this is a server error (5xx), we'll retry
                        // Otherwise, we'll throw an exception
                        if ((int)response.StatusCode < 500)
                        {
                            throw new HttpRequestException($"API request failed with status code {response.StatusCode}");
                        }
                    }
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    // Request timed out
                    lastException = ex;
                    System.Diagnostics.Debug.WriteLine($"API request timed out (attempt {attempts}/{maxRetries}): {ex.Message}");
                }
                catch (HttpRequestException ex)
                {
                    // HTTP error
                    lastException = ex;
                    System.Diagnostics.Debug.WriteLine($"HTTP error (attempt {attempts}/{maxRetries}): {ex.Message}");
                }
                catch (Exception ex)
                {
                    // Other error
                    lastException = ex;
                    System.Diagnostics.Debug.WriteLine($"API request error (attempt {attempts}/{maxRetries}): {ex.Message}");
                }
                
                // If we've reached max retries or cancellation is requested, throw the last exception
                if (attempts >= maxRetries || cancellationToken.IsCancellationRequested)
                {
                    if (lastException != null)
                    {
                        PerformanceMonitor.Instance.RecordFailure($"Api_Request_{endpoint}", lastException);
                    }
                    
                    // Return null instead of throwing - the calling method will handle it
                    return null;
                }
                
                // Wait before retrying (exponential backoff)
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempts - 1));
                await Task.Delay(delay, cancellationToken);
            }
            
            return null;
        }
    }
}
