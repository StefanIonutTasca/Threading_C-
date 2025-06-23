using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using TransportTracker.Core.Services.Api.Transport.Models;

namespace TransportTracker.Core.Services.Api.Transport
{
    /// <summary>
    /// Implementation of the API client specifically for the Busmaps GTFS API
    /// </summary>
    public class BusmapsApiClient : ApiClient
    {
        private readonly string _platformHost;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="BusmapsApiClient"/> class
        /// </summary>
        /// <param name="apiKey">API key for authentication (without 'Bearer' prefix)</param>
        /// <param name="platformHost">Platform host (e.g., "busmaps.com" or "wikiroutes.info")</param>
        /// <param name="logger">Logger instance</param>
        public BusmapsApiClient(string apiKey, string platformHost, ILogger<BusmapsApiClient> logger)
            : base(ApiEndpoints.BaseBusmapsUrl, logger, null)
        {
            if (string.IsNullOrEmpty(apiKey))
                throw new ArgumentException("API key is required for Busmaps API", nameof(apiKey));
            
            if (string.IsNullOrEmpty(platformHost))
                throw new ArgumentException("Platform host is required for Busmaps API", nameof(platformHost));
            
            _platformHost = platformHost;
            
            // Configure Busmaps-specific headers
            ConfigureBusmapsHeaders(apiKey, platformHost);
        }
        
