using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TransportTracker.Core.Models;

namespace TransportTracker.Core.Parallel.Query
{
    /// <summary>
    /// Provides PLINQ query operations optimized for transport data processing
    /// </summary>
    public class ParallelQueryProvider
    {
        private readonly ILogger<ParallelQueryProvider> _logger;
        private readonly IParallelProcessingOptions _defaultOptions;
        
        /// <summary>
        /// Creates a new instance of ParallelQueryProvider
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="defaultOptions">Default processing options</param>
        public ParallelQueryProvider(
            ILogger<ParallelQueryProvider> logger,
            IParallelProcessingOptions defaultOptions = null)
        {
            _logger = logger;
            _defaultOptions = defaultOptions ?? new ParallelProcessingOptions();
        }
        
        /// <summary>
        /// Creates a parallel query from the source collection with the specified options
        /// </summary>
        /// <typeparam name="T">Type of elements in the collection</typeparam>
        /// <param name="source">Source collection</param>
        /// <param name="options">Processing options (optional)</param>
        /// <returns>A parallel query ready for processing</returns>
        public ParallelQuery<T> CreateParallelQuery<T>(IEnumerable<T> source, IParallelProcessingOptions options = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            
            options ??= _defaultOptions;
            
            _logger.LogDebug($"Creating parallel query for {typeof(T).Name} collection with " +
                           $"degree of parallelism: {options.MaxDegreeOfParallelism ?? Environment.ProcessorCount}");
            
            var parallelQuery = source.AsParallel();
            
            if (options.MaxDegreeOfParallelism.HasValue && options.MaxDegreeOfParallelism > 0)
            {
                parallelQuery = parallelQuery.WithDegreeOfParallelism(options.MaxDegreeOfParallelism.Value);
            }
            
            if (options.PreserveOrdering)
            {
                parallelQuery = parallelQuery.AsOrdered();
            }
            
            if (options.CancellationTokenSource != null)
            {
                parallelQuery = parallelQuery.WithCancellation(options.CancellationTokenSource.Token);
            }
            
            // Add execution plan metrics logging for development
            #if DEBUG
            LogExecutionPlan(typeof(T), source.Count(), options);
            #endif
            
            return parallelQuery;
        }
        
        /// <summary>
        /// Filter vehicles by status using parallel processing
        /// </summary>
        /// <param name="vehicles">Source collection of vehicles</param>
        /// <param name="status">Status to filter by</param>
        /// <param name="options">Processing options (optional)</param>
        /// <returns>Filtered collection of vehicles</returns>
        public IEnumerable<Vehicle> FilterVehiclesByStatus(
            IEnumerable<Vehicle> vehicles,
            VehicleStatus status,
            IParallelProcessingOptions options = null)
        {
            options ??= _defaultOptions;
            
            var parallelQuery = CreateParallelQuery(vehicles, options);
            return parallelQuery.Where(v => v.Status == status).ToList();
        }
        
        /// <summary>
        /// Find nearby vehicles within specified distance from given coordinates
        /// </summary>
        /// <param name="vehicles">Source collection of vehicles</param>
        /// <param name="latitude">Center latitude</param>
        /// <param name="longitude">Center longitude</param>
        /// <param name="radiusInKm">Search radius in kilometers</param>
        /// <param name="options">Processing options (optional)</param>
        /// <returns>Collection of vehicles within the specified radius</returns>
        public IEnumerable<Vehicle> FindNearbyVehicles(
            IEnumerable<Vehicle> vehicles,
            double latitude,
            double longitude,
            double radiusInKm,
            IParallelProcessingOptions options = null)
        {
            options ??= _defaultOptions;
            
            var parallelQuery = CreateParallelQuery(vehicles, options);
            
            return parallelQuery.Where(v => CalculateDistance(
                latitude, longitude, v.Latitude, v.Longitude) <= radiusInKm)
                .ToList();
        }
        
