using System;

namespace TransportTracker.Core.Models
{
    /// <summary>
    /// Represents a single step in a route or pedestrian route
    /// </summary>
    public class RouteStep
    {
        /// <summary>
        /// Human-readable instruction for this step (e.g. "Turn left onto Main St.")
        /// </summary>
        public string Instruction { get; set; }

        /// <summary>
        /// Distance of this step in meters
        /// </summary>
        public double Distance { get; set; }

        /// <summary>
        /// Duration of this step
        /// </summary>
        public TimeSpan Duration { get; set; }

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
        /// Creates a deep copy of this step
        /// </summary>
        /// <returns>A new RouteStep with the same properties</returns>
        public RouteStep Clone()
        {
            return new RouteStep
            {
                Instruction = this.Instruction,
                Distance = this.Distance,
                Duration = this.Duration
            };
        }
    }
}
