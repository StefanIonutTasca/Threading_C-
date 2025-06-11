namespace TransportTracker.Core.Services.Api
{
    /// <summary>
    /// Constants for API endpoints - Busmaps GTFS API
    /// </summary>
    public static class ApiEndpoints
    {
        /// <summary>
        /// Base URL for the Busmaps API
        /// </summary>
        public const string BaseBusmapsUrl = "https://capi.busmaps.com:8443";
        
        /// <summary>
        /// Get public transit routes between two points
        /// </summary>
        public const string Routes = "routes";
        
        /// <summary>
        /// Get real-time departure information for transit stops
        /// </summary>
        public const string NextDepartures = "nextDepartures";
        
        /// <summary>
        /// Find transit stops within a specified radius of a location
        /// </summary>
        public const string StopsInRadius = "stopsInRadius";
        
        /// <summary>
        /// Access GTFS feed catalog
        /// </summary>
        public const string GtfsFeedsDownloads = "getGtfsFeedsDownloads";
        
        /// <summary>
        /// Get detailed walking routes with turn-by-turn navigation support
        /// </summary>
        public const string PedestrianRoute = "pedestrian/route";
        
        /// <summary>
        /// Get distance/duration matrices between multiple pedestrian waypoints
        /// </summary>
        public const string PedestrianMatrix = "pedestrian/matrix";

        /// <summary>
        /// Create a routes endpoint with origin and destination coordinates
        /// </summary>
        /// <param name="originLat">Origin latitude</param>
        /// <param name="originLon">Origin longitude</param>
        /// <param name="destLat">Destination latitude</param>
        /// <param name="destLon">Destination longitude</param>
        /// <returns>The endpoint with query parameters</returns>
        public static string RoutesQuery(double originLat, double originLon, double destLat, double destLon) => 
            $"{Routes}?origin={originLat},{originLon}&destination={destLat},{destLon}";
        
        /// <summary>
        /// Create a next departures endpoint with stop ID and region details
        /// </summary>
        /// <param name="stopId">Stop identifier</param>
        /// <param name="regionName">Region name (e.g., "uk_ireland")</param>
        /// <param name="countryIso">ISO country code (e.g., "GBR")</param>
        /// <returns>The endpoint with query parameters</returns>
        public static string NextDeparturesByStop(string stopId, string regionName, string countryIso) => 
            $"{NextDepartures}?stopId={stopId}&regionName={regionName}&countryIso={countryIso}";
        
        /// <summary>
        /// Create a next departures endpoint with location coordinates
        /// </summary>
        /// <param name="lat">Latitude</param>
        /// <param name="lon">Longitude</param>
        /// <param name="radius">Search radius in meters (optional)</param>
        /// <param name="results">Maximum number of results (optional)</param>
        /// <returns>The endpoint with query parameters</returns>
        public static string NextDeparturesByLocation(double lat, double lon, int? radius = null, int? results = null)
        {
            var endpoint = $"{NextDepartures}?location={lat},{lon}";
            
            if (radius.HasValue)
                endpoint += $"&radius={radius}";
                
            if (results.HasValue)
                endpoint += $"&results={results}";
                
            return endpoint;
        }
        
        /// <summary>
        /// Create a stops in radius endpoint
        /// </summary>
        /// <param name="lat">Latitude</param>
        /// <param name="lon">Longitude</param>
        /// <param name="radius">Search radius in meters</param>
        /// <param name="limit">Maximum number of stops to return (optional)</param>
        /// <returns>The endpoint with query parameters</returns>
        public static string StopsInRadiusQuery(double lat, double lon, int radius, int? limit = null)
        {
            var endpoint = $"{StopsInRadius}?lat={lat}&lon={lon}&radius={radius}";
            
            if (limit.HasValue)
                endpoint += $"&limit={limit}";
                
            return endpoint;
        }
        
        /// <summary>
        /// Create a GTFS feeds downloads endpoint with optional country filter
        /// </summary>
        /// <param name="countryIso">ISO country code (optional)</param>
        /// <returns>The endpoint with query parameters</returns>
        public static string GtfsFeedsDownloadsQuery(string countryIso = null)
        {
            return string.IsNullOrEmpty(countryIso) 
                ? GtfsFeedsDownloads 
                : $"{GtfsFeedsDownloads}?countryIso={countryIso}";
        }
        
        /// <summary>
        /// Create a pedestrian route endpoint with coordinates
        /// </summary>
        /// <param name="coordinates">List of coordinate pairs (lon,lat)</param>
        /// <returns>The endpoint with coordinates</returns>
        public static string PedestrianRouteWithCoordinates(List<(double lon, double lat)> coordinates)
        {
            if (coordinates == null || coordinates.Count < 2)
                throw new ArgumentException("At least two coordinates are required for pedestrian routing");
                
            var coordString = string.Join(";", coordinates.Select(c => $"{c.lon},{c.lat}"));
            return $"{PedestrianRoute}/{coordString}";
        }
        
        /// <summary>
        /// Create a pedestrian matrix endpoint with coordinates
        /// </summary>
        /// <param name="coordinates">List of coordinate pairs (lon,lat)</param>
        /// <returns>The endpoint with coordinates</returns>
        public static string PedestrianMatrixWithCoordinates(List<(double lon, double lat)> coordinates)
        {
            if (coordinates == null || coordinates.Count < 2)
                throw new ArgumentException("At least two coordinates are required for pedestrian matrix");
                
            var coordString = string.Join(";", coordinates.Select(c => $"{c.lon},{c.lat}"));
            return $"{PedestrianMatrix}/{coordString}";
        }
    }
}
