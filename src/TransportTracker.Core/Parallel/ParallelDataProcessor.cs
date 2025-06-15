using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TransportTracker.Core.Models;
using TransportTracker.Core.Parallel.Query;
using TransportTracker.Core.Parallel.Processing;
using TransportTracker.Core.Threading.Events;

namespace TransportTracker.Core.Parallel
{
    /// <summary>
    /// Main entry point for parallel data processing operations in the application
    /// </summary>
    public class ParallelDataProcessor
    {
        private readonly ILogger<ParallelDataProcessor> _logger;
        private readonly ParallelQueryProvider _queryProvider;
        private readonly CustomPartitioner _customPartitioner;
        private readonly BatchProcessor<Vehicle, Vehicle> _vehicleBatchProcessor;
        private readonly BatchProcessor<Route, Route> _routeBatchProcessor;
        private readonly BatchProcessor<Schedule, Schedule> _scheduleBatchProcessor;
        private readonly IProgressReporter _progressReporter;
        
        /// <summary>
        /// Creates a new instance of ParallelDataProcessor
        /// </summary>
        public ParallelDataProcessor(ILogger<ParallelDataProcessor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Create progress reporter
            _progressReporter = new ProgressReporter(logger, "Parallel Processing");
            
            // Create default options
            var defaultOptions = new ParallelProcessingOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                PreserveOrdering = true,
                UseCustomPartitioner = true
            };
            
            // Initialize components
            _queryProvider = new ParallelQueryProvider(logger, defaultOptions);
            _customPartitioner = new CustomPartitioner(logger);
            _vehicleBatchProcessor = new BatchProcessor<Vehicle, Vehicle>(logger, _progressReporter, defaultOptions);
            _routeBatchProcessor = new BatchProcessor<Route, Route>(logger, _progressReporter, defaultOptions);
            _scheduleBatchProcessor = new BatchProcessor<Schedule, Schedule>(logger, _progressReporter, defaultOptions);
        }
        
        /// <summary>
        /// Event raised when processing progress changes
        /// </summary>
        public event EventHandler<ProgressEventArgs> ProcessingProgressChanged
        {
            add { _progressReporter.ProgressChanged += value; }
            remove { _progressReporter.ProgressChanged -= value; }
        }
        
        #region Vehicle Processing
        
        /// <summary>
        /// Find vehicles within a specified distance of a location
        /// </summary>
        /// <param name="vehicles">Collection of vehicles</param>
        /// <param name="latitude">Center latitude</param>
        /// <param name="longitude">Center longitude</param>
        /// <param name="radiusInKm">Search radius in kilometers</param>
        /// <param name="options">Optional processing options</param>
        /// <returns>Vehicles within the specified radius</returns>
        public IEnumerable<Vehicle> FindNearbyVehicles(
            IEnumerable<Vehicle> vehicles,
            double latitude,
            double longitude,
            double radiusInKm,
            IParallelProcessingOptions options = null)
        {
            _logger.LogDebug($"Finding vehicles near ({latitude}, {longitude}) within {radiusInKm}km");
            return _queryProvider.FindNearbyVehicles(vehicles, latitude, longitude, radiusInKm, options);
        }
        
        /// <summary>
        /// Filter vehicles by status with parallel processing
        /// </summary>
        /// <param name="vehicles">Collection of vehicles</param>
        /// <param name="status">Status to filter by</param>
        /// <param name="options">Optional processing options</param>
        /// <returns>Filtered vehicles</returns>
        public IEnumerable<Vehicle> FilterVehiclesByStatus(
            IEnumerable<Vehicle> vehicles,
            VehicleStatus status,
            IParallelProcessingOptions options = null)
        {
            _logger.LogDebug($"Filtering vehicles by status: {status}");
            return _queryProvider.FilterVehiclesByStatus(vehicles, status, options);
        }
        
        /// <summary>
        /// Process vehicles in batches with custom logic
        /// </summary>
        /// <param name="vehicles">Collection of vehicles</param>
        /// <param name="processor">Processing function</param>
        /// <param name="batchSize">Size of each batch</param>
        /// <param name="options">Optional processing options</param>
        /// <returns>Processed vehicles</returns>
        public IEnumerable<Vehicle> ProcessVehiclesBatched(
            IEnumerable<Vehicle> vehicles,
            Func<Vehicle, Vehicle> processor,
            int batchSize = 1000,
            IParallelProcessingOptions options = null)
        {
            _logger.LogInformation($"Processing vehicles in batches of {batchSize}");
            return _vehicleBatchProcessor.ProcessBatches(vehicles, processor, batchSize, options);
        }
        
