using System.ComponentModel.DataAnnotations;

namespace TransportTracker.Core.Models
{
    /// <summary>
    /// Junction entity that connects routes and stops with sequence information
    /// </summary>
    public class RouteStop : BaseEntity
    {
        // Id property inherited from BaseEntity

        /// <summary>
        /// ID of the route
        /// </summary>
        [Required]
        public string RouteId { get; set; }

        /// <summary>
        /// Navigation property for the route
        /// </summary>
        public Route Route { get; set; }

        /// <summary>
        /// ID of the stop
        /// </summary>
        [Required]
        public string StopId { get; set; }

        /// <summary>
        /// Navigation property for the stop
        /// </summary>
        public Stop Stop { get; set; }

        /// <summary>
        /// Order of the stop within the route (0-based)
        /// </summary>
        [Required]
        public int SequenceNumber { get; set; }

        /// <summary>
        /// Average time in minutes it takes to travel from the previous stop
        /// </summary>
        public int TravelTimeFromPreviousStop { get; set; }

        /// <summary>
        /// Distance in meters from the previous stop
        /// </summary>
        public double DistanceFromPreviousStop { get; set; }

        /// <summary>
        /// Whether this is a timepoint stop (where vehicles wait if they're early)
        /// </summary>
        public bool IsTimepoint { get; set; }

        /// <summary>
        /// Whether this stop is used only on request (flag stop)
        /// </summary>
        public bool IsRequestStop { get; set; }
    }
}
