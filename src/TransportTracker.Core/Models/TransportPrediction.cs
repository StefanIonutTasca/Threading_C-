using System;

namespace TransportTracker.Core.Models
{
    /// <summary>
    /// Represents a prediction for a transport vehicle's arrival or departure.
    /// </summary>
    public class TransportPrediction
    {
        public string VehicleId { get; set; }
        public string StopId { get; set; }
        public DateTime PredictedArrival { get; set; }
        public DateTime PredictedDeparture { get; set; }
        // Add additional properties as needed
    }
}