        /// <summary>
        /// Configures the HTTP headers required for Busmaps API authentication
        /// </summary>
        /// <param name="apiKey">API key for authentication</param>
        /// <param name="platformHost">Platform host</param>
        private void ConfigureBusmapsHeaders(string apiKey, string platformHost)
        {
            // Use GetType().GetField to access the private _httpClient field from the base class
            var httpClientField = typeof(ApiClient).GetField("_httpClient", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
            if (httpClientField != null)
            {
                var httpClient = (HttpClient)httpClientField.GetValue(this);
                
                // Clear any existing headers
                httpClient.DefaultRequestHeaders.Clear();
                
                // Add required RapidAPI headers
                httpClient.DefaultRequestHeaders.Add("x-rapidapi-key", apiKey);
                httpClient.DefaultRequestHeaders.Add("x-rapidapi-host", platformHost);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }
            else
            {
                throw new InvalidOperationException("Could not access the HttpClient instance in the base class.");
            }
        }
        
        /// <summary>
        /// Gets transit routes between two points
        /// </summary>
        /// <param name="originLat">Origin latitude</param>
        /// <param name="originLon">Origin longitude</param>
        /// <param name="destLat">Destination latitude</param>
        /// <param name="destLon">Destination longitude</param>
        /// <param name="departureTime">Optional departure time (ISO8601)</param>
        /// <param name="maxTransfers">Optional maximum number of transfers</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Routes response data</returns>
        public Task<RoutesResponse> GetRoutesAsync(
            double originLat,
            double originLon,
            double destLat,
            double destLon,
            DateTime? departureTime = null,
            int? maxTransfers = null,
            CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>();
            
            if (departureTime.HasValue)
            {
                queryParams.Add("departureTime", departureTime.Value.ToString("o"));
            }
            
            if (maxTransfers.HasValue)
            {
                queryParams.Add("transfers", maxTransfers.Value.ToString());
            }
            
            return GetAsync<RoutesResponse>(
                ApiEndpoints.RoutesQuery(originLat, originLon, destLat, destLon),
                queryParams,
                cancellationToken);
        }
        
        /// <summary>
        /// Gets next departures from a specific stop
        /// </summary>
        /// <param name="stopId">Stop identifier</param>
        /// <param name="regionName">Region name (e.g., "uk_ireland")</param>
        /// <param name="countryIso">ISO country code (e.g., "GBR")</param>
        /// <param name="requestTime">Optional request time</param>
        /// <param name="results">Maximum number of results</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Next departures response data</returns>
        public Task<NextDeparturesResponse> GetNextDeparturesByStopAsync(
            string stopId,
            string regionName,
            string countryIso,
            DateTime? requestTime = null,
            int? results = null,
            CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>();
            
            if (requestTime.HasValue)
            {
                queryParams.Add("requestTime", requestTime.Value.ToString("o"));
            }
            
            if (results.HasValue)
            {
                queryParams.Add("results", results.Value.ToString());
            }
            
            return GetAsync<NextDeparturesResponse>(
                ApiEndpoints.NextDeparturesByStop(stopId, regionName, countryIso),
                queryParams,
                cancellationToken);
        }
        
        /// <summary>
        /// Gets next departures near a specific location
        /// </summary>
        /// <param name="lat">Latitude</param>
        /// <param name="lon">Longitude</param>
        /// <param name="radius">Search radius in meters (optional)</param>
        /// <param name="results">Maximum number of results (optional)</param>
        /// <param name="requestTime">Optional request time</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Next departures response data</returns>
        public Task<NextDeparturesResponse> GetNextDeparturesByLocationAsync(
            double lat,
            double lon,
            int? radius = null,
            int? results = null,
            DateTime? requestTime = null,
            CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>();
            
            if (requestTime.HasValue)
            {
                queryParams.Add("requestTime", requestTime.Value.ToString("o"));
            }
            
            // Note: radius and results are already in the base endpoint construction
            
            return GetAsync<NextDeparturesResponse>(
                ApiEndpoints.NextDeparturesByLocation(lat, lon, radius, results),
                null,
                cancellationToken);
        }
        
        /// <summary>
        /// Gets stops within a radius of a specific location
        /// </summary>
        /// <param name="lat">Latitude</param>
        /// <param name="lon">Longitude</param>
        /// <param name="radius">Search radius in meters</param>
        /// <param name="limit">Maximum number of stops to return (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stops in radius response data</returns>
        public Task<StopsInRadiusResponse> GetStopsInRadiusAsync(
            double lat,
            double lon,
            int radius,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            return GetAsync<StopsInRadiusResponse>(
                ApiEndpoints.StopsInRadiusQuery(lat, lon, radius, limit),
                null,
                cancellationToken);
        }
        
        /// <summary>
        /// Gets GTFS feeds downloads data
        /// </summary>
        /// <param name="countryIso">Optional ISO country code filter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>GTFS feeds downloads response data</returns>
        public Task<GtfsFeedsDownloadsResponse> GetGtfsFeedsDownloadsAsync(
            string countryIso = null,
            CancellationToken cancellationToken = default)
        {
            return GetAsync<GtfsFeedsDownloadsResponse>(
                ApiEndpoints.GtfsFeedsDownloadsQuery(countryIso),
                null,
                cancellationToken);
        }
        
        /// <summary>
        /// Gets pedestrian route between specified coordinates
        /// </summary>
        /// <param name="coordinates">List of coordinate pairs (lon,lat)</param>
        /// <param name="alternatives">Number of alternative routes (default: 0, max: 3)</param>
        /// <param name="steps">Include turn-by-turn instructions</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Pedestrian route response data</returns>
        public Task<PedestrianRouteResponse> GetPedestrianRouteAsync(
            List<(double lon, double lat)> coordinates,
            int? alternatives = null,
            bool? steps = null,
            CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>();
            
            if (alternatives.HasValue)
            {
                queryParams.Add("alternatives", alternatives.Value.ToString());
            }
            
            if (steps.HasValue)
            {
                queryParams.Add("steps", steps.Value.ToString().ToLowerInvariant());
            }
            
            return GetAsync<PedestrianRouteResponse>(
                ApiEndpoints.PedestrianRouteWithCoordinates(coordinates),
                queryParams,
                cancellationToken);
        }
        
        /// <summary>
        /// Gets pedestrian matrix between specified coordinates
        /// </summary>
        /// <param name="coordinates">List of coordinate pairs (lon,lat)</param>
        /// <param name="sources">Indices of source waypoints</param>
        /// <param name="destinations">Indices of destination waypoints</param>
        /// <param name="includeDistances">Whether to include distances in the response</param>
        /// <param name="includeDurations">Whether to include durations in the response</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Pedestrian matrix response data</returns>
        public Task<PedestrianMatrixResponse> GetPedestrianMatrixAsync(
            List<(double lon, double lat)> coordinates,
            List<int> sources = null,
            List<int> destinations = null,
            bool includeDistances = false,
            bool includeDurations = true,
            CancellationToken cancellationToken = default)
        {
            var queryParams = new Dictionary<string, string>();
            
            if (sources != null && sources.Count > 0)
            {
                queryParams.Add("sources", string.Join(";", sources));
            }
            
            if (destinations != null && destinations.Count > 0)
            {
                queryParams.Add("destinations", string.Join(";", destinations));
            }
            
            // Build annotations parameter
            var annotations = new List<string>();
            if (includeDurations) annotations.Add("duration");
            if (includeDistances) annotations.Add("distance");
            
            if (annotations.Count > 0)
            {
                queryParams.Add("annotations", string.Join(",", annotations));
            }
            
            return GetAsync<PedestrianMatrixResponse>(
                ApiEndpoints.PedestrianMatrixWithCoordinates(coordinates),
                queryParams,
                cancellationToken);
        }
    }
}
