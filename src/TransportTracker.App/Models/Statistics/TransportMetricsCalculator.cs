using System;
using System.Collections.Generic;
using System.Linq;
using TransportTracker.App.Core.Diagnostics;

using TransportTracker.App.Views.Maps;
using TransportTracker.App.Views.Maps.Overlays;

namespace TransportTracker.App.Models.Statistics
{
    /// <summary>
    /// Calculates various metrics and statistics from transport data
    /// </summary>
    public class TransportMetricsCalculator
    {
        private static readonly string[] TransportTypeColors = new[]
        {
            "#FF4757", // Bus - Red
            "#3742FA", // Train - Blue
            "#2ED573", // Tram - Green
            "#FF6B81", // Subway - Pink
            "#1E90FF"  // Ferry - Light Blue
        };
        
        /// <summary>
        /// Calculate a complete set of transport metrics from the provided data
        /// </summary>
        public TransportMetricsCollection CalculateMetrics(
            List<TransportVehicle> vehicles,
            List<RouteInfo> routes,
            List<TransportStop> stops)
        {
            using (PerformanceMonitor.Instance.StartOperation("CalculateTransportMetrics"))
            {
                try
                {
                    var result = new TransportMetricsCollection();
                    
                    // Skip calculations if there's no data
                    if (vehicles == null || !vehicles.Any())
                    {
                        return result;
                    }
                    
                    // Ensure routes and stops are initialized for safety
                    routes ??= new List<RouteInfo>();
                    stops ??= new List<TransportStop>();
                    
                    // Calculate transport type metrics
                    result.TransportTypeMetrics = CalculateTransportTypeMetrics(vehicles);
                    
                    // Calculate route popularity metrics
                    result.PopularRoutes = CalculateRoutePopularity(vehicles, routes);
                    
                    // Calculate activity by hour
                    result.ActivityByHour = CalculateActivityByHour(vehicles);
                    
                    // Calculate system summary
                    result.SystemSummary = CalculateSystemSummary(vehicles, routes, stops);
                    
                    return result;
                }
                catch (Exception ex)
                {
                    PerformanceMonitor.Instance.RecordFailure("CalculateTransportMetrics", ex);
                    System.Diagnostics.Debug.WriteLine($"Error calculating transport metrics: {ex.Message}");
                    return new TransportMetricsCollection();
                }
            }
        }
        
        /// <summary>
        /// Calculate metrics segmented by transport type
        /// </summary>
        private List<TransportTypeMetric> CalculateTransportTypeMetrics(List<TransportVehicle> vehicles)
        {
            var result = new List<TransportTypeMetric>();
            var typeGroups = vehicles.GroupBy(v => v.Type);
            int colorIndex = 0;
            
            foreach (var group in typeGroups)
            {
                string transportType = group.Key;
                var vehiclesInGroup = group.ToList();
                
                // Calculate metrics
                double avgSpeed = vehiclesInGroup.Average(v => v.Speed);
                double avgOccupancy = vehiclesInGroup.Average(v => v.Occupancy);
                int delayedCount = vehiclesInGroup.Count(v => v.DelayMinutes > 0);
                double delayedPct = (double)delayedCount / vehiclesInGroup.Count * 100;
                double avgDelay = vehiclesInGroup.Where(v => v.DelayMinutes > 0).DefaultIfEmpty().Average(v => v?.DelayMinutes ?? 0);
                
                // Assign a color based on type or from our default colors
                string color = GetColorForTransportType(transportType, colorIndex++);
                
                result.Add(new TransportTypeMetric
                {
                    Type = transportType,
                    VehicleCount = vehiclesInGroup.Count,
                    AverageSpeed = Math.Round(avgSpeed, 1),
                    AverageOccupancy = Math.Round(avgOccupancy, 1),
                    DelayedPercentage = Math.Round(delayedPct, 1),
                    AverageDelay = Math.Round(avgDelay, 1),
                    Color = color
                });
            }
            
            return result.OrderByDescending(m => m.VehicleCount).ToList();
        }
        