        /// <summary>
        /// Calculate vehicle density across geographic grid
        /// </summary>
        /// <param name="vehicles">Collection of vehicles</param>
        /// <param name="gridSize">Size of grid cells in kilometers</param>
        /// <param name="options">Optional processing options</param>
        /// <returns>Dictionary mapping grid cells to vehicle counts</returns>
        public Dictionary<(int X, int Y), int> CalculateVehicleDensity(
            IEnumerable<Vehicle> vehicles,
            double gridSize = 0.5,
            IParallelProcessingOptions options = null)
        {
            _logger.LogDebug($"Calculating vehicle density with grid size {gridSize}km");
            return _queryProvider.CalculateCongestionLevels(vehicles, gridSize, options);
        }
        
        /// <summary>
        /// Group vehicles by route with parallel processing
        /// </summary>
        /// <param name="vehicles">Collection of vehicles</param>
        /// <param name="options">Optional processing options</param>
        /// <returns>Dictionary of vehicles grouped by route ID</returns>
        public Dictionary<string, List<Vehicle>> GroupVehiclesByRoute(
            IEnumerable<Vehicle> vehicles,
            IParallelProcessingOptions options = null)
        {
            _logger.LogDebug("Grouping vehicles by route");
            return _queryProvider.GroupVehiclesByRoute(vehicles, options);
        }
        
        #endregion
        
        #region Schedule Processing
        
        /// <summary>
        /// Find upcoming departures for a stop with parallel processing
        /// </summary>
        /// <param name="schedules">Collection of schedules</param>
        /// <param name="stopId">Stop ID</param>
        /// <param name="fromTime">Starting time</param>
        /// <param name="maxResults">Maximum number of results</param>
        /// <param name="options">Optional processing options</param>
        /// <returns>Upcoming departures</returns>
        public IEnumerable<Schedule> FindUpcomingDepartures(
            IEnumerable<Schedule> schedules,
            string stopId,
            DateTime fromTime,
            int maxResults = 10,
            IParallelProcessingOptions options = null)
        {
            _logger.LogDebug($"Finding upcoming departures for stop {stopId} from {fromTime}");
            return _queryProvider.FindUpcomingDepartures(schedules, stopId, fromTime, maxResults, options);
        }
        
        /// <summary>
        /// Process schedules in batches
        /// </summary>
        /// <param name="schedules">Collection of schedules</param>
        /// <param name="processor">Processing function</param>
        /// <param name="batchSize">Size of each batch</param>
        /// <param name="options">Optional processing options</param>
        /// <returns>Processed schedules</returns>
        public IEnumerable<Schedule> ProcessSchedulesBatched(
            IEnumerable<Schedule> schedules,
            Func<Schedule, Schedule> processor,
            int batchSize = 1000,
            IParallelProcessingOptions options = null)
        {
            _logger.LogInformation($"Processing schedules in batches of {batchSize}");
            return _scheduleBatchProcessor.ProcessBatches(schedules, processor, batchSize, options);
        }
        
        /// <summary>
        /// Calculate schedule statistics by route using parallel processing
        /// </summary>
        /// <param name="schedules">Collection of schedules</param>
        /// <param name="options">Optional processing options</param>
        /// <returns>Dictionary mapping route IDs to average delay</returns>
        public Dictionary<string, double> CalculateAverageDelaysByRoute(
            IEnumerable<Schedule> schedules,
            IParallelProcessingOptions options = null)
        {
            _logger.LogDebug("Calculating average delays by route");
            
            // Create parallel query with appropriate options
            var parallelQuery = schedules.AsParallel();
            
            if (options?.MaxDegreeOfParallelism != null)
            {
                parallelQuery = parallelQuery.WithDegreeOfParallelism(options.MaxDegreeOfParallelism.Value);
            }
            
            // Use PLINQ to calculate average delays
            return parallelQuery
                .Where(s => s.DelayMinutes.HasValue)
                .GroupBy(s => s.RouteId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Average(s => s.DelayMinutes.Value)
                );
        }
        
        #endregion
        
        #region Advanced Processing
        
        /// <summary>
        /// Create geographically partitioned data for efficient parallel processing
        /// </summary>
        /// <param name="vehicles">Collection of vehicles</param>
        /// <param name="options">Optional processing options</param>
        /// <returns>Partitioned vehicle data</returns>
        public IEnumerable<Vehicle[]> CreateGeographicPartitions(
            IEnumerable<Vehicle> vehicles,
            IParallelProcessingOptions options = null)
        {
            _logger.LogDebug("Creating geographic partitions for vehicles");
            return _customPartitioner.CreateGeographicPartitioner(
                vehicles,
                v => v.Latitude,
                v => v.Longitude,
                options
            );
        }
        
