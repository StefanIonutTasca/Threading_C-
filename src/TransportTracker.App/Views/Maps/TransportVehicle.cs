using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TransportTracker.App.Views.Maps
{
    /// <summary>
    /// Represents a public transport vehicle with real-time information.
    /// </summary>
    public class TransportVehicle : INotifyPropertyChanged
    {
        // --- Additional properties for ViewModel/XAML compatibility ---
        /// <summary>
        /// Gets or sets the route number displayed for the vehicle.
        /// </summary>
        public string RouteNumber { get; set; }

        /// <summary>
        /// Gets or sets the current speed of the vehicle (km/h).
        /// </summary>
        public double CurrentSpeed { get; set; }

        /// <summary>
        /// Gets or sets the color for the vehicle's representation (e.g., on map or UI).
        /// </summary>
        public string Color { get; set; }

        /// <summary>
        /// Gets or sets whether the vehicle is delayed (for XAML assignment compatibility).
        /// </summary>
        public bool IsDelayed { get; set; } // Changed to get/set for assignment compatibility
        // --- Added properties to match all ViewModel and service usages ---
        /// <summary>
        /// Gets or sets the display label for the vehicle (for map pins, etc.).
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Gets or sets the display address or description for the vehicle location.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Gets or sets the vehicle's location (latitude/longitude as an object).
        /// </summary>
        public object Location { get; set; } // Use appropriate type if available (e.g., Location or Position)

        /// <summary>
        /// Gets or sets the route ID the vehicle is operating on.
        /// </summary>
        public string RouteId { get; set; }

        /// <summary>
        /// Gets or sets the delay in minutes for the vehicle.
        /// </summary>
        public int DelayMinutes { get; set; }

        /// <summary>
        /// Gets or sets the start location (object or string as appropriate).
        /// </summary>
        public object StartLocation { get; set; }

        /// <summary>
        /// Gets or sets the end location (object or string as appropriate).
        /// </summary>
        public object EndLocation { get; set; }

        /// <summary>
        /// Gets or sets the scheduled departure time.
        /// </summary>
        public DateTime? ScheduledDeparture { get; set; }

        /// <summary>
        /// Gets or sets the actual departure time.
        /// </summary>
        public DateTime? ActualDeparture { get; set; }

        /// <summary>
        /// Gets or sets the scheduled arrival time.
        /// </summary>
        public DateTime? ScheduledArrival { get; set; }

        /// <summary>
        /// Gets or sets the expected arrival time.
        /// </summary>
        public DateTime? ExpectedArrival { get; set; }
        // --- End added properties ---
        /// <summary>
        /// Gets or sets the unique identifier for the vehicle.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the vehicle number or identifier (e.g., "Bus 123").
        /// </summary>
        public string Number { get; set; }

        /// <summary>
        /// Gets or sets the type of transport vehicle (e.g., Bus, Train, Tram, Subway, Ferry).
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the route identifier that the vehicle is operating on.
        /// </summary>
        public string Route { get; set; }

        /// <summary>
        /// Gets or sets the next stop the vehicle will arrive at.
        /// </summary>
        public string NextStop { get; set; }

        /// <summary>
        /// Gets or sets the status of the vehicle (e.g., On Time, Delayed, Out of Service).
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the latitude coordinate of the vehicle's current position.
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Gets or sets the longitude coordinate of the vehicle's current position.
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Gets or sets the vehicle's current speed in kilometers per hour.
        /// </summary>
        public double Speed { get; set; }

        /// <summary>
        /// Gets or sets the heading or direction of the vehicle in degrees (0-360).
        /// </summary>
        public double Heading { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last update of the vehicle's position.
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Gets or sets the total capacity of the vehicle (number of passengers).
        /// </summary>
        public int Capacity { get; set; }

        /// <summary>
        /// Gets or sets the current occupancy of the vehicle (number of passengers).
        /// </summary>
        public int Occupancy { get; set; }


        /// <summary>
        /// Gets the occupancy percentage.
        /// </summary>
        public int OccupancyPercentage => Capacity > 0 ? (int)(((double)Occupancy / Capacity) * 100) : 0;

        /// <summary>
        /// Gets a value indicating whether the vehicle is delayed.
        /// </summary>
        

        #region INotifyPropertyChanged

        /// <summary>
        /// PropertyChanged event
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        /// <summary>
        /// Gets a value indicating whether the vehicle is out of service.
        /// </summary>
        public bool IsOutOfService => Status?.Contains("Out of Service") ?? false;

        /// <summary>
        /// Gets or sets a descriptive string for the vehicle's next arrival.
        /// </summary>
        public string NextArrivalInfo { get; set; }

        /// <summary>
        /// Gets a formatted string with occupancy information.
        /// </summary>
        public string OccupancyInfo => $"{Occupancy}/{Capacity} passengers ({OccupancyPercentage:F0}%)";

        /// <summary>
        /// Returns a string that represents the current vehicle.
        /// </summary>
        /// <returns>A string representation of the vehicle.</returns>
        public override string ToString()
        {
            return $"{Type} {Number} - {Route} ({Status})";
        }
    }
}
