using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using TransportTracker.App.Core.Processing;
using TransportTracker.App.Models;

namespace TransportTracker.App.Views.Maps.Overlays
{
    /// <summary>
    /// Manages heatmap visualization for transport data
    /// </summary>
    public class HeatmapManager
    {
        private readonly Map _map;
        private readonly HeatmapLayer _heatmapLayer;
        private readonly TransportBatchService _batchService;
        
        private CancellationTokenSource _updateCts;
        
        /// <summary>
        /// Gets or sets whether the heatmap is visible
        /// </summary>
        public bool IsVisible
        {
            get => _heatmapLayer.IsVisible;
            set => _heatmapLayer.IsVisible = value;
        }
        
        /// <summary>
        /// Gets or sets the opacity of the heatmap (0.0 - 1.0)
        /// </summary>
        public double Opacity
        {
            get => _heatmapLayer.Opacity;
            set => _heatmapLayer.Opacity = value;
        }
        
        /// <summary>
        /// Gets or sets the maximum radius for heatmap points in pixels
        /// </summary>
        public double MaxRadius
        {
            get => _heatmapLayer.MaxRadius;
            set => _heatmapLayer.MaxRadius = value;
        }
        
        /// <summary>
        /// Gets the current update progress
        /// </summary>
        public Progress<BatchProcessingProgress> UpdateProgress { get; }
        
        /// <summary>
        /// Event raised when update progress changes
        /// </summary>
        public event EventHandler<BatchProcessingProgress> ProgressChanged;

        /// <summary>
        /// Creates a new heatmap manager for the specified map
        /// </summary>
        public HeatmapManager(Map map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _heatmapLayer = new HeatmapLayer(map);
            
            // Create progress reporter
            UpdateProgress = new Progress<BatchProcessingProgress>(progress =>
            {
                ProgressChanged?.Invoke(this, progress);
            });
            
            // Create batch service with progress reporting
            _batchService = new TransportBatchService(UpdateProgress);
        }

        /// <summary>
        /// Updates the heatmap with vehicle data
        /// </summary>
        public async Task UpdateFromVehiclesAsync(IEnumerable<TransportVehicle> vehicles, 
            double baseRadius = 0.005, CancellationToken cancellationToken = default)
        {
            if (vehicles == null)
                return;
                
            // Cancel any ongoing updates
            CancelUpdates();
            
            // Create a new cancellation token that combines the passed token and our internal one
            _updateCts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _updateCts.Token);
            
            try
            {
                // Extract locations from vehicles
                var locations = vehicles.Select(v => v.Location).ToList();
                
                // Update heatmap with vehicle locations
                await _heatmapLayer.GenerateFromLocationsAsync(
                    locations, 
                    "vehicles", 
                    baseRadius,
                    1.0,
                    UpdateProgress);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating heatmap: {ex.Message}");
            }
            finally
            {
                linkedCts.Dispose();
            }
        }

        /// <summary>
        /// Updates the heatmap with transport stop data
        /// </summary>
        public async Task UpdateFromStopsAsync(IEnumerable<TransportStop> stops,
            double baseRadius = 0.01, CancellationToken cancellationToken = default)
        {
            if (stops == null)
                return;
                
            // Cancel any ongoing updates
            CancelUpdates();
            
            // Create a new cancellation token that combines the passed token and our internal one
            _updateCts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _updateCts.Token);
            
            try
            {
                // Extract locations from stops
                var locations = stops.Select(s => s.Location).ToList();
                
                // Update heatmap with stop locations
                await _heatmapLayer.GenerateFromLocationsAsync(
                    locations, 
                    "stops", 
                    baseRadius,
                    1.0,
                    UpdateProgress);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating heatmap: {ex.Message}");
            }
            finally
            {
                linkedCts.Dispose();
            }
        }
        
        /// <summary>
        /// Updates the heatmap with combined vehicle and stop data
        /// </summary>
        public async Task UpdateFromCombinedDataAsync(
            IEnumerable<TransportVehicle> vehicles,
            IEnumerable<TransportStop> stops,
            double baseRadius = 0.008,
            CancellationToken cancellationToken = default)
        {
            if (vehicles == null && stops == null)
                return;
                
            // Cancel any ongoing updates
            CancelUpdates();
            
            // Create a new cancellation token that combines the passed token and our internal one
            _updateCts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _updateCts.Token);
            
            try
            {
                // Combine locations from both sources
                var locations = new List<Location>();
                
                if (vehicles != null)
                    locations.AddRange(vehicles.Select(v => v.Location));
                
                if (stops != null)
                    locations.AddRange(stops.Select(s => s.Location));
                
                // Update heatmap with combined locations
                await _heatmapLayer.GenerateFromLocationsAsync(
                    locations, 
                    "combined", 
                    baseRadius,
                    1.0,
                    UpdateProgress);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating heatmap: {ex.Message}");
            }
            finally
            {
                linkedCts.Dispose();
            }
        }
        
        /// <summary>
        /// Cancels any ongoing heatmap updates
        /// </summary>
        public void CancelUpdates()
        {
            _updateCts?.Cancel();
            _updateCts?.Dispose();
            _updateCts = null;
        }

        /// <summary>
        /// Clears the heatmap data
        /// </summary>
        public void Clear()
        {
            _heatmapLayer.Clear();
        }
        
        /// <summary>
        /// Changes the color gradient for the heatmap
        /// </summary>
        public void SetColorGradient(ColorGradient gradient)
        {
            // Create new heatmap layer with updated gradient
            var newLayer = new HeatmapLayer(_map, gradient)
            {
                IsVisible = _heatmapLayer.IsVisible,
                Opacity = _heatmapLayer.Opacity,
                MaxRadius = _heatmapLayer.MaxRadius
            };
            
            // Copy points to new layer
            foreach (var point in _heatmapLayer.Points)
            {
                newLayer.AddPoint(point);
            }
            
            // Clear old layer
            _heatmapLayer.Clear();
            
            // Replace with new layer
            _heatmapLayer.Clear();
        }
    }
}
