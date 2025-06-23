using System;
using System.Collections.Generic;

namespace TransportTracker.Core.Models
{
    /// <summary>
    /// Represents a pedestrian route between two or more points
    /// </summary>
    public class PedestrianRoute
{
    /// <summary>
    /// Encoded polyline string representing the route geometry
    /// </summary>
    public string EncodedPolyline { get; set; } = string.Empty;

    /// <summary>
    /// Turn-by-turn steps of the route
    /// </summary>
    public List<RouteStep> Steps { get; set; } = new List<RouteStep>();

    /// <summary>
    /// Total duration of the route
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Total distance of the route in meters
    /// </summary>
    public double Distance { get; set; }

    /// <summary>
    /// Gets the average speed in kilometers per hour
    /// </summary>
    /// <returns>Average speed in km/h or 0 if duration is zero</returns>
    public double GetAverageSpeedKmh()
    {
        if (Duration.TotalHours > 0)
        {
            return Distance / 1000 / Duration.TotalHours;
        }
        return 0;
    }

    /// <summary>
    /// Gets the formatted duration (e.g., "15 min")
    /// </summary>
    /// <returns>Formatted duration string</returns>
    public string GetFormattedDuration()
    {
        if (Duration.TotalHours >= 1)
        {
            return $"{Duration.TotalHours:F1} hours";
        }
        return $"{Duration.TotalMinutes:F0} min";
    }

    /// <summary>
    /// Gets the formatted distance (e.g., "1.2 km" or "350 m")
    /// </summary>
    /// <returns>Formatted distance string</returns>
    public string GetFormattedDistance()
    {
        if (Distance >= 1000)
        {
            return $"{Distance / 1000:F1} km";
        }
        return $"{Distance:F0} m";
    }

    /// <summary>
    /// Creates a deep copy of this route
    /// </summary>
    /// <returns>A new PedestrianRoute with the same properties</returns>
    public PedestrianRoute Clone()
    {
        var clone = new PedestrianRoute
        {
            Duration = this.Duration,
            Distance = this.Distance,
            EncodedPolyline = this.EncodedPolyline
        };
        if (Steps != null)
        {
            clone.Steps = Steps.Select(step => step.Clone()).ToList();
        }
        return clone;
    }
}


}