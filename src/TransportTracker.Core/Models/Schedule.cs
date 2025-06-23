using System;
using System.ComponentModel.DataAnnotations;

namespace TransportTracker.Core.Models
{
    /// <summary>
    /// Represents a scheduled trip for a particular route
    /// </summary>
    public class Schedule : BaseEntity
    {
        // Id property inherited from BaseEntity

        /// <summary>
        /// Reference to the route this schedule is for
        /// </summary>
        [Required]
        public string RouteId { get; set; }

        /// <summary>
        /// Navigation property for the route
        /// </summary>
        public Route Route { get; set; }

        /// <summary>
        /// Vehicle assigned to this schedule, if any
        /// </summary>
        public string VehicleId { get; set; }
        
        /// <summary>
        /// ID of the starting stop for this schedule
        /// </summary>
        [Required]
        public string StartStopId { get; set; }
        
        /// <summary>
        /// ID of the ending stop for this schedule
        /// </summary>
        [Required]
        public string EndStopId { get; set; }

        /// <summary>
        /// Navigation property for the vehicle
        /// </summary>
        public Vehicle Vehicle { get; set; }

        /// <summary>
        /// Scheduled departure time from the origin
        /// </summary>
        [Required]
        public DateTime DepartureTime { get; set; }

        /// <summary>
        /// Estimated arrival time at the destination
        /// </summary>
        [Required]
        public DateTime ArrivalTime { get; set; }
        
        /// <summary>
        /// Real-time estimated arrival time at the destination
        /// </summary>
        public DateTime EstimatedArrivalTime { get; set; }

        /// <summary>
        /// Days of the week when this schedule is active (bit flags: 1=Monday, 2=Tuesday, 4=Wednesday, etc.)
        /// </summary>
        public int ServiceDays { get; set; }

        /// <summary>
        /// Whether the schedule is for peak hours
        /// </summary>
        public bool IsPeakHour { get; set; }

        /// <summary>
        /// Whether the schedule is currently active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Any notes or special information about this scheduled trip
        /// </summary>
        [MaxLength(500)]
        public string Notes { get; set; }

        /// <summary>
        /// Real-time status of this scheduled trip
        /// </summary>
        public TripStatus Status { get; set; } = TripStatus.OnTime;

        /// <summary>
        /// Delay in minutes (if any, positive for delays, negative for early)
        /// </summary>
        public int DelayMinutes { get; set; }

        /// <summary>
        /// Checks if the schedule is valid for the given date
        /// </summary>
        public bool IsValidForDate(DateTime date)
        {
            // Get the day of week (0 = Sunday, 1 = Monday, etc.)
            int dayOfWeek = ((int)date.DayOfWeek + 6) % 7 + 1;
            
            // Convert to bit flag (1=Monday, 2=Tuesday, 4=Wednesday, etc.)
            int dayFlag = 1 << (dayOfWeek - 1);
            
            // Check if this day's bit is set in the ServiceDays
            return (ServiceDays & dayFlag) != 0;
        }

        /// <summary>
        /// Creates a deep copy of the schedule object
        /// </summary>
        /// <returns>A new Schedule instance with the same values</returns>
        public Schedule Clone()
        {
            return new Schedule
            {
                Id = this.Id,
                RouteId = this.RouteId,
                VehicleId = this.VehicleId,
                StartStopId = this.StartStopId,
                EndStopId = this.EndStopId,
                DepartureTime = this.DepartureTime,
                ArrivalTime = this.ArrivalTime,
                EstimatedArrivalTime = this.EstimatedArrivalTime,
                ServiceDays = this.ServiceDays,
                IsPeakHour = this.IsPeakHour,
                IsActive = this.IsActive,
                Notes = this.Notes,
                Status = this.Status,
                DelayMinutes = this.DelayMinutes
                // Navigation properties are not cloned to avoid circular references
            };
        }
    }

    /// <summary>
    /// Enumeration of possible trip statuses
    /// </summary>
    public enum TripStatus
    {
        OnTime,
        Delayed,
        Early,
        Cancelled,
        Diverted,
        NotYetDeparted,
        InProgress,
        Completed
    }
}
