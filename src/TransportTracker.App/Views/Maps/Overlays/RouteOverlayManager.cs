using MauiMap = Microsoft.Maui.Controls.Maps.Map;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Graphics;
using System.Collections.ObjectModel;

namespace TransportTracker.App.Views.Maps.Overlays
{
    /// <summary>
    /// Manages route overlays and related transport stops on a map
    /// </summary>
    public class RouteOverlayManager
    {
        // Reference to the map to add elements to
        private readonly MauiMap _map;
        
        // Collection of active route polylines
        private readonly Dictionary<string, RoutePolyline> _activeRoutes = new();
        
        // Collection of transport stops by route ID
        private readonly Dictionary<string, List<TransportStop>> _routeStops = new();
        
        // Route color mappings
        private readonly Dictionary<string, Color> _routeColors = new();
        
        /// <summary>
        /// Gets the collection of currently active routes
        /// </summary>
        public IReadOnlyDictionary<string, RoutePolyline> ActiveRoutes => _activeRoutes;
        
        /// <summary>
        /// Gets the collection of stops organized by route
        /// </summary>
        public IReadOnlyDictionary<string, List<TransportStop>> RouteStops => _routeStops;

        /// <summary>
        /// Initializes a new instance of the <see cref="RouteOverlayManager"/> class
        /// </summary>
        /// <param name="map">The map to add routes and stops to</param>
        public RouteOverlayManager(MauiMap map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
        }
        
        /// <summary>
        /// Adds or updates a route on the map
        /// </summary>
        /// <param name="routeId">ID of the route</param>
        /// <param name="routeName">Display name of the route</param>
        /// <param name="path">Collection of locations that form the route path</param>
        /// <param name="color">Optional color for the route (null for automatic)</param>
        /// <param name="lineWidth">Width of the route line</param>
        /// <param name="isDashed">Whether the route should be displayed with dashed lines</param>
        /// <returns>The created or updated route polyline</returns>
        public RoutePolyline AddOrUpdateRoute(
            string routeId, 
            string routeName, 
            IEnumerable<Location> path, 
            Color? color = null, 
            float lineWidth = 5f,
            bool isDashed = false)
        {
            // Assign color if not specified
            var routeColor = color ?? GetOrCreateRouteColor(routeId);
            
            // Check if we already have this route
            if (_activeRoutes.TryGetValue(routeId, out var existingRoute))
            {
                // Update existing route
                existingRoute.Geopath.Clear();
                foreach (var point in path)
                {
                    existingRoute.Geopath.Add(point);
                }
                existingRoute.StrokeColor = routeColor;
                existingRoute.StrokeWidth = lineWidth;
                existingRoute.IsDashed = isDashed;
                
                return existingRoute;
            }
            
            // Create new route
            var newRoute = new RoutePolyline(routeId, path, routeColor, lineWidth, isDashed);
            
            // Add to map
            _map.MapElements.Add(newRoute);
            _activeRoutes[routeId] = newRoute;
            
            return newRoute;
        }
        
        /// <summary>
        /// Adds a transport stop to the map and associates it with a route
        /// </summary>
        /// <param name="stop">The stop to add</param>
        /// <param name="routeId">ID of the route this stop belongs to</param>
        /// <returns>True if the stop was added, false if it already exists</returns>
        public bool AddStopToRoute(TransportStop stop, string routeId)
        {
            if (stop == null || string.IsNullOrEmpty(routeId))
                return false;
                
            // Add route to stop's route list if not already there
            if (!stop.Routes.Contains(routeId))
            {
                stop.AddRoute(routeId);
            }
            
            // Initialize route's stop list if needed
            if (!_routeStops.ContainsKey(routeId))
            {
                _routeStops[routeId] = new List<TransportStop>();
            }
            
            // Don't add duplicate stops
            if (_routeStops[routeId].Any(s => s.StopId == stop.StopId))
                return false;
                
            // Add stop to route's stop list
            _routeStops[routeId].Add(stop);
            
            // Add stop to map if it's not already there
            if (!_map.Pins.Contains(stop))
            {
                _map.Pins.Add(stop);
            }
            
            return true;
        }
        
        /// <summary>
        /// Adds multiple stops to a route
        /// </summary>
        /// <param name="stops">Collection of stops to add</param>
        /// <param name="routeId">ID of the route these stops belong to</param>
        public void AddStopsToRoute(IEnumerable<TransportStop> stops, string routeId)
        {
            if (stops == null || string.IsNullOrEmpty(routeId))
                return;
                
            foreach (var stop in stops)
            {
                AddStopToRoute(stop, routeId);
            }
        }
        
        /// <summary>
        /// Removes a route and optionally its associated stops from the map
        /// </summary>
        /// <param name="routeId">ID of the route to remove</param>
        /// <param name="removeStops">Whether to also remove the route's stops</param>
        /// <returns>True if the route was removed, false if it wasn't found</returns>
        public bool RemoveRoute(string routeId, bool removeStops = false)
        {
            if (!_activeRoutes.TryGetValue(routeId, out var route))
                return false;
                
            // Remove route from map
            _map.MapElements.Remove(route);
            _activeRoutes.Remove(routeId);
            
            // Remove stops if requested
            if (removeStops && _routeStops.TryGetValue(routeId, out var stops))
            {
                foreach (var stop in stops)
                {
                    stop.Routes.Remove(routeId);
                    
                    // Only remove stop from map if it has no more routes
                    if (stop.Routes.Count == 0)
                    {
                        _map.Pins.Remove(stop);
                    }
                }
                
                _routeStops.Remove(routeId);
            }
            
            return true;
        }
        
