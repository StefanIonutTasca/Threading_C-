using Microsoft.Maui.Graphics;

namespace TransportTracker.App.Views.Maps
{
    /// <summary>
    /// Represents route information for display in the map view
    /// </summary>
    public class RouteInfo
    {
        /// <summary>
        /// Gets or sets the distance of the route in kilometers.
        /// </summary>
        public double Distance { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the route
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// Gets or sets the display name of the route
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets or sets the short code for the route (e.g., "A12", "Blue Line")
        /// </summary>
        public string Code { get; set; }
        
        /// <summary>
        /// Gets or sets the route type (e.g., "Bus", "Train", "Subway")
        /// </summary>
        public string Type { get; set; }
        
        /// <summary>
        /// Gets or sets the color associated with this route
        /// </summary>
        public Color Color { get; set; }
        
        /// <summary>
        /// Gets or sets whether the route is currently active in the service
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Gets or sets whether the route is currently visible on the map
        /// </summary>
        public bool IsVisible { get; set; } = true;
        
        /// <summary>
        /// Gets or sets whether this route is currently selected
        /// </summary>
        public bool IsSelected { get; set; }
        
        /// <summary>
        /// Gets or sets the number of vehicles currently operating on this route
        /// </summary>
        public int VehicleCount { get; set; }
        
        /// <summary>
        /// Gets or sets the origin stop name
        /// </summary>
        public string Origin { get; set; }
        
        /// <summary>
        /// Gets or sets the destination stop name
        /// </summary>
        public string Destination { get; set; }
        
        /// <summary>
        /// Gets or sets the frequency of service in minutes
        /// </summary>
        public int FrequencyMinutes { get; set; }
        
        /// <summary>
        /// Gets or sets whether the route is bidirectional
        /// </summary>
        public bool IsBidirectional { get; set; } = true;
        
        /// <summary>
        /// Returns a summary description of the route
        /// </summary>
        public string Description => $"{Origin} → {Destination} | {(FrequencyMinutes > 0 ? $"Every {FrequencyMinutes} min" : "No schedule")}";
    }
}
