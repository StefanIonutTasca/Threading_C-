using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TransportTracker.Core.Models;
using TransportTracker.Core.Services.Api.Transport.Models;

namespace TransportTracker.Core.Services.Api.Transport
{
    /// <summary>
    /// Implementation of the transport API service using the Busmaps GTFS API
    /// </summary>
    public class BusmapsTransportApiService : ITransportApiService
    {
        private readonly BusmapsApiClient _apiClient;
        private readonly ILogger<BusmapsTransportApiService> _logger;
        private readonly string _regionName;
        private readonly string _countryIso;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="BusmapsTransportApiService"/> class
        /// </summary>
        /// <param name="apiClient">The Busmaps API client</param>
        /// <param name="logger">The logger</param>
        /// <param name="regionName">Default region name (e.g., "uk_ireland")</param>
        /// <param name="countryIso">Default ISO country code (e.g., "GBR")</param>
        public BusmapsTransportApiService(
            BusmapsApiClient apiClient,
            ILogger<BusmapsTransportApiService> logger,
            string regionName = "uk_ireland",
            string countryIso = "GBR")
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _regionName = regionName;
            _countryIso = countryIso;
        }

        /// <summary>
        /// Gets routes between two coordinate points
        /// </summary>
        /// <param name="originLat">Origin latitude</param>
        /// <param name="originLon">Origin longitude</param>
        /// <param name="destLat">Destination latitude</param>
        /// <param name="destLon">Destination longitude</param>
        /// <param name="departureTime">Optional departure time</param>
        /// <param name="maxTransfers">Optional maximum number of transfers</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of available routes</returns>
        public async Task<List<Route>> GetRoutesAsync(
            double originLat, 
            double originLon, 
            double destLat, 
            double destLon, 
            DateTime? departureTime = null, 
            int? maxTransfers = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation(
                    "Getting routes from ({0},{1}) to ({2},{3})", 
                    originLat, originLon, destLat, destLon);
                
                var response = await _apiClient.GetRoutesAsync(
                    originLat, originLon, destLat, destLon, departureTime, maxTransfers, cancellationToken);
                
                // Map API response to domain models
                var routes = new List<Route>();
                
                if (response?.Routes != null)
                {
                    foreach (var routeDto in response.Routes)
                    {
                        var route = new Route
                        {
                            Id = routeDto.Id,
                            Name = "Route " + routeDto.Id,
                            Description = $"{routeDto.Transfers} transfers, {routeDto.Duration / 60} min",
                            TransportType = GetPredominantTransportType(routeDto),
                            // Add other properties as needed
                        };
                        
                        routes.Add(route);
                    }
                }
                
                _logger.LogInformation("Found {0} routes", routes.Count);
                return routes;
            }
            catch (ApiClientException ex)
            {
                _logger.LogError(ex, "Error getting routes: {0}", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting routes: {0}", ex.Message);
                throw;
            }
        }
        
        /// <summary>
        /// Gets the predominant transport type from a route
        /// </summary>
        /// <param name="routeDto">Route data</param>
        /// <returns>Transport type</returns>
        private static TransportType GetPredominantTransportType(Models.Route routeDto)
        {
            // Default to bus
            var transportType = TransportType.BUS;
            
            if (routeDto.Sections != null && routeDto.Sections.Count > 0)
            {
                // Count transport types and choose the most common one
                var transportTypes = new Dictionary<TransportType, int>();
                
                foreach (var section in routeDto.Sections)
                {
                    if (section.Transport?.Mode != null)
                    {
                        var type = ParseTransportType(section.Transport.Mode);
                        if (transportTypes.ContainsKey(type))
                        {
                            transportTypes[type]++;
                        }
                        else
                        {
                            transportTypes[type] = 1;
                        }
                    }
                }
                
                if (transportTypes.Count > 0)
                {
                    int maxCount = 0;
                    foreach (var kvp in transportTypes)
                    {
                        if (kvp.Value > maxCount)
                        {
                            transportType = kvp.Key;
                            maxCount = kvp.Value;
                        }
                    }
                }
            }
            
            return transportType;
        }
        
