using System;

namespace TransportTracker.App.Views.Maps
{
    /// <summary>
    /// Represents a public transport vehicle with real-time information.
    /// </summary>
    public class TransportVehicle
    {
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
        /// Gets the occupancy percentage based on the capacity and current occupancy.
        /// </summary>
        public double OccupancyPercentage => Capacity > 0 ? (double)Occupancy / Capacity * 100 : 0;

        /// <summary>
        /// Gets a value indicating whether the vehicle is delayed.
        /// </summary>
        public bool IsDelayed => Status?.Contains("Delayed") ?? false;

        /// <summary>
        /// Gets a value indicating whether the vehicle is out of service.
        /// </summary>
        public bool IsOutOfService => Status?.Contains("Out of Service") ?? false;

        /// <summary>
        /// Gets a descriptive string for the vehicle's next arrival.
        /// </summary>
        public string NextArrivalInfo => $"Next stop: {NextStop}";

        /// <summary>
        /// Gets a descriptive string for the vehicle's occupancy.
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
