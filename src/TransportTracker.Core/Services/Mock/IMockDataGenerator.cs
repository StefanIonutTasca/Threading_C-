using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TransportTracker.Core.Models;

namespace TransportTracker.Core.Services.Mock
{
    /// <summary>
    /// Interface for generating mock transport data for development and testing.
    /// Capable of creating large datasets (100k+ records) with realistic patterns.
    /// </summary>
    public interface IMockDataGenerator
    {
        /// <summary>
        /// Gets or sets the configuration for the mock data generator
        /// </summary>
        MockDataConfiguration Configuration { get; set; }
        
        /// <summary>
        /// Gets a value indicating whether the generator is currently running
        /// </summary>
        bool IsRunning { get; }
        
        /// <summary>
        /// Starts the mock data generation process
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task StartAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Stops the mock data generation process
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task StopAsync();
        
        /// <summary>
        /// Generates a fixed set of routes
        /// </summary>
        /// <param name="count">Number of routes to generate</param>
        /// <returns>A collection of generated routes</returns>
        IEnumerable<Route> GenerateRoutes(int count);
        
        /// <summary>
        /// Generates a fixed set of stops for the given routes
        /// </summary>
        /// <param name="routes">Routes to generate stops for</param>
        /// <param name="averageStopsPerRoute">Average number of stops per route</param>
        /// <returns>A collection of generated stops</returns>
        IEnumerable<Stop> GenerateStops(IEnumerable<Route> routes, int averageStopsPerRoute);
        
        /// <summary>
        /// Generates a fixed set of vehicles for the given routes
        /// </summary>
        /// <param name="routes">Routes to generate vehicles for</param>
        /// <param name="averageVehiclesPerRoute">Average number of vehicles per route</param>
        /// <returns>A collection of generated vehicles</returns>
        IEnumerable<Vehicle> GenerateVehicles(IEnumerable<Route> routes, int averageVehiclesPerRoute);
        
        /// <summary>
        /// Generates a set of schedules for the given routes and stops
        /// </summary>
        /// <param name="routes">Routes to generate schedules for</param>
        /// <param name="stops">Stops to include in schedules</param>
        /// <returns>A collection of generated schedules</returns>
        IEnumerable<Schedule> GenerateSchedules(IEnumerable<Route> routes, IEnumerable<Stop> stops);
        
        /// <summary>
        /// Generates the next batch of vehicle positions based on current state and time
        /// </summary>
        /// <param name="vehicles">Current vehicles to update</param>
        /// <param name="elapsedSeconds">Seconds elapsed since last update</param>
        /// <returns>Updated collection of vehicles with new positions</returns>
        IEnumerable<Vehicle> UpdateVehiclePositions(IEnumerable<Vehicle> vehicles, double elapsedSeconds);
        
        /// <summary>
        /// Event raised when mock data has been updated
        /// </summary>
        event EventHandler<MockDataUpdatedEventArgs> DataUpdated;
    }
    
    /// <summary>
    /// Configuration for mock data generation
    /// </summary>
    public class MockDataConfiguration
    {
        /// <summary>
        /// Gets or sets the number of routes to generate
        /// </summary>
        public int RouteCount { get; set; } = 20;
        
        /// <summary>
        /// Gets or sets the average number of stops per route
        /// </summary>
        public int AverageStopsPerRoute { get; set; } = 15;
        
        /// <summary>
        /// Gets or sets the average number of vehicles per route
        /// </summary>
        public int AverageVehiclesPerRoute { get; set; } = 5;