        /// <summary>
        /// Process large dataset with automatic parallelism configuration
        /// </summary>
        /// <typeparam name="T">Type of data</typeparam>
        /// <param name="items">Collection of items</param>
        /// <param name="processor">Processing function</param>
        /// <returns>Processed results</returns>
        public IEnumerable<TResult> ProcessLargeDataset<T, TResult>(
            IEnumerable<T> items,
            Func<T, TResult> processor)
        {
            _logger.LogInformation($"Processing large dataset of {typeof(T).Name} items");
            
            // Auto-configure options based on collection size and type
            var itemsList = items.ToList();
            var options = SelectOptimalProcessingOptions<T>(itemsList.Count);
            
            // Create parallel query
            var parallelQuery = itemsList.AsParallel();
            
            if (options.MaxDegreeOfParallelism.HasValue)
            {
                parallelQuery = parallelQuery.WithDegreeOfParallelism(options.MaxDegreeOfParallelism.Value);
            }
            
            if (options.PreserveOrdering)
            {
                parallelQuery = parallelQuery.AsOrdered();
            }
            
            // Process with exception handling
            try
            {
                return parallelQuery
                    .Select(processor)
                    .ToList();
            }
            catch (AggregateException ex)
            {
                _logger.LogError(ex, "Parallel processing encountered multiple exceptions");
                
                // Log individual exceptions
                foreach (var innerEx in ex.InnerExceptions)
                {
                    _logger.LogError(innerEx, "Inner exception during parallel processing");
                }
                
                throw;
            }
        }
        
        /// <summary>
        /// Process data with custom aggregation in parallel
        /// </summary>
        /// <typeparam name="TKey">Type of grouping key</typeparam>
        /// <typeparam name="TValue">Type of aggregated value</typeparam>
        /// <param name="items">Collection of items</param>
        /// <param name="keySelector">Function to extract grouping key</param>
        /// <param name="valueSelector">Function to extract value</param>
        /// <param name="options">Optional processing options</param>
        /// <returns>Dictionary of aggregated values by key</returns>
        public Dictionary<TKey, TValue> AggregateByKey<T, TKey, TValue>(
            IEnumerable<T> items,
            Func<T, TKey> keySelector,
            Func<IEnumerable<T>, TValue> valueSelector,
            IParallelProcessingOptions options = null)
        {
            _logger.LogDebug($"Aggregating {typeof(T).Name} items by key");
            
            options ??= new ParallelProcessingOptions();
            
            // Create parallel query
            var parallelQuery = items.AsParallel();
            
            if (options.MaxDegreeOfParallelism.HasValue)
            {
                parallelQuery = parallelQuery.WithDegreeOfParallelism(options.MaxDegreeOfParallelism.Value);
            }
            
            // Perform grouping and aggregation
            return parallelQuery
                .GroupBy(keySelector)
                .ToDictionary(
                    g => g.Key,
                    g => valueSelector(g)
                );
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Select optimal processing options based on collection size and type
        /// </summary>
        /// <typeparam name="T">Type of data</typeparam>
        /// <param name="itemCount">Number of items</param>
        /// <returns>Optimized processing options</returns>
        private IParallelProcessingOptions SelectOptimalProcessingOptions<T>(int itemCount)
        {
            var options = new ParallelProcessingOptions();
            
            // For small collections, limit parallelism to avoid overhead
            if (itemCount < 1000)
            {
                options.MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 2);
                options.UseCustomPartitioner = false;
                options.PreserveOrdering = true;
            }
            // For medium collections
            else if (itemCount < 10000)
            {
                options.MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 4);
                options.UseCustomPartitioner = true;
                options.PartitionChunkSize = 500;
            }
            // For large collections
            else if (itemCount < 100000)
            {
                options.MaxDegreeOfParallelism = Environment.ProcessorCount;
                options.UseCustomPartitioner = true;
                options.PartitionChunkSize = 5000;
                options.PreserveOrdering = typeof(T) == typeof(Vehicle); // Preserve order for vehicles
            }
            // For very large collections
            else
            {
                options.MaxDegreeOfParallelism = Environment.ProcessorCount;
                options.UseCustomPartitioner = true;
                options.PartitionChunkSize = 10000;
                options.PreserveOrdering = false; // Performance over ordering
                options.EnableTaskScheduling = true;
            }
            
            return options;
        }
        
        #endregion
    }
}
