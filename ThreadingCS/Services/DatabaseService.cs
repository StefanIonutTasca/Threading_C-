using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SQLite;
using ThreadingCS.Models;
using Microsoft.Maui.Storage;

namespace ThreadingCS.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _database;
        private readonly string _databasePath;

        public DatabaseService()
        {
            // Store the database next to the .sln file (project root)
            var solutionDir = "c:/Users/radu/Downloads/ThreadingCS/ThreadingCS";
            _databasePath = Path.Combine(solutionDir, "transport_data.db3");
        }

        private async Task InitializeAsync()
        {
            if (_database != null)
                return;

            _database = new SQLiteAsyncConnection(_databasePath);
            
            // Create tables
            await _database.CreateTableAsync<TransportRouteEntity>();
            await _database.CreateTableAsync<TransportStopEntity>();
            await _database.CreateTableAsync<VehicleEntity>();
        }

        // Save routes with their related stops and vehicles
        public async Task SaveRoutesAsync(List<TransportRoute> routes)
        {
            await InitializeAsync();

            const int batchSize = 500;
            int total = routes.Count;
            int numBatches = (int)Math.Ceiling(total / (double)batchSize);
            Debug.WriteLine($"[DB] Starting batch save of {total} routes (with stops/vehicles) using {numBatches} batches...");

            for (int i = 0; i < total; i += batchSize)
            {
                var batch = routes.Skip(i).Take(batchSize).ToList();
                await _database.RunInTransactionAsync(conn =>
                {
                    var routeEntities = new List<TransportRouteEntity>();
                    var stopEntities = new List<TransportStopEntity>();
                    var vehicleEntities = new List<VehicleEntity>();

                    foreach (var route in batch)
                    {
                        routeEntities.Add(new TransportRouteEntity
                        {
                            RouteId = route.RouteId,
                            RouteName = route.RouteName,
                            AgencyName = route.AgencyName,
                            Color = route.Color,
                            Duration = route.Duration,
                            Distance = route.Distance,
                            SavedAt = DateTime.Now
                        });

                        if (route.Stops != null)
                        {
                            foreach (var stop in route.Stops)
                            {
                                stopEntities.Add(new TransportStopEntity
                                {
                                    StopId = stop.StopId,
                                    StopName = stop.StopName,
                                    Latitude = stop.Latitude,
                                    Longitude = stop.Longitude,
                                    EstimatedArrival = stop.EstimatedArrival,
                                    RouteId = route.RouteId
                                });
                            }
                        }

                        if (route.Vehicles != null)
                        {
                            foreach (var vehicle in route.Vehicles)
                            {
                                vehicleEntities.Add(new VehicleEntity
                                {
                                    VehicleId = vehicle.VehicleId,
                                    RouteId = vehicle.RouteId,
                                    Latitude = vehicle.Latitude,
                                    Longitude = vehicle.Longitude,
                                    Bearing = vehicle.Bearing,
                                    LastUpdated = vehicle.LastUpdated
                                });
                            }
                        }
                    }

                    conn.InsertAll(routeEntities, "OR REPLACE");
                    conn.InsertAll(stopEntities, "OR REPLACE");
                    conn.InsertAll(vehicleEntities, "OR REPLACE");
                });
            }
            Debug.WriteLine($"[DB] Finished batch save of {total} routes (with stops/vehicles)");
        }

        // Get all routes with their stops and vehicles
        public async Task<List<TransportRoute>> GetAllRoutesAsync()
        {
            Debug.WriteLine("[DB] Entered GetAllRoutesAsync");
            await InitializeAsync();
            
            var routeEntities = await _database.Table<TransportRouteEntity>().ToListAsync();
            var result = new List<TransportRoute>();

            foreach (var routeEntity in routeEntities)
            {
                // Get related stops
                var stops = await _database.Table<TransportStopEntity>()
                    .Where(s => s.RouteId == routeEntity.RouteId)
                    .ToListAsync();

                // Get related vehicles
                var vehicles = await _database.Table<VehicleEntity>()
                    .Where(v => v.RouteId == routeEntity.RouteId)
                    .ToListAsync();

                // Create the TransportRoute object
                var route = new TransportRoute
                {
                    RouteId = routeEntity.RouteId,
                    RouteName = routeEntity.RouteName,
                    AgencyName = routeEntity.AgencyName,
                    Color = routeEntity.Color,
                    Duration = routeEntity.Duration,
                    Distance = routeEntity.Distance,
                    Stops = stops.ConvertAll(s => new TransportStop
                    {
                        StopId = s.StopId,
                        StopName = s.StopName,
                        Latitude = s.Latitude,
                        Longitude = s.Longitude,
                        EstimatedArrival = s.EstimatedArrival
                    }),
                    Vehicles = vehicles.ConvertAll(v => new Vehicle
                    {
                        VehicleId = v.VehicleId,
                        RouteId = v.RouteId,
                        Latitude = v.Latitude,
                        Longitude = v.Longitude,
                        Bearing = v.Bearing,
                        LastUpdated = v.LastUpdated
                    })
                };
                
                result.Add(route);
            }

            Debug.WriteLine($"[DB] Returning {result.Count} routes from GetAllRoutesAsync");
            return result;
        }

        // Check if cache is recent (within the specified minutes)
        public async Task<bool> HasRecentCacheAsync(int minutes = 30)
        {
            Debug.WriteLine($"[DB] Entered HasRecentCacheAsync with minutes={minutes}");
            await InitializeAsync();
            
            var mostRecentRoute = await _database.Table<TransportRouteEntity>()
                .OrderByDescending(r => r.SavedAt)
                .FirstOrDefaultAsync();
                
            if (mostRecentRoute == null)
                return false;
                
            return (DateTime.Now - mostRecentRoute.SavedAt).TotalMinutes < minutes;
        }

        // Save large dataset
        public async Task SaveLargeDatasetAsync(List<TransportRoute> routes)
        {
            Debug.WriteLine($"[DB] Entered SaveLargeDatasetAsync with {routes?.Count ?? 0} routes");
            await InitializeAsync();
            
            // Clear any existing data in the database (for large datasets, we don't want to mix)
            await _database.ExecuteAsync("DELETE FROM TransportRouteEntity");
            await _database.ExecuteAsync("DELETE FROM TransportStopEntity");
            await _database.ExecuteAsync("DELETE FROM VehicleEntity");
            
            // Use batch processing for better performance
            var batchSize = 1000;
            for (int i = 0; i < routes.Count; i += batchSize)
            {
                var batch = routes.Skip(i).Take(batchSize).ToList();
                await SaveRoutesAsync(batch);
            }
        }

        // Clear all data
        public async Task ClearAllDataAsync()
        {
            await InitializeAsync();
            await _database.ExecuteAsync("DELETE FROM TransportRouteEntity");
            await _database.ExecuteAsync("DELETE FROM TransportStopEntity");
            await _database.ExecuteAsync("DELETE FROM VehicleEntity");
        }
    }
}
