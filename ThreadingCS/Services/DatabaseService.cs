using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
            // Store the database in the app's data directory
            string appDataPath = FileSystem.AppDataDirectory;
            _databasePath = Path.Combine(appDataPath, "transport_data.db3");
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

        // Get all routes with their stops and vehicles - optimized version with timeout
        public async Task<List<TransportRoute>> GetAllRoutesAsync(int limit = 100)
        {
            Debug.WriteLine("[DB] Entered GetAllRoutesAsync with optimized query");
            await InitializeAsync();
            
            try
            {
                // Use a CancellationTokenSource to implement a timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // 10-second timeout
                
                // Get a limited number of routes for better performance
                var routeEntities = await _database.Table<TransportRouteEntity>()
                    .Take(limit)
                    .ToListAsync()
                    .WaitAsync(cts.Token);
                
                Debug.WriteLine($"[DB] Retrieved {routeEntities.Count} route entities");
                
                // Get all stops and vehicles in bulk instead of per-route
                var allStops = await _database.Table<TransportStopEntity>().ToListAsync().WaitAsync(cts.Token);
                var allVehicles = await _database.Table<VehicleEntity>().ToListAsync().WaitAsync(cts.Token);
                
                Debug.WriteLine($"[DB] Retrieved {allStops.Count} stops and {allVehicles.Count} vehicles");
                
                // Group stops and vehicles by route ID for faster lookups
                var stopsByRouteId = allStops.GroupBy(s => s.RouteId).ToDictionary(g => g.Key, g => g.ToList());
                var vehiclesByRouteId = allVehicles.GroupBy(v => v.RouteId).ToDictionary(g => g.Key, g => g.ToList());
                
                var result = new List<TransportRoute>();
                
                // Create route objects with their related stops and vehicles
                foreach (var routeEntity in routeEntities)
                {
                    var routeStops = stopsByRouteId.ContainsKey(routeEntity.RouteId) 
                        ? stopsByRouteId[routeEntity.RouteId] 
                        : new List<TransportStopEntity>();
                        
                    var routeVehicles = vehiclesByRouteId.ContainsKey(routeEntity.RouteId) 
                        ? vehiclesByRouteId[routeEntity.RouteId] 
                        : new List<VehicleEntity>();
                    
                    // Create the TransportRoute object
                    var route = new TransportRoute
                    {
                        RouteId = routeEntity.RouteId,
                        RouteName = routeEntity.RouteName,
                        AgencyName = routeEntity.AgencyName,
                        Color = routeEntity.Color,
                        Duration = routeEntity.Duration,
                        Distance = routeEntity.Distance,
                        Stops = routeStops.ConvertAll(s => new TransportStop
                        {
                            StopId = s.StopId,
                            StopName = s.StopName,
                            Latitude = s.Latitude,
                            Longitude = s.Longitude,
                            EstimatedArrival = s.EstimatedArrival
                        }),
                        Vehicles = routeVehicles.ConvertAll(v => new Vehicle
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
                
                Debug.WriteLine($"[DB] Returning {result.Count} routes from optimized GetAllRoutesAsync");
                return result;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[DB] GetAllRoutesAsync timed out after 10 seconds");
                return new List<TransportRoute>(); // Return empty list on timeout
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB] Error in GetAllRoutesAsync: {ex.Message}");
                return new List<TransportRoute>(); // Return empty list on error
            }
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

        // Save large dataset with optimized batch processing and timeout
        public async Task<bool> SaveLargeDatasetAsync(List<TransportRoute> routes, int maxRoutesToSave = 1000)
        {
            Debug.WriteLine($"[DB] Entered SaveLargeDatasetAsync with {routes?.Count ?? 0} routes");
            await InitializeAsync();
            
            try
            {
                // Use a CancellationTokenSource to implement a timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30-second timeout
                
                // Limit the number of routes to save for better performance
                var routesToSave = routes.Take(maxRoutesToSave).ToList();
                Debug.WriteLine($"[DB] Limiting save to {routesToSave.Count} routes for performance");
                
                // Clear any existing data in the database (for large datasets, we don't want to mix)
                await _database.ExecuteAsync("DELETE FROM TransportRouteEntity").WaitAsync(cts.Token);
                await _database.ExecuteAsync("DELETE FROM TransportStopEntity").WaitAsync(cts.Token);
                await _database.ExecuteAsync("DELETE FROM VehicleEntity").WaitAsync(cts.Token);
                
                // Use smaller batch size for better performance
                var batchSize = 100; // Smaller batches are faster for SQLite
                int processedBatches = 0;
                
                for (int i = 0; i < routesToSave.Count && !cts.Token.IsCancellationRequested; i += batchSize)
                {
                    var batch = routesToSave.Skip(i).Take(batchSize).ToList();
                    await SaveRoutesAsync(batch);
                    processedBatches++;
                    Debug.WriteLine($"[DB] Saved batch {processedBatches} of {Math.Ceiling(routesToSave.Count / (double)batchSize)}");
                }
                
                Debug.WriteLine($"[DB] Successfully saved {routesToSave.Count} routes to database");
                return true;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[DB] SaveLargeDatasetAsync timed out after 30 seconds");
                return false; // Return false on timeout
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB] Error in SaveLargeDatasetAsync: {ex.Message}");
                return false; // Return false on error
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
