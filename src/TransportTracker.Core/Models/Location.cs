using System;
using System.ComponentModel.DataAnnotations;

namespace TransportTracker.Core.Models
{
    /// <summary>
    /// Represents a geographical location with coordinates
    /// </summary>
    public class Location : BaseEntity
    {
        // Id property inherited from BaseEntity
        
        /// <summary>
        /// Latitude coordinate
        /// </summary>
        [Required]
        public double Latitude { get; set; }
        
        /// <summary>
        /// Longitude coordinate
        /// </summary>
        [Required]
        public double Longitude { get; set; }
        
        /// <summary>
        /// Altitude in meters above sea level (if available)
        /// </summary>
        public double? Altitude { get; set; }
        
        /// <summary>
        /// Accuracy of the location data in meters (if available)
        /// </summary>
        public double? Accuracy { get; set; }
        
        /// <summary>
        /// Time when the location was recorded
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Name or description of the location
        /// </summary>
        [MaxLength(100)]
        public string Name { get; set; }
        
        /// <summary>
        /// Address of the location
        /// </summary>
        [MaxLength(200)]
        public string Address { get; set; }
        
        /// <summary>
        /// Calculate distance to another location in kilometers using the Haversine formula
        /// </summary>
        /// <param name="other">The other location</param>
        /// <returns>Distance in kilometers</returns>
        public double DistanceTo(Location other)
        {
            const double EarthRadiusKm = 6371.0;
            
            var dLat = ToRadians(other.Latitude - this.Latitude);
            var dLon = ToRadians(other.Longitude - this.Longitude);
            
            var lat1 = ToRadians(this.Latitude);
            var lat2 = ToRadians(other.Latitude);
            
            var a = Math.Sin(dLat/2) * Math.Sin(dLat/2) +
                    Math.Sin(dLon/2) * Math.Sin(dLon/2) * Math.Cos(lat1) * Math.Cos(lat2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
            
            return EarthRadiusKm * c;
        }
        
        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}
