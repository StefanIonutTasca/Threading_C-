using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TransportTracker.Core.Models
{
    /// <summary>
    /// Represents a service alert or notification about transport service
    /// </summary>
    public class ServiceAlert : BaseEntity
    {
        // Id property inherited from BaseEntity
        
        /// <summary>
        /// Title of the alert
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Title { get; set; }
        
        /// <summary>
        /// Detailed description of the alert
        /// </summary>
        [Required]
        [MaxLength(2000)]
        public string Description { get; set; }
        
        /// <summary>
        /// When the alert was issued
        /// </summary>
        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When the alert expires
        /// </summary>
        public DateTime ExpiresAt { get; set; }
        
        /// <summary>
        /// Severity level of the alert
        /// </summary>
        public AlertSeverity Severity { get; set; }
        
        /// <summary>
        /// Type of the alert
        /// </summary>
        public AlertType Type { get; set; }
        
        /// <summary>
        /// Affected route IDs, if any
        /// </summary>
        public List<string> AffectedRouteIds { get; set; } = new List<string>();
        
        /// <summary>
        /// Affected stop IDs, if any
        /// </summary>
        public List<string> AffectedStopIds { get; set; } = new List<string>();
        
        /// <summary>
        /// URL with more information, if available
        /// </summary>
        [MaxLength(500)]
        public string InfoUrl { get; set; }
        
        /// <summary>
        /// Determines if the alert is currently active
        /// </summary>
        public bool IsActive => DateTime.UtcNow <= ExpiresAt;
        
        /// <summary>
        /// Any recommended actions for passengers
        /// </summary>
        [MaxLength(500)]
        public string RecommendedAction { get; set; }
    }
    
    /// <summary>
    /// Enumeration of possible alert severity levels
    /// </summary>
    public enum AlertSeverity
    {
        Info,
        Warning,
        Severe,
        Emergency
    }
    
    /// <summary>
    /// Enumeration of possible alert types
    /// </summary>
    public enum AlertType
    {
        Delay,
        Detour,
        ServiceChange,
        Closure,
        Weather,
        Maintenance,
        Accident,
        SpecialEvent,
        Other
    }
}
