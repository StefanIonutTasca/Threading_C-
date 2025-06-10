using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TransportTracker.Core.Models
{
    /// <summary>
    /// Represents a public transport route (e.g. bus line, train route)
    /// </summary>
    public class Route : BaseEntity
    {
        // Id property inherited from BaseEntity

        /// <summary>
        /// Human-readable name or number of the route (e.g., "Route 42", "Blue Line")
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Name { get; set; }

        /// <summary>
        /// Short description of the route
        /// </summary>
        [MaxLength(200)]
        public string Description { get; set; }

        /// <summary>
        /// Type of transport used on this route
        /// </summary>
        [Required]
        public VehicleType TransportType { get; set; }

        /// <summary>
        /// Color associated with the route (hex code, e.g., "#FF0000" for red)
        /// </summary>
        [MaxLength(10)]
        public string Color { get; set; }

        /// <summary>
        /// Text color for use on top of the route color (for contrast)
        /// </summary>
        [MaxLength(10)]
        public string TextColor { get; set; }

        /// <summary>
        /// Starting point of the route (usually a terminal station/stop)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Origin { get; set; }

        /// <summary>
        /// End point of the route (usually a terminal station/stop)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Destination { get; set; }

        /// <summary>
        /// All stops along this route
        /// </summary>
        public List<RouteStop> RouteStops { get; set; } = new List<RouteStop>();
        
        /// <summary>
        /// All vehicles currently operating on this route
        /// </summary>
        public List<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
        
        /// <summary>
        /// All scheduled trips for this route
        /// </summary>
        public List<Schedule> Schedules { get; set; } = new List<Schedule>();
        
        /// <summary>
        /// Whether the route is currently active in the system
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Frequency in minutes during peak hours
        /// </summary>
        public int? PeakFrequency { get; set; }
        
        /// <summary>
        /// Frequency in minutes during off-peak hours
        /// </summary>
        public int? OffPeakFrequency { get; set; }
        
        /// <summary>
        /// Average journey time for the whole route in minutes
        /// </summary>
        public int AverageJourneyTime { get; set; }
        
        /// <summary>
        /// Polygon points representing the route path for display on a map
        /// </summary>
        public string PolylinePath { get; set; }
    }
}
