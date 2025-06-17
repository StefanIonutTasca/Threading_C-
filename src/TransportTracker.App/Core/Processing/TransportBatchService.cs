using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using System.Linq;
using TransportTracker.App.Models;

namespace TransportTracker.App.Core.Processing
{
    /// <summary>
    /// Service for batch processing transport data operations
    /// </summary>
    public class TransportBatchService
    {
        private readonly BatchProcessor<TransportVehicle, TransportVehicle> _vehicleBatchProcessor;
        private readonly BatchProcessor<TransportStop, TransportStop> _stopBatchProcessor;
        private readonly BatchProcessor<Location, HeatmapPoint> _heatmapGenerator;

        public TransportBatchService(IProgress<BatchProcessingProgress> progress = null)
        {
            // Create batch processors with default settings
            var options = new BatchProcessingOptions();
            
            // Batch processor for vehicle data
            _vehicleBatchProcessor = new BatchProcessor<TransportVehicle, TransportVehicle>(
                ProcessVehicleBatchAsync,
                progress,
                options
            );
            
            // Batch processor for stop data
            _stopBatchProcessor = new BatchProcessor<TransportStop, TransportStop>(
                ProcessStopBatchAsync,
                progress, 
                options
            );
            
            // Batch processor for generating heatmap data
            _heatmapGenerator = new BatchProcessor<Location, HeatmapPoint>(
                GenerateHeatmapPointsAsync,
                progress,
                options
            );
        }

        /// <summary>
        /// Processes a collection of vehicle data in optimized batches
        /// </summary>
        /// <param name="vehicles">The vehicles to process</param>
        /// <param name="processingFunc">Custom processing function to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Processed vehicle collection</returns>
        public async Task<IEnumerable<TransportVehicle>> ProcessVehiclesAsync(
            IEnumerable<TransportVehicle> vehicles,
            Func<TransportVehicle, TransportVehicle> processingFunc = null,
            CancellationToken cancellationToken = default)
        {
            if (vehicles == null)
                return Enumerable.Empty<TransportVehicle>();
                
            // Store the processing function for batch operations
            _currentVehicleProcessingFunc = processingFunc;
            
            // Process the vehicles in batches
            return await _vehicleBatchProcessor.ProcessAsync(vehicles, cancellationToken);
        }

        /// <summary>
        /// Processes a collection of transport stops in optimized batches
        /// </summary>
        /// <param name="stops">The stops to process</param>
        /// <param name="processingFunc">Custom processing function to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Processed stops collection</returns>
        public async Task<IEnumerable<TransportStop>> ProcessStopsAsync(
            IEnumerable<TransportStop> stops,
            Func<TransportStop, TransportStop> processingFunc = null,
            CancellationToken cancellationToken = default)
        {
            if (stops == null)
                return Enumerable.Empty<TransportStop>();
                
            // Store the processing function for batch operations
            _currentStopProcessingFunc = processingFunc;
            
            // Process the stops in batches
            return await _stopBatchProcessor.ProcessAsync(stops, cancellationToken);
        }

        /// <summary>
        /// Generates heatmap data points from vehicle and stop locations
        /// </summary>
        /// <param name="vehicleLocations">Vehicle location data</param>
        /// <param name="stopLocations">Stop location data</param>
        /// <param name="radius">Base radius for heatmap points</param>
        /// <param name="maxIntensity">Maximum intensity value</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of heatmap points</returns>
        public async Task<IEnumerable<HeatmapPoint>> GenerateHeatmapDataAsync(
            IEnumerable<Location> locations,
            double radius = 0.01,
            double maxIntensity = 1.0,
            CancellationToken cancellationToken = default)
        {
            if (locations == null)
                return Enumerable.Empty<HeatmapPoint>();
                
            // Store parameters for the batch processing
            _heatmapRadius = radius;
            _heatmapMaxIntensity = maxIntensity;
            
            // Process the locations in batches
            return await _heatmapGenerator.ProcessAsync(locations, cancellationToken);
        }

        #region Private Processing Methods
        
        // Store processing functions provided by caller
        private Func<TransportVehicle, TransportVehicle> _currentVehicleProcessingFunc;
        private Func<TransportStop, TransportStop> _currentStopProcessingFunc;
        
        // Heatmap generation parameters
        private double _heatmapRadius;
        private double _heatmapMaxIntensity;

        /// <summary>
        /// Processes a batch of vehicles
        /// </summary>
        private async Task<IEnumerable<TransportVehicle>> ProcessVehicleBatchAsync(
            IEnumerable<TransportVehicle> vehicleBatch, 
            CancellationToken cancellationToken)
        {
            // Return the batch as-is if no processing function specified
            if (_currentVehicleProcessingFunc == null)
                return vehicleBatch;
                
            // Process each vehicle in parallel
            return await ParallelDataProcessor.ProcessInParallelAsync(
                vehicleBatch,
                _currentVehicleProcessingFunc,
                cancellationToken
            );
        }
        
        /// <summary>
        /// Processes a batch of stops
        /// </summary>
        private async Task<IEnumerable<TransportStop>> ProcessStopBatchAsync(
            IEnumerable<TransportStop> stopBatch, 
            CancellationToken cancellationToken)
        {
            // Return the batch as-is if no processing function specified
            if (_currentStopProcessingFunc == null)
                return stopBatch;
                
            // Process each stop in parallel
            return await ParallelDataProcessor.ProcessInParallelAsync(
                stopBatch,
                _currentStopProcessingFunc,
                cancellationToken
            );
        }
        
        /// <summary>
        /// Generates heatmap points from location data
        /// </summary>
        private async Task<IEnumerable<HeatmapPoint>> GenerateHeatmapPointsAsync(
            IEnumerable<Location> locationBatch, 
            CancellationToken cancellationToken)
        {
            // Calculate density based on proximity of locations in the batch
            var result = new List<HeatmapPoint>();
            var locations = locationBatch.ToList();
            
            // Skip processing if canceled
            if (cancellationToken.IsCancellationRequested)
                return result;
                
            // Generate heatmap points for each location
            foreach (var location in locations)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                    
                // Count nearby locations to determine intensity
                int nearbyCount = 0;
                foreach (var other in locations)
                {
                    // Skip self
                    if (other == location)
                        continue;
                        
                    // Calculate distance between points
                    double distance = Location.CalculateDistance(location, other, DistanceUnits.Kilometers);
                    
                    // Count if within radius
                    if (distance <= _heatmapRadius * 2)
                    {
                        nearbyCount++;
                    }
                }
                
                // Calculate intensity based on nearby count with limits
                double intensity = Math.Min(_heatmapMaxIntensity, 
                    0.2 + (nearbyCount / (double)Math.Max(1, locations.Count)) * _heatmapMaxIntensity);
                
                // Create heatmap point
                result.Add(new HeatmapPoint
                {
                    Location = location,
                    Intensity = intensity,
                    Radius = _heatmapRadius * (0.5 + intensity / 2) // Scale radius by intensity 
                });
            }
            
            // Return generated points
            return await Task.FromResult(result);
        }
        
        #endregion
    }

    /// <summary>
    /// Represents a point on a heatmap with intensity data
    /// </summary>
    public class HeatmapPoint
    {
        /// <summary>
        /// Geographic location of this point
        /// </summary>
        public Location Location { get; set; }
        
        /// <summary>
        /// Intensity value from 0.0 to 1.0
        /// </summary>
        public double Intensity { get; set; }
        
        /// <summary>
        /// Radius of influence in map units
        /// </summary>
        public double Radius { get; set; }
    }
}
