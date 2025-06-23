using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace TransportTracker.Core.Models
{
    /// <summary>
    /// Represents a public transport stop or station
    /// </summary>
    public class Stop : BaseEntity
{
    public string StopId { get; set; }
    public Stop StopDetails { get; set; }
    // Id property inherited from BaseEntity

        /// <summary>
        /// Name of the stop (e.g., "Central Station", "Main Street")
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        /// <summary>
        /// Code or identifier for the stop (e.g., "CNTRL01")
        /// </summary>
        [MaxLength(20)]
        public string Code { get; set; }

        /// <summary>
        /// The primary route ID this stop is associated with (optional)
        /// </summary>
        public string RouteId { get; set; }

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
        /// Combined location object with latitude and longitude
        /// </summary>
        public Location Location
        {
            get => new Location { Latitude = Latitude, Longitude = Longitude };
            set
            {
                if (value != null)
                {
                    Latitude = value.Latitude;
                    Longitude = value.Longitude;
                }
            }
        }

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
        /// Direct reference to routes for compatibility with legacy and new code
        /// </summary>
        public List<Route> Routes { get; set; } = new List<Route>();

        /// <summary>
        /// Sequence number of this stop in a route (if applicable)
        /// </summary>
        public int SequenceNumber { get; set; }

        /// <summary>
        /// Transport types that serve this stop
        /// </summary>
        public List<TransportType> TransportTypes { get; set; } = new List<TransportType>();

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

        /// <summary>
        /// Creates a deep copy of the stop object
        /// </summary>
        /// <returns>A new Stop instance with the same values</returns>
        public Stop Clone()
        {
            var clone = new Stop
            {
                Id = this.Id,
                Name = this.Name,
                Code = this.Code,
                Description = this.Description,
                Latitude = this.Latitude,
                Longitude = this.Longitude,
                Zone = this.Zone,
                Address = this.Address,
                HasShelter = this.HasShelter,
                HasSeating = this.HasSeating,
                IsAccessible = this.IsAccessible,
                HasRealtimeInfo = this.HasRealtimeInfo,
                Type = this.Type,
                LastUpdated = this.LastUpdated,
                RouteId = this.RouteId
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

            if (TransportTypes != null)
            {
                clone.TransportTypes = new List<TransportType>(TransportTypes);
            }
            
            return clone;
        }
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
