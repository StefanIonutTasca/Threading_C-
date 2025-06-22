using System;

namespace TransportTracker.Core.Models
{
    /// <summary>
    /// Represents a real-time vehicle location with additional metadata
    /// </summary>
    public class VehicleLocation
    {
        /// <summary>
        /// Unique identifier for this vehicle location entry
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Vehicle identifier
        /// </summary>
        public string VehicleId { get; set; }

        /// <summary>
        /// Route identifier
        /// </summary>
        public string RouteId { get; set; }

        /// <summary>
        /// Trip identifier
        /// </summary>
        public string TripId { get; set; }

        /// <summary>
        /// Latitude coordinate
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Longitude coordinate
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Current bearing/heading in degrees (0-360, 0 = North)
        /// </summary>
        public double Bearing { get; set; }

        /// <summary>
        /// Speed in kilometers per hour
        /// </summary>
        public double Speed { get; set; }

        /// <summary>
        /// Timestamp when this location was recorded
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Optional status information
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Estimated occupancy level (0-100%)
        /// </summary>
        public int? OccupancyPercentage { get; set; }

        /// <summary>
        /// Next stop identifier
        /// </summary>
        public string NextStopId { get; set; }

        /// <summary>
        /// Whether the vehicle is currently at a stop
        /// </summary>
        public bool IsAtStop { get; set; }
    }
}
