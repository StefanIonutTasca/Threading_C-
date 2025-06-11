using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TransportTracker.Core.Services.Api.Transport.Models
{
    /// <summary>
    /// Response from the pedestrian route endpoint
    /// </summary>
    public class PedestrianRouteResponse
    {
        /// <summary>
        /// Status code of the request (e.g., "Ok", "NoRoute")
        /// </summary>
        [JsonPropertyName("code")]
        public string Code { get; set; }
        
        /// <summary>
        /// List of routes
        /// </summary>
        [JsonPropertyName("routes")]
        public List<PedestrianRoute> Routes { get; set; } = new List<PedestrianRoute>();
        
        /// <summary>
        /// List of waypoints used in the route calculation
        /// </summary>
        [JsonPropertyName("waypoints")]
        public List<Waypoint> Waypoints { get; set; } = new List<Waypoint>();
        
        /// <summary>
        /// Request processing time at server in milliseconds
        /// </summary>
        [JsonPropertyName("processingTimeMs")]
        public double ProcessingTimeMs { get; set; }
        
        /// <summary>
        /// Checks if the response is valid (has "Ok" status)
        /// </summary>
        /// <returns>True if the response is valid, otherwise false</returns>
        public bool IsValidResponse()
        {
            return Code?.ToLowerInvariant() == "ok";
        }
    }
    
    /// <summary>
    /// Pedestrian route information
    /// </summary>
    public class PedestrianRoute
    {
        /// <summary>
        /// Route summary information
        /// </summary>
        [JsonPropertyName("summary")]
        public RouteSummary Summary { get; set; }
        
        /// <summary>
        /// List of legs (segments) of the route
        /// </summary>
        [JsonPropertyName("legs")]
        public List<RouteLeg> Legs { get; set; } = new List<RouteLeg>();
        
        /// <summary>
        /// Geometry of the route
        /// </summary>
        [JsonPropertyName("geometry")]
        public string Geometry { get; set; }
        
        /// <summary>
        /// Total duration of the route in seconds
        /// </summary>
        [JsonPropertyName("duration")]
        public double Duration { get; set; }
        
        /// <summary>
        /// Total distance of the route in meters
        /// </summary>
        [JsonPropertyName("distance")]
        public double Distance { get; set; }
        
        /// <summary>
        /// Routes index for the route
        /// </summary>
        [JsonPropertyName("routesIndex")]
        public int RoutesIndex { get; set; }
    }
    
    /// <summary>
    /// Route summary information
    /// </summary>
    public class RouteSummary
    {
        /// <summary>
        /// Total duration of the route in seconds
        /// </summary>
        [JsonPropertyName("duration")]
        public double Duration { get; set; }
        
        /// <summary>
        /// Total distance of the route in meters
        /// </summary>
        [JsonPropertyName("distance")]
        public double Distance { get; set; }
    }
    
    /// <summary>
    /// Route leg (segment) information
    /// </summary>
    public class RouteLeg
    {
        /// <summary>
        /// Duration of the leg in seconds
        /// </summary>
        [JsonPropertyName("duration")]
        public double Duration { get; set; }
        
        /// <summary>
        /// Distance of the leg in meters
        /// </summary>
        [JsonPropertyName("distance")]
        public double Distance { get; set; }
        
        /// <summary>
        /// Summary of the leg
        /// </summary>
        [JsonPropertyName("summary")]
        public string Summary { get; set; }
        
        /// <summary>
        /// List of steps in this leg
        /// </summary>
        [JsonPropertyName("steps")]
        public List<RouteStep> Steps { get; set; } = new List<RouteStep>();
    }
    
    /// <summary>
    /// Step in a route leg
    /// </summary>
    public class RouteStep
    {
        /// <summary>
        /// Mode of travel (e.g., "walking")
        /// </summary>
        [JsonPropertyName("mode")]
        public string Mode { get; set; }
        
        /// <summary>
        /// Duration of the step in seconds
        /// </summary>
        [JsonPropertyName("duration")]
        public double Duration { get; set; }
        
        /// <summary>
        /// Distance of the step in meters
        /// </summary>
        [JsonPropertyName("distance")]
        public double Distance { get; set; }
        
        /// <summary>
        /// Name of the street or path
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        /// <summary>
        /// Type of maneuver (e.g., "turn", "depart", "arrive")
        /// </summary>
        [JsonPropertyName("maneuver")]
        public Maneuver Maneuver { get; set; }
        
        /// <summary>
        /// Geometry of the step
        /// </summary>
        [JsonPropertyName("geometry")]
        public string Geometry { get; set; }
    }
    
    /// <summary>
    /// Maneuver information
    /// </summary>
    public class Maneuver
    {
        /// <summary>
        /// Location coordinates of the maneuver [lon, lat]
        /// </summary>
        [JsonPropertyName("location")]
        public List<double> Location { get; set; } = new List<double>();
        
        /// <summary>
        /// Bearing before the maneuver
        /// </summary>
        [JsonPropertyName("bearingBefore")]
        public double BearingBefore { get; set; }
        
        /// <summary>
        /// Bearing after the maneuver
        /// </summary>
        [JsonPropertyName("bearingAfter")]
        public double BearingAfter { get; set; }
        
        /// <summary>
        /// Type of the maneuver (e.g., "turn", "depart", "arrive")
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        /// <summary>
        /// Direction of the maneuver (e.g., "left", "right", "straight")
        /// </summary>
        [JsonPropertyName("modifier")]
        public string Modifier { get; set; }
        
        /// <summary>
        /// Gets the longitude from the location
        /// </summary>
        public double GetLongitude()
        {
            return Location.Count > 0 ? Location[0] : 0;
        }
        
        /// <summary>
        /// Gets the latitude from the location
        /// </summary>
        public double GetLatitude()
        {
            return Location.Count > 1 ? Location[1] : 0;
        }
        
        /// <summary>
        /// Gets a formatted description of the maneuver
        /// </summary>
        /// <returns>A string describing the maneuver</returns>
        public string GetDescription()
        {
            if (string.IsNullOrEmpty(Type))
                return string.Empty;
                
            string description = Type;
            
            if (!string.IsNullOrEmpty(Modifier))
                description += $" {Modifier}";
                
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(description);
        }
    }
    
    /// <summary>
    /// Waypoint information
    /// </summary>
    public class Waypoint
    {
        /// <summary>
        /// Name of the waypoint
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        /// <summary>
        /// Location coordinates of the waypoint [lon, lat]
        /// </summary>
        [JsonPropertyName("location")]
        public List<double> Location { get; set; } = new List<double>();
        
        /// <summary>
        /// Gets the longitude from the location
        /// </summary>
        public double GetLongitude()
        {
            return Location.Count > 0 ? Location[0] : 0;
        }
        
        /// <summary>
        /// Gets the latitude from the location
        /// </summary>
        public double GetLatitude()
        {
            return Location.Count > 1 ? Location[1] : 0;
        }
    }
}