        /// <summary>
        /// Group vehicles by route with parallel processing
        /// </summary>
        /// <param name="vehicles">Source collection of vehicles</param>
        /// <param name="options">Processing options (optional)</param>
        /// <returns>Dictionary of vehicles grouped by route ID</returns>
        public Dictionary<string, List<Vehicle>> GroupVehiclesByRoute(
            IEnumerable<Vehicle> vehicles,
            IParallelProcessingOptions options = null)
        {
            options ??= _defaultOptions;
            
            var parallelQuery = CreateParallelQuery(vehicles, options);
            
            return parallelQuery
                .GroupBy(v => v.RouteId)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToList());
        }
        
        /// <summary>
        /// Find upcoming departures for a stop using parallel processing
        /// </summary>
        /// <param name="schedules">Source collection of schedules</param>
        /// <param name="stopId">Stop ID to find departures for</param>
        /// <param name="fromTime">Starting time for search</param>
        /// <param name="maxResults">Maximum number of results to return</param>
        /// <param name="options">Processing options (optional)</param>
        /// <returns>List of upcoming departures</returns>
        public IEnumerable<Schedule> FindUpcomingDepartures(
            IEnumerable<Schedule> schedules,
            string stopId,
            DateTime fromTime,
            int maxResults = 10,
            IParallelProcessingOptions options = null)
        {
            options ??= _defaultOptions;
            
            var parallelQuery = CreateParallelQuery(schedules, options);
            
            return parallelQuery
                .Where(s => s.IsActive && 
                          s.DepartureTime >= fromTime && 
                          GetScheduleStopsIds(s).Contains(stopId))
                .OrderBy(s => s.DepartureTime)
                .Take(maxResults)
                .ToList();
        }
        
        /// <summary>
        /// Calculate transport congestion levels using parallel processing
        /// </summary>
        /// <param name="vehicles">Source collection of vehicles</param>
        /// <param name="gridSize">Size of grid cells in kilometers</param>
        /// <param name="options">Processing options (optional)</param>
        /// <returns>Dictionary mapping grid cells to vehicle counts</returns>
        public Dictionary<(int X, int Y), int> CalculateCongestionLevels(
            IEnumerable<Vehicle> vehicles,
            double gridSize = 0.5, // 0.5km grid cells by default
            IParallelProcessingOptions options = null)
        {
            options ??= _defaultOptions;
            
            var parallelQuery = CreateParallelQuery(vehicles, options);
            
            // Use PLINQ to process the grid allocation in parallel
            return parallelQuery
                .GroupBy(v => (
                    X: (int)(v.Longitude / gridSize),
                    Y: (int)(v.Latitude / gridSize)
                ))
                .ToDictionary(
                    g => g.Key,
                    g => g.Count()
                );
        }
        
        #region Helper Methods
        
        /// <summary>
        /// Calculate the Haversine distance between two points
        /// </summary>
        /// <returns>Distance in kilometers</returns>
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadius = 6371; // km
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
                    
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadius * c;
        }
        
        /// <summary>
        /// Convert degrees to radians
        /// </summary>
        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }
        
        /// <summary>
        /// Get the stop IDs for a schedule (placeholder implementation)
        /// </summary>
        /// <remarks>
        /// In a real implementation, this would use the actual relationship between schedules and stops
        /// </remarks>
        private IEnumerable<string> GetScheduleStopsIds(Schedule schedule)
        {
            // This is a placeholder. In the real application, this would fetch the stops associated with the schedule
            // For now, returning an empty list to make the method compile
            return new List<string>();
        }
        
        /// <summary>
        /// Log execution plan metrics for development purposes
        /// </summary>
        private void LogExecutionPlan<T>(Type itemType, int count, IParallelProcessingOptions options)
        {
            int degreeOfParallelism = options.MaxDegreeOfParallelism ?? Environment.ProcessorCount;
            int estimatedChunkSize = options.PartitionChunkSize ?? (count / degreeOfParallelism);
            
            _logger.LogDebug(
                $"PLINQ Execution Plan for {itemType.Name}: " +
                $"Items: {count}, " +
                $"Parallelism: {degreeOfParallelism}, " +
                $"Chunks: {Math.Ceiling((double)count / estimatedChunkSize)}, " +
                $"Avg. Chunk Size: {estimatedChunkSize}");
        }
        
        #endregion
    }
}
