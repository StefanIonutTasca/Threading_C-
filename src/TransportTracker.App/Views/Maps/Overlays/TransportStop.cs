using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Graphics;

namespace TransportTracker.App.Views.Maps.Overlays
{
    /// <summary>
    /// Represents a transport stop or station on the map
    /// </summary>
    public class TransportStop : Pin
    {
        /// <summary>
        /// Gets or sets the unique identifier of the stop
        /// </summary>
        public string StopId { get; set; }
        
        /// <summary>
        /// Gets or sets the type of stop (bus stop, train station, etc.)
        /// </summary>
        public string StopType { get; set; }
        
        /// <summary>
        /// Gets or sets the primary route associated with this stop
        /// </summary>
        public string PrimaryRoute { get; set; }
        
        /// <summary>
        /// Gets or sets the list of route IDs that service this stop
        /// </summary>
        public List<string> Routes { get; set; } = new List<string>();
        
        /// <summary>
        /// Gets or sets the color of the stop icon
        /// </summary>
        public Color StopColor { get; set; }
        
        /// <summary>
        /// Gets or sets whether the stop is currently active (service available)
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Gets or sets the next arrival time at this stop
        /// </summary>
        public DateTime? NextArrival { get; set; }
        
        /// <summary>
        /// Gets or sets whether the stop is wheelchair accessible
        /// </summary>
        public bool IsAccessible { get; set; }
        
        /// <summary>
        /// Gets or sets whether the stop has multiple levels/platforms
        /// </summary>
        public bool HasMultipleLevels { get; set; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="TransportStop"/> class
        /// </summary>
        public TransportStop()
        {
            // Set default pin style
            IsDraggable = false;
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="TransportStop"/> class with parameters
        /// </summary>
        /// <param name="stopId">The unique identifier of the stop</param>
        /// <param name="location">The geographic location of the stop</param>
        /// <param name="name">The name of the stop</param>
        /// <param name="stopType">The type of stop</param>
        /// <param name="routes">List of routes servicing this stop</param>
        public TransportStop(string stopId, Location location, string name, string stopType, List<string> routes = null)
        {
            StopId = stopId;
            Location = location;
            Label = name;
            StopType = stopType;
            Routes = routes ?? new List<string>();
            
            // Set the address field to show routes
            UpdateRouteDisplay();
            
            // Set styling based on type
            SetStopStyling(stopType);
            
            IsDraggable = false;
        }
        
        /// <summary>
        /// Adds a route to this stop
        /// </summary>
        /// <param name="routeId">The ID of the route to add</param>
        public void AddRoute(string routeId)
        {
            if (!Routes.Contains(routeId))
            {
                Routes.Add(routeId);
                UpdateRouteDisplay();
            }
        }
        
        /// <summary>
        /// Updates the next arrival time at this stop
        /// </summary>
        /// <param name="arrival">The next arrival time</param>
        public void UpdateNextArrival(DateTime? arrival)
        {
            NextArrival = arrival;
            UpdateDescription();
        }
        
        /// <summary>
        /// Set the stop's styling based on its type
        /// </summary>
        /// <param name="stopType">Type of the stop</param>
        private void SetStopStyling(string stopType)
        {
            switch (stopType?.ToLower())
            {
                case "bus":
                    StopColor = Colors.Blue;
                    ImageSource = "bus_stop.png";
                    break;
                case "train":
                    StopColor = Colors.Orange;
                    ImageSource = "train_station.png";
                    break;
                case "tram":
                    StopColor = Colors.Green;
                    ImageSource = "tram_stop.png";
                    break;
                case "subway":
                    StopColor = Colors.Red;
                    ImageSource = "subway_station.png";
                    break;
                case "ferry":
                    StopColor = Colors.Cyan;
                    ImageSource = "ferry_terminal.png";
                    break;
                case "hub":
                case "transit_center":
                    StopColor = Colors.Purple;
                    ImageSource = "transit_hub.png";
                    break;
                default:
                    StopColor = Colors.Gray;
                    ImageSource = "stop.png";
                    break;
            }
        }
        
        /// <summary>
        /// Updates the route display in the address field
        /// </summary>
        private void UpdateRouteDisplay()
        {
            if (Routes != null && Routes.Any())
            {
                Address = $"Routes: {string.Join(", ", Routes)}";
                
                // Set primary route if not already set
                PrimaryRoute ??= Routes.FirstOrDefault();
            }
            else
            {
                Address = "No routes";
            }
        }
        
        /// <summary>
        /// Updates the description to include next arrival time if available
        /// </summary>
        private void UpdateDescription()
        {
            var description = UpdateRouteDisplay;
            
            if (NextArrival.HasValue)
            {
                var now = DateTime.Now;
                var timeUntilArrival = NextArrival.Value - now;
                
                if (timeUntilArrival.TotalMinutes > 0 && timeUntilArrival.TotalHours < 1)
                {
                    Address += $" | Next arrival in {(int)timeUntilArrival.TotalMinutes} min";
                }
                else if (timeUntilArrival.TotalMinutes > 0)
                {
                    Address += $" | Next arrival at {NextArrival.Value:HH:mm}";
                }
            }
        }
    }
}
