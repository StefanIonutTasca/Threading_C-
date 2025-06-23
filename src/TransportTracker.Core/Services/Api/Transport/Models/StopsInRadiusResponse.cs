using System.Collections.Generic;
using System.Text.Json.Serialization;
using TransportTracker.Core.Models;

namespace TransportTracker.Core.Services.Api.Transport.Models
{
    /// <summary>
    /// Response from the stops in radius endpoint
    /// </summary>
    public class StopsInRadiusResponse
    {
        /// <summary>
        /// List of stops within the specified radius
        /// </summary>
        [JsonPropertyName("stops")]
        public List<StopInfo> Stops { get; set; } = new List<StopInfo>();
        
        /// <summary>
        /// Name of the region that processed the request
        /// </summary>
        [JsonPropertyName("regionName")]
        public string RegionName { get; set; }
        
        /// <summary>
        /// Request processing time at server in milliseconds
        /// </summary>
        [JsonPropertyName("processingTimeMs")]
        public double ProcessingTimeMs { get; set; }
    }
    
    /// <summary>
    /// Stop information
    /// </summary>
    public class StopInfo
    {
        /// <summary>
        /// Stop identifier
        /// </summary>
        [JsonPropertyName("stopId")]
        public string StopId { get; set; }
        
        /// <summary>
        /// Name of the stop
        /// </summary>
        [JsonPropertyName("stopName")]
        public string StopName { get; set; }
        
        /// <summary>
        /// Group type of the stop (e.g., "subway", "bus")
        /// </summary>
        [JsonPropertyName("stopTypeGroup")]
        public string StopTypeGroup { get; set; }
        
        /// <summary>
        /// Latitude of the stop
        /// </summary>
        [JsonPropertyName("stopLat")]
        public double StopLat { get; set; }
        
        /// <summary>
        /// Longitude of the stop
        /// </summary>
        [JsonPropertyName("stopLon")]
        public double StopLon { get; set; }
        
        /// <summary>
        /// Description of the stop
        /// </summary>
        [JsonPropertyName("stopDesc")]
        public string StopDesc { get; set; }
        
        /// <summary>
        /// Stop code (e.g., "OXC")
        /// </summary>
        [JsonPropertyName("stopCode")]
        public string StopCode { get; set; }
        
        /// <summary>
        /// Wheelchair boarding information (0=unknown, 1=accessible, 2=not accessible)
        /// </summary>
        [JsonPropertyName("wheelchairBoarding")]
        public int WheelchairBoarding { get; set; }
        
        /// <summary>
        /// Routes serving this stop
        /// </summary>
        [JsonPropertyName("routes")]
        public List<RouteInfo> Routes { get; set; } = new List<RouteInfo>();
        
        /// <summary>
        /// Converts this StopInfo to a domain Stop model
        /// </summary>
        /// <returns>A Stop domain model</returns>
        public Core.Models.Stop ToDomainStop()
        {
            var stop = new Core.Models.Stop
            {
                Id = StopId,
                Name = StopName,
                Code = StopCode,
                Description = StopDesc,
                Location = new Core.Models.Location
                {
                    Latitude = StopLat,
                    Longitude = StopLon,
                    Timestamp = System.DateTime.UtcNow
                },
                TransportTypes = new List<Core.Models.TransportType>(),
                IsAccessible = WheelchairBoarding == 1
            };

            // Convert the transport types from the routes
            foreach (var route in Routes)
            {
                Core.Models.TransportType transportType;
                if (System.Enum.TryParse(route.RouteType.ToUpper(), true, out transportType) && 
                    !stop.TransportTypes.Contains(transportType))
                {
                    stop.TransportTypes.Add(transportType);
                }
            }

            return stop;
        }
    }
    
    /// <summary>
    /// Route information
    /// </summary>
    public class RouteInfo
    {
        /// <summary>
        /// Route identifier
        /// </summary>
        [JsonPropertyName("routeId")]
        public string RouteId { get; set; }
        
        /// <summary>
        /// Short name of the route (e.g., "Central")
        /// </summary>
        [JsonPropertyName("routeShortName")]
        public string RouteShortName { get; set; }
        
        /// <summary>
        /// Long name of the route (e.g., "Central Line")
        /// </summary>
        [JsonPropertyName("routeLongName")]
        public string RouteLongName { get; set; }
        
        /// <summary>
        /// Route color in hex format (e.g., "dc241f")
        /// </summary>
        [JsonPropertyName("routeColor")]
        public string RouteColor { get; set; }
        
        /// <summary>
        /// Route text color in hex format (e.g., "ffffff")
        /// </summary>
        [JsonPropertyName("routeTextColor")]
        public string RouteTextColor { get; set; }
        
        /// <summary>
        /// Destination of the trip
        /// </summary>
        [JsonPropertyName("tripHeadsign")]
        public string TripHeadsign { get; set; }
        
        /// <summary>
        /// Type of route (e.g., "subway", "bus")
        /// </summary>
        [JsonPropertyName("routeType")]
        public string RouteType { get; set; }
        
        /// <summary>
        /// Converts this RouteInfo to a domain Route model
        /// </summary>
        /// <returns>A Route domain model</returns>
        public Core.Models.Route ToDomainRoute()
        {
            Core.Models.TransportType transportType;
            System.Enum.TryParse(RouteType.ToUpper(), true, out transportType);
            
            return new Core.Models.Route
            {
                Id = RouteId,
                Name = string.IsNullOrEmpty(RouteLongName) ? RouteShortName : RouteLongName,
                ShortName = RouteShortName,
                Description = TripHeadsign,
                TransportType = transportType.ToVehicleType(),
                Color = RouteColor
            };
        }
    }
}
