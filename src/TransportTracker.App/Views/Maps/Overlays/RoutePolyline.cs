using System.Collections.ObjectModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Graphics;

namespace TransportTracker.App.Views.Maps.Overlays
{
    /// <summary>
    /// Represents a polyline overlay for displaying transport route paths on a map
    /// </summary>
    public class RoutePolyline : MapElement
    {
        /// <summary>
        /// Gets or sets the ID of the route associated with this polyline
        /// </summary>
        public string RouteId { get; set; }
        
        /// <summary>
        /// Gets or sets the stroke color of the polyline
        /// </summary>
        public Color StrokeColor { get; set; } = Colors.Blue;
        
        /// <summary>
        /// Gets or sets the width of the polyline stroke
        /// </summary>
        public float StrokeWidth { get; set; } = 5f;
        
        /// <summary>
        /// Gets or sets whether the polyline should be rendered with dashed lines
        /// </summary>
        public bool IsDashed { get; set; } = false;
        
        /// <summary>
        /// Gets or sets the collection of geographic coordinates that make up the polyline
        /// </summary>
        public ObservableCollection<Location> Geopath { get; set; } = new ObservableCollection<Location>();
        
        /// <summary>
        /// Gets or sets the Z-index of the polyline to control rendering order
        /// </summary>
        public int ZIndex { get; set; } = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="RoutePolyline"/> class
        /// </summary>
        public RoutePolyline()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RoutePolyline"/> class with an initial geopath
        /// </summary>
        /// <param name="geopath">The coordinates that make up the polyline path</param>
        public RoutePolyline(IEnumerable<Location> geopath)
        {
            Geopath = new ObservableCollection<Location>(geopath);
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RoutePolyline"/> class with full customization
        /// </summary>
        /// <param name="routeId">ID of the route</param>
        /// <param name="geopath">Coordinates that make up the polyline path</param>
        /// <param name="strokeColor">Color of the polyline</param>
        /// <param name="strokeWidth">Width of the polyline stroke</param>
        /// <param name="isDashed">Whether the polyline should be rendered with dashes</param>
        public RoutePolyline(string routeId, IEnumerable<Location> geopath, Color strokeColor, float strokeWidth = 5f, bool isDashed = false)
        {
            RouteId = routeId;
            Geopath = new ObservableCollection<Location>(geopath);
            StrokeColor = strokeColor;
            StrokeWidth = strokeWidth;
            IsDashed = isDashed;
        }
    }
}