        /// <summary>
        /// Calculate route popularity metrics
        /// </summary>
        private List<RoutePopularityMetric> CalculateRoutePopularity(
            List<TransportVehicle> vehicles, 
            List<RouteInfo> routes)
        {
            var result = new List<RoutePopularityMetric>();
            var routeDict = routes.ToDictionary(r => r.Id, r => r);
            var vehiclesByRoute = vehicles.GroupBy(v => v.RouteId);
            
            foreach (var group in vehiclesByRoute)
            {
                string routeId = group.Key;
                var vehiclesOnRoute = group.ToList();
                RouteInfo routeInfo = null;
                
                // Try to get route info if available
                if (routeDict.ContainsKey(routeId))
                {
                    routeInfo = routeDict[routeId];
                }
                
                // Calculate passenger count (estimated from occupancy)
                int totalPassengers = (int)vehiclesOnRoute.Sum(v => v.Occupancy * 0.8); // Assuming average capacity
                double avgOccupancy = vehiclesOnRoute.Average(v => v.Occupancy);
                
                result.Add(new RoutePopularityMetric
                {
                    RouteId = routeId,
                    RouteName = routeInfo?.Name ?? $"Route {routeId}",
                    TransportType = routeInfo?.Type ?? vehiclesOnRoute.FirstOrDefault()?.Type ?? "Unknown",
                    PassengerCount = totalPassengers,
                    VehicleCount = vehiclesOnRoute.Count,
                    AverageOccupancy = Math.Round(avgOccupancy, 1),
                    Color = !string.IsNullOrWhiteSpace(routeInfo?.Color)
    ? routeInfo.Color
    : "#CCCCCC"
                });
            }
            
            return result.OrderByDescending(r => r.PassengerCount).ToList();
        }
        
        /// <summary>
        /// Calculate activity metrics by hour
        /// </summary>
        private List<TimeSeriesDataPoint> CalculateActivityByHour(List<TransportVehicle> vehicles)
        {
            var result = new List<TimeSeriesDataPoint>();
            
            // Group vehicles by the hour of their last update
            var hourGroups = vehicles
                .GroupBy(v => v.LastUpdated.Hour)
                .OrderBy(g => g.Key);
                
            foreach (var group in hourGroups)
            {
                int hour = group.Key;
                int count = group.Count();
                
                result.Add(new TimeSeriesDataPoint
                {
                    Hour = hour,
                    Timestamp = DateTime.Today.AddHours(hour),
                    Value = count,
                    Category = $"{hour:D2}:00"
                });
            }
            
            // Fill in any missing hours with zero values
            for (int hour = 0; hour < 24; hour++)
            {
                if (!result.Any(p => p.Hour == hour))
                {
                    result.Add(new TimeSeriesDataPoint
                    {
                        Hour = hour,
                        Timestamp = DateTime.Today.AddHours(hour),
                        Value = 0,
                        Category = $"{hour:D2}:00"
                    });
                }
            }
            
            return result.OrderBy(p => p.Hour).ToList();
        }
        
        /// <summary>
        /// Calculate overall system summary metrics
        /// </summary>
        private TransportSystemSummary CalculateSystemSummary(
            List<TransportVehicle> vehicles,
            List<RouteInfo> routes,
            List<TransportStop> stops)
        {
            // Count unique routes represented in vehicles
            var uniqueRouteIds = vehicles.Select(v => v.RouteId).Distinct().ToList();
            int routeCount = Math.Max(uniqueRouteIds.Count, routes.Count);
            
            // Calculate occupancy metrics
            double systemOccupancy = vehicles.Average(v => v.Occupancy);
            
            // Calculate on-time performance
            int onTimeCount = vehicles.Count(v => v.DelayMinutes <= 2); // Consider 2 min or less as on-time
            double onTimePct = (double)onTimeCount / vehicles.Count * 100;
            
            // Calculate efficiency score based on multiple factors
            double occupancyScore = systemOccupancy * 0.6; // 0-60 points based on occupancy
            double delayScore = onTimePct * 0.4 / 100; // 0-40 points based on on-time performance
            double efficiencyScore = occupancyScore + delayScore;
            
            // Estimate total passengers (rough estimate)
            int totalPassengers = (int)vehicles.Sum(v => v.Occupancy * 0.8);
            
            return new TransportSystemSummary
            {
                TotalVehicles = vehicles.Count,
                TotalRoutes = routeCount,
                TotalStops = stops.Count,
                TotalPassengers = totalPassengers,
                SystemOccupancy = Math.Round(systemOccupancy, 1),
                OnTimePerformance = Math.Round(onTimePct, 1),
                EfficiencyScore = Math.Round(efficiencyScore, 1)
            };
        }
        
        /// <summary>
        /// Get a color for a transport type
        /// </summary>
        private string GetColorForTransportType(string transportType, int index)
        {
            switch (transportType.ToLower())
            {
                case "bus":
                    return "#FF4757";
                case "train":
                    return "#3742FA";
                case "tram":
                    return "#2ED573";
                case "subway":
                    return "#FF6B81";
                case "ferry":
                    return "#1E90FF";
                default:
                    // Use modulo to cycle through colors for unknown types
                    return TransportTypeColors[index % TransportTypeColors.Length];
            }
        }
    }
}
