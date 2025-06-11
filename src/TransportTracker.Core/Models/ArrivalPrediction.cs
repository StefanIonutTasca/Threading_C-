using System;
using System.ComponentModel.DataAnnotations;

namespace TransportTracker.Core.Models
{
    /// <summary>
    /// Represents an arrival prediction for a vehicle at a stop
    /// </summary>
    public class ArrivalPrediction : BaseEntity
    {
        // Id property inherited from BaseEntity
        
        /// <summary>
        /// ID of the stop this prediction is for
        /// </summary>
        [Required]
        public string StopId { get; set; }
        
        /// <summary>
        /// Navigation property for the stop
        /// </summary>
        public Stop Stop { get; set; }
        
        /// <summary>
        /// ID of the route this prediction is for
        /// </summary>
        [Required]
        public string RouteId { get; set; }
        
        /// <summary>
        /// Navigation property for the route
        /// </summary>
        public Route Route { get; set; }
        
        /// <summary>
        /// ID of the vehicle this prediction is for
        /// </summary>
        [Required]
        public string VehicleId { get; set; }
        
        /// <summary>
        /// Navigation property for the vehicle
        /// </summary>
        public Vehicle Vehicle { get; set; }
        
        /// <summary>
        /// Predicted arrival time
        /// </summary>
        [Required]
        public DateTime PredictedArrivalTime { get; set; }
        
        /// <summary>
        /// Scheduled arrival time
        /// </summary>
        public DateTime ScheduledArrivalTime { get; set; }
        
        /// <summary>
        /// Delay in seconds (positive means late, negative means early)
        /// </summary>
        public int DelaySeconds { get; set; }
        
        /// <summary>
        /// Prediction status
        /// </summary>
        public PredictionStatus Status { get; set; }
        
        /// <summary>
        /// Confidence level of the prediction (0-100%)
        /// </summary>
        [Range(0, 100)]
        public int ConfidenceLevel { get; set; }
        
        /// <summary>
        /// When the prediction was generated
        /// </summary>
        public DateTime PredictionTime { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Distance of the vehicle from the stop in meters
        /// </summary>
        public double DistanceFromStop { get; set; }
        
        /// <summary>
        /// Number of stops away from the target stop
        /// </summary>
        public int StopsAway { get; set; }
        
        /// <summary>
        /// Calculates whether the vehicle is on time, early, or late
        /// </summary>
        /// <returns>The status of the arrival</returns>
        public ArrivalStatus GetArrivalStatus()
        {
            // If delay is within 30 seconds, consider it on time
            if (Math.Abs(DelaySeconds) <= 30)
            {
                return ArrivalStatus.OnTime;
            }
            
            return DelaySeconds > 0 ? ArrivalStatus.Late : ArrivalStatus.Early;
        }
        
        /// <summary>
        /// Get formatted delay time for display
        /// </summary>
        /// <returns>Formatted delay string</returns>
        public string GetFormattedDelay()
        {
            int absDelay = Math.Abs(DelaySeconds);
            
            if (absDelay < 60)
            {
                return $"{absDelay} sec {(DelaySeconds >= 0 ? "late" : "early")}";
            }
            
            int minutes = absDelay / 60;
            int seconds = absDelay % 60;
            
            if (seconds == 0)
            {
                return $"{minutes} min {(DelaySeconds >= 0 ? "late" : "early")}";
            }
            
            return $"{minutes} min {seconds} sec {(DelaySeconds >= 0 ? "late" : "early")}";
        }
    }
    
    /// <summary>
    /// Enumeration of possible prediction statuses
    /// </summary>
    public enum PredictionStatus
    {
        Scheduled,
        Predicted,
        Cancelled,
        NoData,
        Added
    }
    
    /// <summary>
    /// Enumeration of possible arrival statuses
    /// </summary>
    public enum ArrivalStatus
    {
        OnTime,
        Late,
        Early,
        Unknown
    }
}
