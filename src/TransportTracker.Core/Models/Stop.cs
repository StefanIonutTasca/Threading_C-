using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TransportTracker.Core.Models
{
    /// <summary>
    /// Represents a public transport stop or station
    /// </summary>
    public class Stop : BaseEntity
    {
        // Id property inherited from BaseEntity

        /// <summary>
        /// Name of the stop (e.g., "Central Station", "Main Street")
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        /// <summary>
        /// Description of the stop
        /// </summary>
        [MaxLength(500)]
        public string Description { get; set; }

        /// <summary>
        /// Geographical position - latitude
        /// </summary>
        [Required]
        public double Latitude { get; set; }

        /// <summary>
        /// Geographical position - longitude
        /// </summary>
        [Required]
        public double Longitude { get; set; }

        /// <summary>
        /// Zone or region identifier where the stop is located
        /// </summary>
        [MaxLength(20)]
        public string Zone { get; set; }

        /// <summary>
        /// Address of the stop
        /// </summary>
        [MaxLength(200)]
        public string Address { get; set; }

        /// <summary>
        /// Routes that go through this stop
        /// </summary>
        public List<RouteStop> RouteStops { get; set; } = new List<RouteStop>();

        /// <summary>
        /// Whether the stop has a shelter
        /// </summary>
        public bool HasShelter { get; set; }

        /// <summary>
        /// Whether the stop has seating
        /// </summary>
        public bool HasSeating { get; set; }

        /// <summary>
        /// Whether the stop is wheelchair accessible
        /// </summary>
        public bool IsAccessible { get; set; }

        /// <summary>
        /// Whether the stop has real-time information display
        /// </summary>
        public bool HasRealtimeInfo { get; set; }

        /// <summary>
        /// Type of the stop (bus stop, train station, etc.)
        /// </summary>
        public StopType Type { get; set; }
        
        /// <summary>
        /// Last time the stop information was updated
        /// </summary>
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Enumeration of possible stop types
    /// </summary>
    public enum StopType
    {
        BusStop,
        TrainStation,
        TramStop,
        MetroStation,
        FerryTerminal,
        TaxiStand,
        Other
    }
}
