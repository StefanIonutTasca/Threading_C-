using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TransportTracker.Core.Services.Api.Transport.Models
{
    /// <summary>
    /// Response from the next departures endpoint
    /// </summary>
    public class NextDeparturesResponse
    {
        /// <summary>
        /// Request time in Unix timestamp
        /// </summary>
        [JsonPropertyName("requestTime")]
        public long RequestTime { get; set; }
        
        /// <summary>
        /// Local date in format "YYYY-MM-DD"
        /// </summary>
        [JsonPropertyName("localDate")]
        public string LocalDate { get; set; }
        
        /// <summary>
        /// Local time in format "HH:MM:SS"
        /// </summary>
        [JsonPropertyName("localTime")]
        public string LocalTime { get; set; }
        
        /// <summary>
        /// List of stop departures
        /// </summary>
        [JsonPropertyName("stopDepartures")]
        public List<StopDeparture> StopDepartures { get; set; } = new List<StopDeparture>();
        
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
        
        /// <summary>
        /// Converts the request time from Unix timestamp to DateTime
        /// </summary>
        /// <returns>DateTime representation of the request time</returns>
        public DateTime GetRequestDateTime()
        {
            return DateTimeOffset.FromUnixTimeSeconds(RequestTime).DateTime;
        }
    }
    
    /// <summary>
    /// Stop departure information
    /// </summary>
    public class StopDeparture
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
        /// List of departures from this stop
        /// </summary>
        [JsonPropertyName("departureList")]
        public List<Departure> DepartureList { get; set; } = new List<Departure>();
        
        /// <summary>
        /// List of transit agencies serving this stop
        /// </summary>
        [JsonPropertyName("agencies")]
        public List<Agency> Agencies { get; set; } = new List<Agency>();
        
        /// <summary>
        /// Gets a location object representing the stop's coordinates
        /// </summary>
        /// <returns>A Location object with the stop's coordinates</returns>
        public Models.Core.Location ToLocation()
        {
            return new Models.Core.Location
            {
                Latitude = StopLat,
                Longitude = StopLon,
                Timestamp = DateTime.UtcNow
            };
        }
    }
    
    /// <summary>
    /// Departure information
    /// </summary>
    public class Departure
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
        /// Type of route (e.g., "subway", "bus")
        /// </summary>
        [JsonPropertyName("routeType")]
        public string RouteType { get; set; }
        
        /// <summary>
        /// Destination of the trip
        /// </summary>
        [JsonPropertyName("tripHeadsign")]
        public string TripHeadsign { get; set; }
        
        /// <summary>
        /// Departure time in format "HH:MM:SS"
        /// </summary>
        [JsonPropertyName("departureTime")]
        public string DepartureTime { get; set; }
        
        /// <summary>
        /// Date of the departure in format "YYYY-MM-DD"
        /// </summary>
        [JsonPropertyName("date")]
        public string Date { get; set; }
        
        /// <summary>
        /// Route color in hex format (e.g., "dc241f")
        /// </summary>
        [JsonPropertyName("routeColor")]
        public string RouteColor { get; set; }
        
        /// <summary>
        /// Agency identifier
        /// </summary>
        [JsonPropertyName("agencyId")]
        public string AgencyId { get; set; }
        
        /// <summary>
        /// Gets a DateTime representation of the departure time
        /// </summary>
        /// <returns>DateTime representation of the departure time</returns>
        public DateTime GetDepartureDateTime()
        {
            return DateTime.Parse($"{Date} {DepartureTime}");
        }
        
        /// <summary>
        /// Converts the route color from hex string to a System.Drawing.Color
        /// </summary>
        /// <returns>Color representation of the route color</returns>
        public string GetRouteColorHex()
        {
            return !string.IsNullOrEmpty(RouteColor) ? $"#{RouteColor}" : "#000000";
        }
    }
    
    /// <summary>
    /// Transit agency information
    /// </summary>
    public class Agency
    {
        /// <summary>
        /// Agency identifier
        /// </summary>
        [JsonPropertyName("agencyId")]
        public string AgencyId { get; set; }
        
        /// <summary>
        /// Name of the agency
        /// </summary>
        [JsonPropertyName("agencyName")]
        public string AgencyName { get; set; }
        
        /// <summary>
        /// URL of the agency's website
        /// </summary>
        [JsonPropertyName("agencyUrl")]
        public string AgencyUrl { get; set; }
    }
    
    /// <summary>
    /// Namespace for core model types to avoid naming conflicts
    /// </summary>
    namespace Models.Core
    {
        /// <summary>
        /// Location coordinates (to avoid naming conflict with the Location class in RoutesResponse)
        /// </summary>
        public class Location
        {
            /// <summary>
            /// Latitude
            /// </summary>
            public double Latitude { get; set; }
            
            /// <summary>
            /// Longitude
            /// </summary>
            public double Longitude { get; set; }
            
            /// <summary>
            /// Timestamp of when the location was recorded
            /// </summary>
            public DateTime Timestamp { get; set; }
        }
    }
}