        /// <summary>
        /// Parses a transport mode string to a TransportType
        /// </summary>
        /// <param name="mode">Transport mode string</param>
        /// <returns>Transport type</returns>
        private static TransportType ParseTransportType(string mode)
        {
            if (string.IsNullOrEmpty(mode))
            {
                return TransportType.BUS;
            }
            
            switch (mode.ToLowerInvariant())
            {
                case "bus":
                    return TransportType.BUS;
                case "subway":
                case "metro":
                    return TransportType.SUBWAY;
                case "train":
                case "rail":
                    return TransportType.TRAIN;
                case "tram":
                case "light_rail":
                    return TransportType.TRAM;
                case "ferry":
                    return TransportType.FERRY;
                case "pedestrian":
                case "walk":
                    return TransportType.WALK;
                default:
                    return TransportType.BUS;
            }
        }

        /// <summary>
        /// Gets stops within a radius of a specific location
        /// </summary>
        /// <param name="latitude">Latitude</param>
        /// <param name="longitude">Longitude</param>
        /// <param name="radiusInMeters">Radius in meters</param>
        /// <param name="limit">Optional limit on number of stops</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of stops</returns>
        public async Task<List<Stop>> GetStopsInRadiusAsync(
            double latitude, 
            double longitude, 
            int radiusInMeters = 500, 
            int? limit = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation(
                    "Getting stops in radius {0}m around ({1},{2})", 
                    radiusInMeters, latitude, longitude);
                
                var response = await _apiClient.GetStopsInRadiusAsync(
                    latitude, longitude, radiusInMeters, limit, cancellationToken);
                
                // Map API response to domain models
                var stops = new List<Stop>();
                
                if (response?.Stops != null)
                {
                    foreach (var stopDto in response.Stops)
                    {
                        stops.Add(stopDto.ToDomainStop());
                    }
                }
                
                _logger.LogInformation("Found {0} stops", stops.Count);
                return stops;
            }
            catch (ApiClientException ex)
            {
                _logger.LogError(ex, "Error getting stops in radius: {0}", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting stops in radius: {0}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets arrival predictions for a specific stop
        /// </summary>
        /// <param name="stopId">Stop identifier</param>
        /// <param name="limit">Optional limit on number of predictions</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of arrival predictions</returns>
        public async Task<List<ArrivalPrediction>> GetArrivalPredictionsAsync(
            string stopId, 
            int? limit = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting arrival predictions for stop {0}", stopId);
                
                var response = await _apiClient.GetNextDeparturesByStopAsync(
                    stopId, _regionName, _countryIso, null, limit, cancellationToken);
                
                // Map API response to domain models
                var predictions = new List<ArrivalPrediction>();
                
                if (response?.StopDepartures != null)
                {
                    foreach (var stopDeparture in response.StopDepartures)
                    {
                        foreach (var departure in stopDeparture.DepartureList)
                        {
                            var arrivalTime = departure.GetDepartureDateTime();
                            
                            var prediction = new ArrivalPrediction
                            {
                                StopId = stopDeparture.StopId,
                                RouteId = departure.RouteId,
                                ScheduledArrivalTime = arrivalTime,
                                PredictedArrivalTime = arrivalTime, // No real-time prediction in the API response
                                Destination = departure.TripHeadsign,
                                // Set other properties as available
                            };
                            
                            predictions.Add(prediction);
                        }
                    }
                }
                
                _logger.LogInformation("Found {0} arrival predictions", predictions.Count);
                return predictions;
            }
            catch (ApiClientException ex)
            {
                _logger.LogError(ex, "Error getting arrival predictions: {0}", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting arrival predictions: {0}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets arrival predictions for stops near a location
        /// </summary>
        /// <param name="latitude">Latitude</param>
        /// <param name="longitude">Longitude</param>
        /// <param name="radiusInMeters">Radius in meters</param>
        /// <param name="limit">Optional limit on number of predictions</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of arrival predictions</returns>
        public async Task<List<ArrivalPrediction>> GetArrivalPredictionsByLocationAsync(
            double latitude, 
            double longitude, 
            int radiusInMeters = 500, 
            int? limit = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation(
                    "Getting arrival predictions near ({0},{1}) within {2}m", 
                    latitude, longitude, radiusInMeters);
                
                var response = await _apiClient.GetNextDeparturesByLocationAsync(
                    latitude, longitude, radiusInMeters, limit, null, cancellationToken);
                
                // Map API response to domain models
                var predictions = new List<ArrivalPrediction>();
                
                if (response?.StopDepartures != null)
                {
                    foreach (var stopDeparture in response.StopDepartures)
                    {
                        foreach (var departure in stopDeparture.DepartureList)
                        {
                            var arrivalTime = departure.GetDepartureDateTime();
                            
                            var prediction = new ArrivalPrediction
                            {
                                StopId = stopDeparture.StopId,
                                RouteId = departure.RouteId,
                                ScheduledArrivalTime = arrivalTime,
                                PredictedArrivalTime = arrivalTime, // No real-time prediction in the API response
                                Destination = departure.TripHeadsign,
                                // Set other properties as available
                            };
                            
                            predictions.Add(prediction);
                        }
                    }
                }
                
                _logger.LogInformation("Found {0} arrival predictions", predictions.Count);
                return predictions;
            }
            catch (ApiClientException ex)
            {
                _logger.LogError(ex, "Error getting arrival predictions by location: {0}", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting arrival predictions by location: {0}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets vehicle locations for a specific route
        /// </summary>
        /// <param name="routeId">Route identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of vehicle locations</returns>
        public Task<List<VehicleLocation>> GetVehicleLocationsAsync(
            string routeId, 
            CancellationToken cancellationToken = default)
        {
            // Busmaps API doesn't provide real-time vehicle locations
            _logger.LogWarning("GetVehicleLocationsAsync is not implemented for Busmaps API");
            return Task.FromResult(new List<VehicleLocation>());
        }

        /// <summary>
        /// Gets service alerts
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of service alerts</returns>
        public Task<List<ServiceAlert>> GetServiceAlertsAsync(CancellationToken cancellationToken = default)
        {
            // Busmaps API doesn't provide service alerts
            _logger.LogWarning("GetServiceAlertsAsync is not implemented for Busmaps API");
            return Task.FromResult(new List<ServiceAlert>());
        }

        /// <summary>
        /// Gets routes by IDs
        /// </summary>
        /// <param name="routeIds">Route identifiers</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of routes</returns>
        public Task<List<Route>> GetRoutesByIdsAsync(IEnumerable<string> routeIds, CancellationToken cancellationToken = default)
        {
            // Busmaps API doesn't provide a direct endpoint to get routes by IDs
            _logger.LogWarning("GetRoutesByIdsAsync is not implemented for Busmaps API");
            return Task.FromResult(new List<Route>());
        }

        /// <summary>
        /// Gets all available routes
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of routes</returns>
        public Task<List<Route>> GetAllRoutesAsync(CancellationToken cancellationToken = default)
        {
            // Busmaps API doesn't provide an endpoint to get all routes
            _logger.LogWarning("GetAllRoutesAsync is not implemented for Busmaps API");
            return Task.FromResult(new List<Route>());
        }
        
        /// <summary>
        /// Gets pedestrian route between points
        /// </summary>
        /// <param name="coordinates">List of coordinate pairs (lat,lon)</param>
        /// <param name="includeSteps">Whether to include turn-by-turn instructions</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Pedestrian route information</returns>
        public async Task<Models.PedestrianRoute> GetPedestrianRouteAsync(
            List<(double lat, double lon)> coordinates, 
            bool includeSteps = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting pedestrian route with {0} coordinates", coordinates.Count);
                
                // Convert coordinates from (lat,lon) to (lon,lat) format
                var lonLatCoordinates = coordinates.ConvertAll(c => (c.lon, c.lat));
                
                var response = await _apiClient.GetPedestrianRouteAsync(
                    lonLatCoordinates, null, includeSteps, cancellationToken);
                
                if (response != null && response.IsValidResponse() && response.Routes.Count > 0)
                {
                    var route = response.Routes[0];
                    
                    var result = new Models.PedestrianRoute
                    {
                        Duration = TimeSpan.FromSeconds(route.Duration),
                        Distance = route.Distance,
                        EncodedPolyline = route.Geometry,
                        // Add other properties as needed
                    };
                    
                    if (includeSteps && route.Legs != null)
                    {
                        result.Steps = new List<Models.RouteStep>();
                        
                        foreach (var leg in route.Legs)
                        {
                            if (leg.Steps != null)
                            {
                                foreach (var step in leg.Steps)
                                {
                                    result.Steps.Add(new Models.RouteStep
                                    {
                                        Instruction = $"{step.Maneuver?.GetDescription()} on {step.Name}",
                                        Distance = step.Distance,
                                        Duration = TimeSpan.FromSeconds(step.Duration)
                                    });
                                }
                            }
                        }
                    }
                    
                    return result;
                }
                
                return null;
            }
            catch (ApiClientException ex)
            {
                _logger.LogError(ex, "Error getting pedestrian route: {0}", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting pedestrian route: {0}", ex.Message);
                throw;
            }
        }
    }
}
