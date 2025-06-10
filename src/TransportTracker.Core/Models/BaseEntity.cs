using System;
using System.ComponentModel.DataAnnotations;

namespace TransportTracker.Core.Models
{
    /// <summary>
    /// Base class for all entities with common properties
    /// </summary>
    public abstract class BaseEntity
    {
        /// <summary>
        /// Unique identifier for the entity
        /// </summary>
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// When the entity was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the entity was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
