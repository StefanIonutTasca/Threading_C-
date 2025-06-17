using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Graphics;
using TransportTracker.App.Core.Processing;

namespace TransportTracker.App.Views.Maps.Overlays
{
    /// <summary>
    /// Provides a heatmap overlay for visualizing transport density on a map
    /// </summary>
    public class HeatmapLayer
    {
        private readonly Map _map;
        private readonly ObservableCollection<HeatmapPoint> _heatmapPoints = new();
        private readonly Dictionary<HeatmapPoint, MapElement> _mapElements = new();
        private readonly Dictionary<string, List<HeatmapPoint>> _pointsByType = new();
        private readonly ColorGradient _colorGradient;
        
        private bool _isVisible = true;
        private double _opacity = 0.7;
        private double _maxRadius = 100;

        /// <summary>
        /// Gets or sets whether the heatmap layer is visible
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    UpdateVisibility();
                }
            }
        }

        /// <summary>
        /// Gets or sets the opacity of the heatmap layer (0.0 - 1.0)
        /// </summary>
        public double Opacity
        {
            get => _opacity;
            set
            {
                if (_opacity != value)
                {
                    _opacity = Math.Clamp(value, 0.0, 1.0);
                    UpdateOpacity();
                }
            }
        }
        
        /// <summary>
        /// Gets or sets the maximum radius for heatmap points in screen pixels
        /// </summary>
        public double MaxRadius
        {
            get => _maxRadius;
            set
            {
                if (_maxRadius != value && value > 0)
                {
                    _maxRadius = value;
                    RefreshAllPoints();
                }
            }
        }
        
        /// <summary>
        /// Gets the current points in the heatmap
        /// </summary>
        public IReadOnlyCollection<HeatmapPoint> Points => _heatmapPoints;
        
        /// <summary>
        /// Gets the color gradient used for intensity visualization
        /// </summary>
        public ColorGradient Gradient => _colorGradient;

        /// <summary>
        /// Creates a new heatmap layer attached to the specified map
        /// </summary>
        public HeatmapLayer(Map map, ColorGradient gradient = null)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _colorGradient = gradient ?? new ColorGradient(
                new[] { 
                    Colors.Green.WithAlpha(0.4f), 
                    Colors.Yellow.WithAlpha(0.6f), 
                    Colors.Orange.WithAlpha(0.7f),
                    Colors.Red.WithAlpha(0.8f)
                }
            );
            
            // Handle point collection changes
            _heatmapPoints.CollectionChanged += OnHeatmapPointsChanged;
        }

        /// <summary>
        /// Add a single heatmap point to the map
        /// </summary>
        public void AddPoint(HeatmapPoint point)
        {
            if (point == null)
                return;
                
            _heatmapPoints.Add(point);
        }
        
        /// <summary>
        /// Updates the heatmap with a collection of points
        /// </summary>
        public void UpdatePoints(IEnumerable<HeatmapPoint> points, string type = null)
        {
            if (points == null)
                return;
                
            // Remove existing points of this type if specified
            if (type != null && _pointsByType.TryGetValue(type, out var existingPoints))
            {
                foreach (var point in existingPoints)
                {
                    RemovePoint(point);
                }
                _pointsByType[type] = new List<HeatmapPoint>();
            }
            
            // Add new points
            foreach (var point in points)
            {
                AddPoint(point);
                
                // Track points by type if specified
                if (type != null)
                {
                    if (!_pointsByType.TryGetValue(type, out var typePoints))
                    {
                        typePoints = new List<HeatmapPoint>();
                        _pointsByType[type] = typePoints;
                    }
                    typePoints.Add(point);
                }
            }
        }
        
        /// <summary>
        /// Removes a specific point from the heatmap
        /// </summary>
        public void RemovePoint(HeatmapPoint point)
        {
            if (point == null)
                return;
                
            _heatmapPoints.Remove(point);
        }
        
        /// <summary>
        /// Clears all heatmap points
        /// </summary>
        public void Clear()
        {
            _heatmapPoints.Clear();
            _pointsByType.Clear();
        }
        
        /// <summary>
        /// Clears points of a specific type
        /// </summary>
        public void ClearType(string type)
        {
            if (type == null || !_pointsByType.TryGetValue(type, out var points))
                return;
                
            foreach (var point in points.ToList())
            {
                RemovePoint(point);
            }
            
            _pointsByType.Remove(type);
        }
        
        /// <summary>
        /// Generate heatmap data from vehicle and stop locations
        /// </summary>
        public async Task GenerateFromLocationsAsync(
            IEnumerable<Location> locations,
            string type = null,
            double baseRadius = 0.01,
            double maxIntensity = 1.0,
            IProgress<BatchProcessingProgress> progress = null)
        {
            if (locations == null)
                return;
                
            // Use batch processing service to generate points
            var batchService = new TransportBatchService(progress);
            var heatmapPoints = await batchService.GenerateHeatmapDataAsync(
                locations,
                baseRadius,
                maxIntensity
            );
            
            // Update the heatmap with generated points
            UpdatePoints(heatmapPoints, type);
        }

        #region Private Methods
        
        /// <summary>
        /// Handles changes to the heatmap points collection
        /// </summary>
        private void OnHeatmapPointsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (HeatmapPoint point in e.NewItems)
                    {
                        AddPointToMap(point);
                    }
                    break;
                    
                case NotifyCollectionChangedAction.Remove:
                    foreach (HeatmapPoint point in e.OldItems)
                    {
                        RemovePointFromMap(point);
                    }
                    break;
                    
                case NotifyCollectionChangedAction.Replace:
                    foreach (HeatmapPoint point in e.OldItems)
                    {
                        RemovePointFromMap(point);
                    }
                    foreach (HeatmapPoint point in e.NewItems)
                    {
                        AddPointToMap(point);
                    }
                    break;
                    
                case NotifyCollectionChangedAction.Reset:
                    RemoveAllPointsFromMap();
                    break;
            }
        }
        
        /// <summary>
        /// Adds a specific heatmap point to the map
        /// </summary>
        private void AddPointToMap(HeatmapPoint point)
        {
            if (_map == null || point == null || _mapElements.ContainsKey(point))
                return;
                
            // Create heatmap circle element
            var circle = new Circle
            {
                Center = point.Location,
                // Convert radius to determine appearance
                Radius = Distance.FromKilometers(point.Radius),
                StrokeColor = Colors.Transparent,
                FillColor = _colorGradient.GetColor(point.Intensity).WithAlpha((float)_opacity),
                ZIndex = 10 // Below pins but above standard polylines
            };
            
            // Add circle to map
            _map.MapElements.Add(circle);
            
            // Track the element
            _mapElements[point] = circle;
        }
        
        /// <summary>
        /// Removes a specific heatmap point from the map
        /// </summary>
        private void RemovePointFromMap(HeatmapPoint point)
        {
            if (_map == null || point == null || !_mapElements.TryGetValue(point, out var element))
                return;
                
            // Remove from map
            _map.MapElements.Remove(element);
            
            // Remove from tracking
            _mapElements.Remove(point);
        }
        
        /// <summary>
        /// Removes all heatmap points from the map
        /// </summary>
        private void RemoveAllPointsFromMap()
        {
            if (_map == null)
                return;
                
            // Remove all tracked elements from map
            foreach (var element in _mapElements.Values)
            {
                _map.MapElements.Remove(element);
            }
            
            // Clear tracking
            _mapElements.Clear();
        }
        
        /// <summary>
        /// Updates visibility of all heatmap elements
        /// </summary>
        private void UpdateVisibility()
        {
            if (_map == null)
                return;
                
            foreach (var element in _mapElements.Values)
            {
                if (element is Circle circle)
                {
                    circle.IsVisible = _isVisible;
                }
            }
        }
        
        /// <summary>
        /// Updates opacity of all heatmap elements
        /// </summary>
        private void UpdateOpacity()
        {
            if (_map == null)
                return;
                
            foreach (var pair in _mapElements)
            {
                if (pair.Value is Circle circle && pair.Key is HeatmapPoint point)
                {
                    // Update color with new opacity
                    circle.FillColor = _colorGradient.GetColor(point.Intensity).WithAlpha((float)_opacity);
                }
            }
        }
        
        /// <summary>
        /// Refreshes all points on the map
        /// </summary>
        private void RefreshAllPoints()
        {
            RemoveAllPointsFromMap();
            
            foreach (var point in _heatmapPoints)
            {
                AddPointToMap(point);
            }
        }
        
        #endregion
    }

    /// <summary>
    /// Provides color interpolation for heatmap visualization
    /// </summary>
    public class ColorGradient
    {
        private readonly Color[] _colors;

        /// <summary>
        /// Creates a new color gradient with specified colors
        /// </summary>
        public ColorGradient(Color[] colors)
        {
            _colors = colors?.Length > 0 ? colors : new[] { Colors.Red };
        }

        /// <summary>
        /// Gets a color for the specified intensity value (0.0 - 1.0)
        /// </summary>
        public Color GetColor(double intensity)
        {
            // Clamp intensity to valid range
            intensity = Math.Clamp(intensity, 0.0, 1.0);
            
            if (_colors.Length == 1)
                return _colors[0];
                
            // Calculate position within gradient
            double position = intensity * (_colors.Length - 1);
            int index = (int)position;
            double remainder = position - index;
            
            // Handle edge case
            if (index >= _colors.Length - 1)
                return _colors[^1];
                
            // Interpolate between two colors
            var color1 = _colors[index];
            var color2 = _colors[index + 1];
            
            return InterpolateColor(color1, color2, remainder);
        }
        
        /// <summary>
        /// Interpolates between two colors
        /// </summary>
        private Color InterpolateColor(Color color1, Color color2, double amount)
        {
            return new Color(
                (float)(color1.Red + (color2.Red - color1.Red) * amount),
                (float)(color1.Green + (color2.Green - color1.Green) * amount),
                (float)(color1.Blue + (color2.Blue - color1.Blue) * amount),
                (float)(color1.Alpha + (color2.Alpha - color1.Alpha) * amount)
            );
        }
    }
}