        /// <summary>
        /// Gets or sets the simulation speed factor (1.0 = real time)
        /// </summary>
        public double SimulationSpeedFactor { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets the schedule start time hour (e.g., 5 for 5:00 AM)
        /// </summary>
        public int ScheduleStartTimeHour { get; set; } = 5;

        /// <summary>
        /// Gets or sets the schedule end time hour (e.g., 23 for 11:00 PM)
        /// </summary>
        public int ScheduleEndTimeHour { get; set; } = 23;

        /// <summary>
        /// Gets or sets the average trip frequency in minutes
        /// </summary>
        public int AverageTripFrequencyMinutes { get; set; } = 15;
        
        /// <summary>
        /// Gets or sets the update interval in milliseconds
        /// </summary>
        public int UpdateIntervalMs { get; set; } = 1000;
        
        /// <summary>
        /// Gets or sets the average vehicle speed in km/h
        /// </summary>
        public double AverageVehicleSpeed { get; set; } = 30;
        
        /// <summary>
        /// Gets or sets the speed variation percentage (0.0 to 1.0)
        /// </summary>
        public double SpeedVariation { get; set; } = 0.3;
        
        /// <summary>
        /// Gets or sets the probability of a vehicle delay (0.0 to 1.0)
        /// </summary>
        public double DelayProbability { get; set; } = 0.15;
        
        /// <summary>
        /// Gets or sets the maximum delay in seconds
        /// </summary>
        public int MaxDelaySeconds { get; set; } = 300;
        
        /// <summary>
        /// Gets or sets the geographical center for generated data (latitude)
        /// </summary>
        public double CenterLatitude { get; set; } = 52.370216; // Amsterdam by default
        
        /// <summary>
        /// Gets or sets the geographical center for generated data (longitude)
        /// </summary>
        public double CenterLongitude { get; set; } = 4.895168; // Amsterdam by default
        
        /// <summary>
        /// Gets or sets the radius in kilometers for the generated data
        /// </summary>
        public double RadiusKm { get; set; } = 10;
        
        /// <summary>
        /// Gets or sets whether to simulate realistic rush hours
        /// </summary>
        public bool SimulateRushHours { get; set; } = true;
        
        /// <summary>
        /// Gets or sets whether to simulate weekend schedules
        /// </summary>
        public bool SimulateWeekends { get; set; } = true;
        
        /// <summary>
        /// Gets or sets the randomization seed (for reproducible results)
        /// </summary>
        public int? RandomSeed { get; set; }
        
        /// <summary>
        /// Gets or sets whether to use real-time simulation
        /// When false, time progresses faster than real time
        /// </summary>
        public bool RealTimeSimulation { get; set; } = true;
        
        /// <summary>
        /// Gets or sets the time acceleration factor when not in real-time mode
        /// </summary>
        public double TimeAccelerationFactor { get; set; } = 10.0;
    }
    
    /// <summary>
    /// Event arguments for the DataUpdated event
    /// </summary>
    public class MockDataUpdatedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the updated routes
        /// </summary>
        public IEnumerable<Route> Routes { get; }
        
        /// <summary>
        /// Gets the updated stops
        /// </summary>
        public IEnumerable<Stop> Stops { get; }
        
        /// <summary>
        /// Gets the updated vehicles
        /// </summary>
        public IEnumerable<Vehicle> Vehicles { get; }
        
        /// <summary>
        /// Gets the updated schedules
        /// </summary>
        public IEnumerable<Schedule> Schedules { get; }
        
        /// <summary>
        /// Gets the timestamp of the update
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// Gets the simulated time (may be different from actual time if time acceleration is enabled)
        /// </summary>
        public DateTime SimulatedTime { get; }
        
        /// <summary>
        /// Creates a new instance of the MockDataUpdatedEventArgs class
        /// </summary>
        /// <param name="routes">Updated routes</param>
        /// <param name="stops">Updated stops</param>
        /// <param name="vehicles">Updated vehicles</param>
        /// <param name="schedules">Updated schedules</param>
        /// <param name="timestamp">Real timestamp of the update</param>
        /// <param name="simulatedTime">Simulated time</param>
        public MockDataUpdatedEventArgs(
            IEnumerable<Route> routes,
            IEnumerable<Stop> stops,
            IEnumerable<Vehicle> vehicles,
            IEnumerable<Schedule> schedules,
            DateTime timestamp,
            DateTime simulatedTime)
        {
            Routes = routes;
            Stops = stops;
            Vehicles = vehicles;
            Schedules = schedules;
            Timestamp = timestamp;
            SimulatedTime = simulatedTime;
        }
    }
}
