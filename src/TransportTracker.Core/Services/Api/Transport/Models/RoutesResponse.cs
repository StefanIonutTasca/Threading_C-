using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TransportTracker.Core.Services.Api.Transport.Models
{
    /// <summary>
    /// Response from the routes endpoint
    /// </summary>
    public class RoutesResponse
    {
        /// <summary>
        /// List of route options
        /// </summary>
        [JsonPropertyName("routes")]
        public List<Route> Routes { get; set; } = new List<Route>();
        
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
    /// Route information
    /// </summary>
    public class Route
    {
        /// <summary>
        /// Unique route identifier
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }
        
        /// <summary>
        /// Total duration in seconds
        /// </summary>
        [JsonPropertyName("duration")]
        public int Duration { get; set; }
        
        /// <summary>
        /// Number of transfers
        /// </summary>
        [JsonPropertyName("transfers")]
        public int Transfers { get; set; }
        
        /// <summary>
        /// Total walking distance in meters
        /// </summary>
        [JsonPropertyName("walkingDistance")]
        public int WalkingDistance { get; set; }
        
        /// <summary>
        /// Total walking duration in seconds
        /// </summary>
        [JsonPropertyName("walkingDuration")]
        public int WalkingDuration { get; set; }
        
        /// <summary>
        /// Estimated calories burned from walking
        /// </summary>
        [JsonPropertyName("walkingCalories")]
        public double WalkingCalories { get; set; }
        
        /// <summary>
        /// Estimated CO2 emission in kg
        /// </summary>
        [JsonPropertyName("co2EmissionKg")]
        public double Co2EmissionKg { get; set; }
        
        /// <summary>
        /// Estimated CO2 saved compared to car in kg
        /// </summary>
        [JsonPropertyName("co2SavedComparedToCarKg")]
        public double Co2SavedComparedToCarKg { get; set; }
        
        /// <summary>
        /// Sections of the route
        /// </summary>
        [JsonPropertyName("sections")]
        public List<RouteSection> Sections { get; set; } = new List<RouteSection>();
    }
    
    /// <summary>
    /// Section of a route
    /// </summary>
    public class RouteSection
    {
        /// <summary>
        /// Section identifier
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }
        
        /// <summary>
        /// Type of section (e.g., "pedestrian", "transit")
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        /// <summary>
        /// Travel summary information
        /// </summary>
        [JsonPropertyName("travelSummary")]
        public TravelSummary TravelSummary { get; set; }
        
        /// <summary>
        /// Departure information
        /// </summary>
        [JsonPropertyName("departure")]
        public DepartureArrival Departure { get; set; }
        
        /// <summary>
        /// Arrival information
        /// </summary>
        [JsonPropertyName("arrival")]
        public DepartureArrival Arrival { get; set; }
        
        /// <summary>
        /// Transport information
        /// </summary>
        [JsonPropertyName("transport")]
        public Transport Transport { get; set; }
        
        /// <summary>
        /// Encoded polyline string representing the route geometry
        /// </summary>
        [JsonPropertyName("polyline")]
        public string Polyline { get; set; }
    }
    
    /// <summary>
    /// Travel summary information
    /// </summary>
    public class TravelSummary
    {
        /// <summary>
        /// Duration in seconds
        /// </summary>
        [JsonPropertyName("duration")]
        public int Duration { get; set; }
        
        /// <summary>
        /// Length in meters
        /// </summary>
        [JsonPropertyName("length")]
        public int Length { get; set; }
    }
    
    /// <summary>
    /// Departure or arrival information
    /// </summary>
    public class DepartureArrival
    {
        /// <summary>
        /// Time of departure or arrival
        /// </summary>
        [JsonPropertyName("time")]
        public string Time { get; set; }
        
        /// <summary>
        /// Place of departure or arrival
        /// </summary>
        [JsonPropertyName("place")]
        public Place Place { get; set; }
    }
    
    /// <summary>
    /// Place information
    /// </summary>
    public class Place
    {
        /// <summary>
        /// Name of the place
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        /// <summary>
        /// Type of place (e.g., "place", "station")
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        /// <summary>
        /// Location coordinates
        /// </summary>
        [JsonPropertyName("location")]
        public Location Location { get; set; }
    }
    
    /// <summary>
    /// Location coordinates
    /// </summary>
    public class Location
    {
        /// <summary>
        /// Latitude
        /// </summary>
        [JsonPropertyName("lat")]
        public double Lat { get; set; }
        
        /// <summary>
        /// Longitude
        /// </summary>
        [JsonPropertyName("lng")]
        public double Lng { get; set; }
    }
    
    /// <summary>
    /// Transport information
    /// </summary>
    public class Transport
    {
        /// <summary>
        /// Transport mode (e.g., "pedestrian", "bus", "subway")
        /// </summary>
        [JsonPropertyName("mode")]
        public string Mode { get; set; }
    }
}
