using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace TransportTracker.Core.Models
{
    /// <summary>
    /// Represents a public transport route (e.g. bus line, train route)
    /// </summary>
    public class Route : BaseEntity
    {
        /// <summary>
        /// Gets or sets the short code for the route (e.g., "A12", "Blue Line").
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Gets or sets the type of the route (e.g., "Bus", "Train", "Subway").
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the frequency of service in minutes.
        /// </summary>
        public int FrequencyMinutes { get; set; }

        /// <summary>
        /// Gets or sets whether the route is bidirectional.
        /// </summary>
        public bool IsBidirectional { get; set; } = true;

        /// <summary>
        /// Gets or sets the number of vehicles currently operating on this route.
        /// </summary>
        public int VehicleCount { get; set; }

        // Id property inherited from BaseEntity

        /// <summary>
        /// Human-readable name or number of the route (e.g., "Route 42", "Blue Line")
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Name { get; set; }

        /// <summary>
        /// Short identifier for the route (e.g., "42", "B")
        /// </summary>
        [MaxLength(10)]
        public string ShortName { get; set; }

        /// <summary>
        /// Short description of the route
        /// </summary>
        [MaxLength(200)]
        public new string Description { get; set; }

        /// <summary>
        /// Type of transport used on this route
        /// </summary>
        [Required]
        public new VehicleType TransportType { get; set; }

        /// <summary>
        /// Color associated with the route (hex code, e.g., "#FF0000" for red)
        /// </summary>
        [MaxLength(10)]
        public new string Color { get; set; }

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
        /// Direct reference to stops for compatibility with legacy and new code
        /// </summary>
        public List<Stop> Stops { get; set; } = new List<Stop>();
        
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
        
        /// <summary>
        /// Gets all stops for this route ordered by sequence
        /// </summary>
        /// <returns>The ordered list of stops for this route</returns>
        public List<Stop> GetOrderedStops()
        {
            if (RouteStops == null || RouteStops.Count == 0)
                return new List<Stop>();
                
            return RouteStops
                .OrderBy(rs => rs.SequenceNumber)
                .Select(rs => rs.Stop)
                .ToList();
        }
        
        /// <summary>
        /// Creates a deep copy of the route object
        /// </summary>
        /// <returns>A new Route instance with the same values</returns>
        public Route Clone()
        {
            var clone = new Route
            {
                Id = this.Id,
                Name = this.Name,
                ShortName = this.ShortName,
                Description = this.Description,
                TransportType = this.TransportType,
                Color = this.Color,
                TextColor = this.TextColor,
                Origin = this.Origin,
                Destination = this.Destination,
                IsActive = this.IsActive,
                PeakFrequency = this.PeakFrequency,
                OffPeakFrequency = this.OffPeakFrequency,
                AverageJourneyTime = this.AverageJourneyTime,
                PolylinePath = this.PolylinePath
            };
            
            // Deep copy lists if they're not null
            if (RouteStops != null)
            {
                clone.RouteStops = RouteStops.Select(rs => new RouteStop
                {
                    RouteId = rs.RouteId,
                    StopId = rs.StopId,
                    SequenceNumber = rs.SequenceNumber
                }).ToList();
            }
            
            // We don't clone references to other entities to avoid circular references
            // The navigation properties should be populated separately if needed
            
            return clone;
        }
    }
}