        /// <summary>
        /// Shows or hides a route and its stops on the map
        /// </summary>
        /// <param name="routeId">ID of the route to toggle</param>
        /// <param name="visible">Whether the route should be visible</param>
        /// <param name="toggleStops">Whether to also toggle visibility of the route's stops</param>
        /// <returns>True if the route was found and toggled</returns>
        public bool ToggleRouteVisibility(string routeId, bool visible, bool toggleStops = true)
        {
            if (!_activeRoutes.TryGetValue(routeId, out var route))
                return false;
                
            // Toggle route visibility
            route.IsVisible = visible;
            
            // Toggle stop visibility if requested
            if (toggleStops && _routeStops.TryGetValue(routeId, out var stops))
            {
                foreach (var stop in stops)
                {
                    // Don't hide stops that belong to other visible routes too
                    if (!visible)
                    {
                        bool shouldHide = true;
                        
                        // Check if stop belongs to any other visible routes
                        foreach (var otherRouteId in stop.Routes)
                        {
                            if (otherRouteId != routeId && 
                                _activeRoutes.TryGetValue(otherRouteId, out var otherRoute) && 
                                otherRoute.IsVisible)
                            {
                                shouldHide = false;
                                break;
                            }
                        }
                        
                        stop.IsVisible = !shouldHide;
                    }
                    else
                    {
                        stop.IsVisible = true;
                    }
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Highlights a route by increasing its width and adjusting other routes
        /// </summary>
        /// <param name="routeId">ID of the route to highlight</param>
        /// <param name="highlight">Whether to highlight the route</param>
        /// <returns>True if the route was found and highlighted</returns>
        public bool HighlightRoute(string routeId, bool highlight)
        {
            if (!_activeRoutes.TryGetValue(routeId, out var route))
                return false;
                
            const float normalWidth = 5f;
            const float highlightedWidth = 8f;
            const float fadedWidth = 3f;
            
            // Adjust the target route
            route.StrokeWidth = highlight ? highlightedWidth : normalWidth;
            route.ZIndex = highlight ? 1 : 0;
            
            // Adjust other routes
            if (highlight)
            {
                foreach (var otherRoute in _activeRoutes.Values.Where(r => r.RouteId != routeId))
                {
                    otherRoute.StrokeWidth = fadedWidth;
                }
            }
            else
            {
                // Reset all routes to normal
                foreach (var otherRoute in _activeRoutes.Values)
                {
                    otherRoute.StrokeWidth = normalWidth;
                    otherRoute.ZIndex = 0;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Gets or generates a consistent color for a route based on its ID
        /// </summary>
        /// <param name="routeId">ID of the route</param>
        /// <returns>Color for the route</returns>
        private Color GetOrCreateRouteColor(string routeId)
        {
            // Return existing color if we have one
            if (_routeColors.TryGetValue(routeId, out var existingColor))
            {
                return existingColor;
            }
            
            // Create a deterministic color based on the route ID
            // This ensures the same route always gets the same color
            var hashCode = routeId.GetHashCode();
            
            // Use predefined colors for common transport types if identifiable from routeId
            if (routeId.Contains("bus", StringComparison.OrdinalIgnoreCase))
            {
                _routeColors[routeId] = Colors.Blue;
            }
            else if (routeId.Contains("tram", StringComparison.OrdinalIgnoreCase))
            {
                _routeColors[routeId] = Colors.Green;
            }
            else if (routeId.Contains("subway", StringComparison.OrdinalIgnoreCase) || 
                     routeId.Contains("metro", StringComparison.OrdinalIgnoreCase))
            {
                _routeColors[routeId] = Colors.Red;
            }
            else if (routeId.Contains("train", StringComparison.OrdinalIgnoreCase))
            {
                _routeColors[routeId] = Colors.Orange;
            }
            else
            {
                // Generate a color based on hashcode
                byte r = (byte)((hashCode & 0xFF0000) >> 16);
                byte g = (byte)((hashCode & 0x00FF00) >> 8);
                byte b = (byte)(hashCode & 0x0000FF);
                
                // Ensure reasonable saturation and brightness
                var hasLowSaturation = Math.Max(Math.Max(r, g), b) - Math.Min(Math.Min(r, g), b) < 30;
                var isDark = r + g + b < 300;
                
                if (hasLowSaturation)
                {
                    // Make more saturated by boosting highest component
                    var max = Math.Max(Math.Max(r, g), b);
                    if (r == max) r = (byte)Math.Min(r + 50, 255);
                    else if (g == max) g = (byte)Math.Min(g + 50, 255);
                    else b = (byte)Math.Min(b + 50, 255);
                }
                
                if (isDark)
                {
                    // Make brighter
                    r = (byte)Math.Min(r + 60, 255);
                    g = (byte)Math.Min(g + 60, 255);
                    b = (byte)Math.Min(b + 60, 255);
                }
                
                _routeColors[routeId] = Color.FromRgb(r, g, b);
            }
            
            return _routeColors[routeId];
        }
        
        /// <summary>
        /// Clears all routes and stops from the map
        /// </summary>
        public void ClearAll()
        {
            // Remove all route polylines
            foreach (var route in _activeRoutes.Values)
            {
                _map.MapElements.Remove(route);
            }
            
            // Remove all stops
            foreach (var stopsList in _routeStops.Values)
            {
                foreach (var stop in stopsList)
                {
                    _map.Pins.Remove(stop);
                }
            }
            
            // Clear collections
            _activeRoutes.Clear();
            _routeStops.Clear();
        }
    }
}
