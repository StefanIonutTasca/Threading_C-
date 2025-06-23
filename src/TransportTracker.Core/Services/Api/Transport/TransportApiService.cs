using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TransportTracker.Core.Models;

namespace TransportTracker.Core.Services.Api.Transport
{
    /// <summary>
    /// Implementation of the transport API service
    /// </summary>
    public class TransportApiService : ITransportApiService
    {
        private const string DefaultApiName = "TransportApi";
        private readonly IApiClientFactory _apiClientFactory;
        private readonly ILogger<TransportApiService> _logger;
        private readonly string _apiName;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransportApiService"/> class
        /// </summary>
        /// <param name="apiClientFactory">API client factory</param>
        /// <param name="logger">Logger</param>
        /// <param name="apiName">Name of the API to use</param>
        public TransportApiService(
            IApiClientFactory apiClientFactory,
            ILogger<TransportApiService> logger,
            string apiName = DefaultApiName)
        {
            _apiClientFactory = apiClientFactory ?? throw new ArgumentNullException(nameof(apiClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiName = apiName ?? DefaultApiName;
        }

        /// <inheritdoc />
        public async Task<List<Route>> GetRoutesAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching all routes");
            
            try
            {
                var apiClient = _apiClientFactory.CreateClient(_apiName);
                var response = await apiClient.GetAsync<List<Route>>(
                    ApiEndpoints.Routes,
                    cancellationToken: cancellationToken);
                
                _logger.LogInformation("Successfully fetched {Count} routes", response?.Count ?? 0);
                return response ?? new List<Route>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching routes");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Route> GetRouteAsync(string routeId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(routeId))
            {
                throw new ArgumentException("Route ID cannot be null or empty", nameof(routeId));
            }
            
            _logger.LogInformation("Fetching route with ID {RouteId}", routeId);
            
            try
            {
                var apiClient = _apiClientFactory.CreateClient(_apiName);
                return await apiClient.GetAsync<Route>(
                    ApiEndpoints.RouteById(routeId),
                    cancellationToken: cancellationToken);
            }
            catch (ApiClientException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Route with ID {RouteId} not found", routeId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching route with ID {RouteId}", routeId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Stop>> GetRouteStopsAsync(string routeId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(routeId))
            {
                throw new ArgumentException("Route ID cannot be null or empty", nameof(routeId));
            }
            
            _logger.LogInformation("Fetching stops for route with ID {RouteId}", routeId);
            
            try
            {
                var apiClient = _apiClientFactory.CreateClient(_apiName);
                var response = await apiClient.GetAsync<List<Stop>>(
                    ApiEndpoints.RouteStopsById(routeId),
                    cancellationToken: cancellationToken);
                
                _logger.LogInformation("Successfully fetched {Count} stops for route {RouteId}", response?.Count ?? 0, routeId);
                return response ?? new List<Stop>();
            }
            catch (ApiClientException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Route with ID {RouteId} not found when fetching stops", routeId);
                return new List<Stop>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching stops for route with ID {RouteId}", routeId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Vehicle>> GetRouteVehiclesAsync(string routeId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(routeId))
            {
                throw new ArgumentException("Route ID cannot be null or empty", nameof(routeId));
            }
            
            _logger.LogInformation("Fetching vehicles for route with ID {RouteId}", routeId);
            
            try
            {
                var apiClient = _apiClientFactory.CreateClient(_apiName);
                var response = await apiClient.GetAsync<List<Vehicle>>(
                    ApiEndpoints.RouteVehiclesById(routeId),
                    cancellationToken: cancellationToken);
                
                _logger.LogInformation("Successfully fetched {Count} vehicles for route {RouteId}", response?.Count ?? 0, routeId);
                return response ?? new List<Vehicle>();
            }
            catch (ApiClientException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Route with ID {RouteId} not found when fetching vehicles", routeId);
                return new List<Vehicle>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching vehicles for route with ID {RouteId}", routeId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Vehicle>> GetVehicleLocationsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching all vehicle locations");
            
            try
            {
                var apiClient = _apiClientFactory.CreateClient(_apiName);
                var response = await apiClient.GetAsync<List<Vehicle>>(
                    ApiEndpoints.VehicleLocations,
                    cancellationToken: cancellationToken);
                
                _logger.LogInformation("Successfully fetched {Count} vehicle locations", response?.Count ?? 0);
                return response ?? new List<Vehicle>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching vehicle locations");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Vehicle> GetVehicleAsync(string vehicleId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(vehicleId))
            {
                throw new ArgumentException("Vehicle ID cannot be null or empty", nameof(vehicleId));
            }
            
            _logger.LogInformation("Fetching vehicle with ID {VehicleId}", vehicleId);
            
            try
            {
                var apiClient = _apiClientFactory.CreateClient(_apiName);
                return await apiClient.GetAsync<Vehicle>(
                    ApiEndpoints.VehicleById(vehicleId),
                    cancellationToken: cancellationToken);
            }
            catch (ApiClientException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Vehicle with ID {VehicleId} not found", vehicleId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching vehicle with ID {VehicleId}", vehicleId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Stop>> GetStopsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching all stops");
            
            try
            {
                var apiClient = _apiClientFactory.CreateClient(_apiName);
                var response = await apiClient.GetAsync<List<Stop>>(
                    ApiEndpoints.Stops,
                    cancellationToken: cancellationToken);
                
                _logger.LogInformation("Successfully fetched {Count} stops", response?.Count ?? 0);
                return response ?? new List<Stop>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching stops");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Stop> GetStopAsync(string stopId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(stopId))
            {
                throw new ArgumentException("Stop ID cannot be null or empty", nameof(stopId));
            }
            
            _logger.LogInformation("Fetching stop with ID {StopId}", stopId);
            
            try
            {
                var apiClient = _apiClientFactory.CreateClient(_apiName);
                return await apiClient.GetAsync<Stop>(
                    ApiEndpoints.StopById(stopId),
                    cancellationToken: cancellationToken);
            }
            catch (ApiClientException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Stop with ID {StopId} not found", stopId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching stop with ID {StopId}", stopId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<ArrivalPrediction>> GetStopPredictionsAsync(string stopId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(stopId))
            {
                throw new ArgumentException("Stop ID cannot be null or empty", nameof(stopId));
            }
            
            _logger.LogInformation("Fetching predictions for stop with ID {StopId}", stopId);
            
            try
            {
                var apiClient = _apiClientFactory.CreateClient(_apiName);
                var response = await apiClient.GetAsync<List<ArrivalPrediction>>(
                    ApiEndpoints.StopPredictionsById(stopId),
                    cancellationToken: cancellationToken);
                
                _logger.LogInformation("Successfully fetched {Count} predictions for stop {StopId}", response?.Count ?? 0, stopId);
                return response ?? new List<ArrivalPrediction>();
            }
            catch (ApiClientException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Stop with ID {StopId} not found when fetching predictions", stopId);
                return new List<ArrivalPrediction>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching predictions for stop with ID {StopId}", stopId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<ServiceAlert>> GetServiceAlertsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching service alerts");
            
            try
            {
                var apiClient = _apiClientFactory.CreateClient(_apiName);
                var response = await apiClient.GetAsync<List<ServiceAlert>>(
                    ApiEndpoints.Alerts,
                    cancellationToken: cancellationToken);
                
                _logger.LogInformation("Successfully fetched {Count} service alerts", response?.Count ?? 0);
                return response?.Where(a => a.IsActive).ToList() ?? new List<ServiceAlert>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching service alerts");
                throw;
            }
        }
    }
}
