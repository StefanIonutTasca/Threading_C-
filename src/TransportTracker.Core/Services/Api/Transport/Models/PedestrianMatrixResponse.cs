using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TransportTracker.Core.Services.Api.Transport.Models
{
    /// <summary>
    /// Response from the pedestrian matrix endpoint
    /// </summary>
    public class PedestrianMatrixResponse
    {
        /// <summary>
        /// Status code of the request (e.g., "Ok", "NoTable")
        /// </summary>
        [JsonPropertyName("code")]
        public string Code { get; set; }
        
        /// <summary>
        /// List of durations between source and destination points
        /// </summary>
        [JsonPropertyName("durations")]
        public List<List<double?>> Durations { get; set; }
        
        /// <summary>
        /// List of distances between source and destination points
        /// </summary>
        [JsonPropertyName("distances")]
        public List<List<double?>> Distances { get; set; }
        
        /// <summary>
        /// Information about sources used in the matrix calculation
        /// </summary>
        [JsonPropertyName("sources")]
        public List<MatrixPoint> Sources { get; set; }
        
        /// <summary>
        /// Information about destinations used in the matrix calculation
        /// </summary>
        [JsonPropertyName("destinations")]
        public List<MatrixPoint> Destinations { get; set; }
        
        /// <summary>
        /// Request processing time at server in milliseconds
        /// </summary>
        [JsonPropertyName("processingTimeMs")]
        public double ProcessingTimeMs { get; set; }
        
        /// <summary>
        /// Checks if the response is valid (has "Ok" status)
        /// </summary>
        /// <returns>True if the response is valid, otherwise false</returns>
        public bool IsValidResponse()
        {
            return Code?.ToLowerInvariant() == "ok";
        }
        
        /// <summary>
        /// Gets the duration between a source and destination
        /// </summary>
        /// <param name="sourceIndex">Index of the source point</param>
        /// <param name="destinationIndex">Index of the destination point</param>
        /// <returns>Duration in seconds, or null if not available</returns>
        public double? GetDuration(int sourceIndex, int destinationIndex)
        {
            if (Durations == null || sourceIndex >= Durations.Count || 
                destinationIndex >= Durations[sourceIndex].Count)
                return null;
                
            return Durations[sourceIndex][destinationIndex];
        }
        
        /// <summary>
        /// Gets the distance between a source and destination
        /// </summary>
        /// <param name="sourceIndex">Index of the source point</param>
        /// <param name="destinationIndex">Index of the destination point</param>
        /// <returns>Distance in meters, or null if not available</returns>
        public double? GetDistance(int sourceIndex, int destinationIndex)
        {
            if (Distances == null || sourceIndex >= Distances.Count || 
                destinationIndex >= Distances[sourceIndex].Count)
                return null;
                
            return Distances[sourceIndex][destinationIndex];
        }
    }
    
    /// <summary>
    /// Point used in the matrix calculation
    /// </summary>
    public class MatrixPoint
    {
        /// <summary>
        /// Name of the point (usually derived from OSM or empty)
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        /// <summary>
        /// Location coordinates of the point [lon, lat]
        /// </summary>
        [JsonPropertyName("location")]
        public List<double> Location { get; set; } = new List<double>();
        
        /// <summary>
        /// Gets the longitude from the location
        /// </summary>
        public double GetLongitude()
        {
            return Location.Count > 0 ? Location[0] : 0;
        }
        
        /// <summary>
        /// Gets the latitude from the location
        /// </summary>
        public double GetLatitude()
        {
            return Location.Count > 1 ? Location[1] : 0;
        }
    }
}
