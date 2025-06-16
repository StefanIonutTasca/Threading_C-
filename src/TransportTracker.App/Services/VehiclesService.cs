using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TransportTracker.App.Views.Maps;

namespace TransportTracker.App.Services
{
    /// <summary>
    /// Service for accessing and manipulating transport vehicle data
    /// </summary>
    public class VehiclesService : IVehiclesService
    {
        private readonly Dictionary<string, TransportVehicle> _vehicleCache = new Dictionary<string, TransportVehicle>();
        private readonly Random _random = new Random();
        
        /// <summary>
        /// Initializes a new instance of the VehiclesService class
        /// </summary>
        public VehiclesService()
        {
            // Initialize with some mock data
            var mockVehicles = GenerateMockVehicles(100);
            foreach (var vehicle in mockVehicles)
            {
                _vehicleCache[vehicle.Id] = vehicle;
            }
        }

        /// <summary>
        /// Gets all vehicles
        /// </summary>
        /// <returns>A list of all transport vehicles</returns>
        public Task<IEnumerable<TransportVehicle>> GetAllVehiclesAsync()
        {
            return Task.FromResult<IEnumerable<TransportVehicle>>(_vehicleCache.Values.ToList());
        }
        
        /// <summary>
        /// Gets a vehicle by its unique identifier
        /// </summary>
        /// <param name="id">The ID of the vehicle to retrieve</param>
        /// <returns>The vehicle with the specified ID, or null if not found</returns>
        public Task<TransportVehicle> GetVehicleByIdAsync(string id)
        {
            if (_vehicleCache.TryGetValue(id, out var vehicle))
            {
                // Simulate changing location as if the vehicle is moving
                UpdateVehiclePosition(vehicle);
                return Task.FromResult(vehicle);
            }
            
            return Task.FromResult<TransportVehicle>(null);
        }
        
        /// <summary>
        /// Gets vehicles based on search criteria and pagination parameters
        /// </summary>
        /// <param name="searchText">Optional text to search for in vehicle properties</param>
        /// <param name="typeFilters">Optional dictionary of vehicle type filters</param>
        /// <param name="statusFilters">Optional dictionary of status filters</param>
        /// <param name="page">The page number to retrieve</param>
        /// <param name="pageSize">The number of items per page</param>
        /// <returns>A list of vehicles matching the criteria</returns>
        public Task<IEnumerable<TransportVehicle>> GetVehiclesAsync(
            string searchText = null,
            Dictionary<string, bool> typeFilters = null,
            Dictionary<string, bool> statusFilters = null,
            int page = 1,
            int pageSize = 20)
        {
            // Apply filters
            var query = _vehicleCache.Values.AsEnumerable();
            
            // Filter by search text
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var searchLower = searchText.ToLowerInvariant();
                query = query.Where(v =>
                    v.Number.ToLowerInvariant().Contains(searchLower) ||
                    v.Route.ToLowerInvariant().Contains(searchLower) ||
                    v.Type.ToLowerInvariant().Contains(searchLower) ||
                    v.Status.ToLowerInvariant().Contains(searchLower));
            }
            
            // Filter by vehicle type
            if (typeFilters != null && typeFilters.Any(kv => kv.Value))
            {
                query = query.Where(v => typeFilters.TryGetValue(v.Type, out bool isActive) && isActive);
            }
            
            // Filter by status
            if (statusFilters != null && statusFilters.Any(kv => kv.Value))
            {
                query = query.Where(v => statusFilters.TryGetValue(v.Status, out bool isActive) && isActive);
            }
            
            // Apply pagination
            var pagedResult = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
                
            return Task.FromResult<IEnumerable<TransportVehicle>>(pagedResult);
        }
        
        /// <summary>
        /// Generates mock vehicle data
        /// </summary>
        private List<TransportVehicle> GenerateMockVehicles(int count)
        {
            var vehicles = new List<TransportVehicle>();
            var vehicleTypes = new[] { "Bus", "Train", "Tram", "Subway", "Ferry" };
            var statusOptions = new[] { "On Time", "Delayed", "Cancelled", "Out of Service" };
            var routePrefixes = new[] { "A", "B", "C", "X", "Y", "Z" };
            
            for (int i = 0; i < count; i++)
            {
                string id = Guid.NewGuid().ToString();
                string type = vehicleTypes[_random.Next(vehicleTypes.Length)];
                string number = $"{_random.Next(1, 999):D3}";
                string route = $"{routePrefixes[_random.Next(routePrefixes.Length)]}{_random.Next(1, 100)}";
                string status = statusOptions[_random.Next(statusOptions.Length)];
                
                var vehicle = new TransportVehicle
                {
                    Id = id,
                    Type = type,
                    Number = number,
                    Route = route,
                    Status = status,
                    Latitude = 51.5 + (_random.NextDouble() * 0.1 - 0.05),
                    Longitude = -0.12 + (_random.NextDouble() * 0.1 - 0.05),
                    Speed = _random.Next(0, 80),
                    Occupancy = _random.Next(0, 100),
                    Capacity = 100,
                    LastUpdated = DateTime.Now.AddMinutes(-_random.Next(0, 60)),
                    NextStop = $"Stop {_random.Next(1, 20)}",
                    NextArrivalInfo = $"{_random.Next(1, 15)} min",
                    StartLocation = $"Start Location {_random.Next(1, 10)}",
                    EndLocation = $"End Location {_random.Next(1, 10)}",
                    ScheduledDeparture = DateTime.Now.AddHours(-1).AddMinutes(_random.Next(-15, 15)),
                    ActualDeparture = DateTime.Now.AddHours(-1).AddMinutes(_random.Next(-20, 20)),
                    ScheduledArrival = DateTime.Now.AddMinutes(_random.Next(15, 60)),
                    ExpectedArrival = DateTime.Now.AddMinutes(_random.Next(15, 75))
                };
                
                vehicles.Add(vehicle);
            }
            
            return vehicles;
        }
        
        /// <summary>
        /// Updates the position of a vehicle to simulate movement
        /// </summary>
        private void UpdateVehiclePosition(TransportVehicle vehicle)
        {
            // Small random movement
            double latDelta = (_random.NextDouble() * 0.002) - 0.001;
            double lonDelta = (_random.NextDouble() * 0.002) - 0.001;
            
            vehicle.Latitude += latDelta;
            vehicle.Longitude += lonDelta;
            
            // Update other dynamic properties
            vehicle.LastUpdated = DateTime.Now;
            vehicle.Speed = Math.Max(0, vehicle.Speed + _random.Next(-5, 6));
            
            // Occasionally update occupancy
            if (_random.Next(100) < 30)
            {
                int occupancyDelta = _random.Next(-5, 6);
                vehicle.Occupancy = Math.Clamp(vehicle.Occupancy + occupancyDelta, 0, vehicle.Capacity);
            }
            
            // Occasionally update status
            if (_random.Next(100) < 10)
            {
                var statusOptions = new[] { "On Time", "Delayed", "Cancelled", "Out of Service" };
                vehicle.Status = statusOptions[_random.Next(statusOptions.Length)];
            }
            
            // Update arrival info
            vehicle.NextArrivalInfo = $"{_random.Next(1, 15)} min";
        }
    }
}
