using System;
using System.ComponentModel.DataAnnotations;

namespace TransportTracker.Core.Models
{
    /// <summary>
    /// Represents a public transport vehicle like a bus, train, or tram
    /// </summary>
    public class Vehicle : BaseEntity
    {
        // Id property inherited from BaseEntity

        /// <summary>
        /// The registration number or other external identifier for the vehicle
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string RegistrationNumber { get; set; }

        /// <summary>
        /// Type of vehicle (Bus, Train, Tram, etc.)
        /// </summary>
        [Required]
        public VehicleType Type { get; set; }

        /// <summary>
        /// Current status of the vehicle
        /// </summary>
        public VehicleStatus Status { get; set; }

        /// <summary>
        /// Current geographical position (latitude)
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Current geographical position (longitude)
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Current bearing/heading in degrees (0-360)
        /// </summary>
        public double Bearing { get; set; }

        /// <summary>
        /// Current speed in kilometers per hour
        /// </summary>
        public double Speed { get; set; }

        /// <summary>
        /// Last time the vehicle data was updated
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Reference to the route this vehicle is currently servicing
        /// </summary>
        public string RouteId { get; set; }
        
        /// <summary>
        /// Navigation property for the route
        /// </summary>
        public Route Route { get; set; }

        /// <summary>
        /// Maximum passenger capacity of the vehicle
        /// </summary>
        public int Capacity { get; set; }

        /// <summary>
        /// Estimated occupancy percentage (0-100)
        /// </summary>
        [Range(0, 100)]
        public int OccupancyPercentage { get; set; }

        /// <summary>
        /// Whether the vehicle is accessible for wheelchairs
        /// </summary>
        public bool IsAccessible { get; set; }

        /// <summary>
        /// Whether the vehicle has WiFi available
        /// </summary>
        public bool HasWifi { get; set; }
    }

    /// <summary>
    /// Enumeration of possible vehicle types
    /// </summary>
    public enum VehicleType
    {
        Bus,
        Train,
        Tram,
        Metro,
        Ferry,
        Taxi,
        Other
    }

    /// <summary>
    /// Enumeration of possible vehicle statuses
    /// </summary>
    public enum VehicleStatus
    {
        InService,
        OutOfService,
        Delayed,
        Stopped,
        NotTracked
    }
}
