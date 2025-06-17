using System;
using System.Collections.Generic;

namespace TransportTracker.App.Models.Statistics
{
    /// <summary>
    /// Collection of various transport metrics
    /// </summary>
    public class TransportMetricsCollection
    {
        /// <summary>
        /// Statistics by transport type (bus, train, etc.)
        /// </summary>
        public List<TransportTypeMetric> TransportTypeMetrics { get; set; } = new List<TransportTypeMetric>();
        
        /// <summary>
        /// Most popular routes by passenger count
        /// </summary>
        public List<RoutePopularityMetric> PopularRoutes { get; set; } = new List<RoutePopularityMetric>();
        
        /// <summary>
        /// Vehicle activity over time (hourly)
        /// </summary>
        public List<TimeSeriesDataPoint> ActivityByHour { get; set; } = new List<TimeSeriesDataPoint>();
        
        /// <summary>
        /// Overall system summary metrics
        /// </summary>
        public TransportSystemSummary SystemSummary { get; set; } = new TransportSystemSummary();
        
        /// <summary>
        /// Timestamp when these metrics were calculated
        /// </summary>
        public DateTime CalculatedAt { get; set; } = DateTime.Now;
    }
    
    /// <summary>
    /// Statistics for a particular transport type
    /// </summary>
    public class TransportTypeMetric
    {
        /// <summary>
        /// Type of transport (Bus, Train, etc.)
        /// </summary>
        public string Type { get; set; }
        
        /// <summary>
        /// Total number of vehicles of this type
        /// </summary>
        public int VehicleCount { get; set; }
        
        /// <summary>
        /// Average speed in km/h
        /// </summary>
        public double AverageSpeed { get; set; }
        
        /// <summary>
        /// Average occupancy percentage
        /// </summary>
        public double AverageOccupancy { get; set; }
        
        /// <summary>
        /// Percentage of vehicles that are delayed
        /// </summary>
        public double DelayedPercentage { get; set; }
        
        /// <summary>
        /// Average delay in minutes
        /// </summary>
        public double AverageDelay { get; set; }
        
        /// <summary>
        /// Color associated with this transport type
        /// </summary>
        public string Color { get; set; }
    }
    
    /// <summary>
    /// Information about route popularity
    /// </summary>
    public class RoutePopularityMetric
    {
        /// <summary>
        /// Route identifier
        /// </summary>
        public string RouteId { get; set; }
        
        /// <summary>
        /// Route name
        /// </summary>
        public string RouteName { get; set; }
        
        /// <summary>
        /// Transport type for this route
        /// </summary>
        public string TransportType { get; set; }
        
        /// <summary>
        /// Total estimated passenger count
        /// </summary>
        public int PassengerCount { get; set; }
        
        /// <summary>
        /// Number of vehicles currently servicing this route
        /// </summary>
        public int VehicleCount { get; set; }
        
        /// <summary>
        /// Average occupancy percentage across vehicles on this route
        /// </summary>
        public double AverageOccupancy { get; set; }
        
        /// <summary>
        /// Color associated with this route
        /// </summary>
        public string Color { get; set; }
    }
    
    /// <summary>
    /// Data point for time series visualizations
    /// </summary>
    public class TimeSeriesDataPoint
    {
        /// <summary>
        /// Timestamp for this data point
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Hour of day (0-23)
        /// </summary>
        public int Hour { get; set; }
        
        /// <summary>
        /// Value for this data point
        /// </summary>
        public double Value { get; set; }
        
        /// <summary>
        /// Category/label for this data point (optional)
        /// </summary>
        public string Category { get; set; }
    }
    
    /// <summary>
    /// Overall system-wide transport metrics
    /// </summary>
    public class TransportSystemSummary
    {
        /// <summary>
        /// Total number of active vehicles
        /// </summary>
        public int TotalVehicles { get; set; }
        
        /// <summary>
        /// Total number of routes
        /// </summary>
        public int TotalRoutes { get; set; }
        
        /// <summary>
        /// Total number of stops
        /// </summary>
        public int TotalStops { get; set; }
        
        /// <summary>
        /// Estimated total passenger count across system
        /// </summary>
        public int TotalPassengers { get; set; }
        
        /// <summary>
        /// Average occupancy across all vehicles
        /// </summary>
        public double SystemOccupancy { get; set; }
        
        /// <summary>
        /// Percentage of vehicles on time
        /// </summary>
        public double OnTimePerformance { get; set; }
        
        /// <summary>
        /// System efficiency score (0-100)
        /// </summary>
        public double EfficiencyScore { get; set; }
    }
}
