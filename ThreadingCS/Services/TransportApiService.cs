using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ThreadingCS.Models;

namespace ThreadingCS.Services
{
    public class TransportApiService
    {
        private readonly HttpClient _client;
        private const string ApiKey = "d9b31e3854msh332940292b70607p17060fjsn98ba936d56cb";
        private const string ApiHost = "busmaps-gtfs-api.p.rapidapi.com";
        private readonly DatabaseService _databaseService;
        
        public TransportApiService(DatabaseService databaseService = null)
        {
            _client = new HttpClient();
            _databaseService = databaseService ?? new DatabaseService();
        }

        public async Task<TransportApiResponse> GetRoutesAsync(double originLat, double originLng, double destLat, double destLng, bool useCache = true)
        {
            try
            {
                Debug.WriteLine("[API] Attempting to fetch routes from RapidAPI...");
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://busmaps-gtfs-api.p.rapidapi.com/routes?origin={originLat}%2C{originLng}&destination={destLat}%2C{destLng}&transfers=1");
                request.Headers.Add("x-rapidapi-key", ApiKey);
                request.Headers.Add("x-rapidapi-host", ApiHost);
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("User-Agent", "ThreadingCS-MAUI-App");
                // Log request details
                Debug.WriteLine($"[API REQUEST] {request.Method} {request.RequestUri}");
                foreach (var header in request.Headers)
                {
                    Debug.WriteLine($"[API REQUEST HEADER] {header.Key}: {string.Join(", ", header.Value)}");
                }

                var response = await _client.SendAsync(request);
                Debug.WriteLine($"[API RESPONSE] Status: {(int)response.StatusCode} {response.StatusCode}");
                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[API RESPONSE BODY] {body}");
                response.EnsureSuccessStatusCode();

                using var jsonDoc = JsonDocument.Parse(body);
                var root = jsonDoc.RootElement;

                var apiRoutes = new List<TransportRoute>();
                if (root.TryGetProperty("routes", out var routesArray))
                {
                    foreach (var routeElement in routesArray.EnumerateArray())
                    {
                        var route = new TransportRoute
                        {
                            RouteId = routeElement.TryGetProperty("id", out var routeId) ? routeId.GetString() : $"Route-{Guid.NewGuid()}",
                            RouteName = routeElement.TryGetProperty("id", out var routeName) ? routeName.GetString() : "Unknown Route",
                            AgencyName = "Unknown Agency", // No direct field in new API, fallback
                            Color = "#FF0000", // No direct field, fallback
                            Duration = routeElement.TryGetProperty("duration", out var duration) ? duration.GetDouble() : 0,
                            Stops = new List<TransportStop>(),
                            Vehicles = new List<Vehicle>()
                        };
                        // Parse sections as stops
                        if (routeElement.TryGetProperty("sections", out var sectionsArray))
                        {
                            foreach (var sectionElement in sectionsArray.EnumerateArray())
                            {
                                // Departure stop
                                if (sectionElement.TryGetProperty("departure", out var departureObj) &&
                                    departureObj.TryGetProperty("place", out var depPlace) &&
                                    depPlace.TryGetProperty("location", out var depLoc))
                                {
                                    var stop = new TransportStop
                                    {
                                        StopId = sectionElement.TryGetProperty("id", out var sectionId) ? sectionId.GetString() + "-dep" : $"Stop-{Guid.NewGuid()}",
                                        StopName = depPlace.TryGetProperty("name", out var depName) ? depName.GetString() : "",
                                        Latitude = depLoc.TryGetProperty("lat", out var lat) ? lat.GetDouble() : 0.0,
                                        Longitude = depLoc.TryGetProperty("lng", out var lng) ? lng.GetDouble() : 0.0,
                                        EstimatedArrival = departureObj.TryGetProperty("time", out var depTime) && DateTime.TryParse(depTime.GetString(), out var dtDep) ? dtDep : DateTime.Now
                                    };
                                    route.Stops.Add(stop);
                                }
                                // Arrival stop
                                if (sectionElement.TryGetProperty("arrival", out var arrivalObj) &&
                                    arrivalObj.TryGetProperty("place", out var arrPlace) &&
                                    arrPlace.TryGetProperty("location", out var arrLoc))
                                {
                                    var stop = new TransportStop
                                    {
                                        StopId = sectionElement.TryGetProperty("id", out var sectionId) ? sectionId.GetString() + "-arr" : $"Stop-{Guid.NewGuid()}",
                                        StopName = arrPlace.TryGetProperty("name", out var arrName) ? arrName.GetString() : "",
                                        Latitude = arrLoc.TryGetProperty("lat", out var lat) ? lat.GetDouble() : 0.0,
                                        Longitude = arrLoc.TryGetProperty("lng", out var lng) ? lng.GetDouble() : 0.0,
                                        EstimatedArrival = arrivalObj.TryGetProperty("time", out var arrTime) && DateTime.TryParse(arrTime.GetString(), out var dtArr) ? dtArr : DateTime.Now
                                    };
                                    route.Stops.Add(stop);
                                }
                            }
                        }
                        // Simulate vehicles for now (API does not provide vehicles)
                        var random = new Random();
                        int vehicleCount = random.Next(1, 3);
                        for (int i = 0; i < vehicleCount; i++)
                        {
                            var stopIdx = i % Math.Max(1, route.Stops.Count);
                            var baseStop = route.Stops.Count > 0 ? route.Stops[stopIdx] : new TransportStop { Latitude = 0, Longitude = 0 };
                            var vehicle = new Vehicle
                            {
                                VehicleId = $"{route.RouteId}-V{i}",
                                RouteId = route.RouteId,
                                Latitude = baseStop.Latitude + (random.NextDouble() - 0.5) * 0.005,
                                Longitude = baseStop.Longitude + (random.NextDouble() - 0.5) * 0.005,
                                Bearing = random.Next(0, 360),
                                LastUpdated = DateTime.Now
                            };
                            route.Vehicles.Add(vehicle);
                        }
                        apiRoutes.Add(route);
                    }
                }
                if (apiRoutes.Count > 0)
                {
                    await _databaseService.SaveRoutesAsync(apiRoutes);
                }
                Debug.WriteLine($"[API] Successfully fetched and cached {apiRoutes.Count} routes from API");
                return new TransportApiResponse
                {
                    IsSuccess = true,
                    Routes = apiRoutes,
                    TotalCount = apiRoutes.Count,
                    ErrorMessage = null
                };
            }
            catch (Exception apiEx)
            {
                Debug.WriteLine($"[API] Error fetching from RapidAPI: {apiEx.Message}. Trying to load from cache...");
                try
                {
                    var cachedRoutes = await _databaseService.GetAllRoutesAsync();
                    if (cachedRoutes.Count > 0)
                    {
                        Debug.WriteLine($"[API] Returning {cachedRoutes.Count} cached routes after API failure");
                        return new TransportApiResponse
                        {
                            IsSuccess = true,
                            Routes = cachedRoutes,
                            TotalCount = cachedRoutes.Count,
                            ErrorMessage = "Loaded from cache due to API error."
                        };
                    }
                }
                catch (Exception cacheEx)
                {
                    Debug.WriteLine($"[API] Error loading from cache: {cacheEx.Message}. Using mock data.");
                }
                var sampleRoutes = GenerateSampleRoutes(25);
                await _databaseService.SaveRoutesAsync(sampleRoutes);
                return new TransportApiResponse
                {
                    IsSuccess = false,
                    Routes = sampleRoutes,
                    TotalCount = sampleRoutes.Count,
                    ErrorMessage = "API and cache failed. Using mock data."
                };
            }
        }
                
        // Get real-time vehicle updates for multiple routes in parallel
        public async Task<Dictionary<string, List<Vehicle>>> GetVehicleUpdatesAsync(List<string> routeIds)
        {
            var results = new Dictionary<string, List<Vehicle>>();
            
            // Create a list of tasks for parallel API calls
            var tasks = new List<Task<(string RouteId, List<Vehicle> Vehicles)>>();
            
            foreach (var routeId in routeIds)
            {
                // Create a task for each route ID
                tasks.Add(GetVehicleUpdatesForRouteAsync(routeId));
            }
            
            // Wait for all API calls to complete
            var updatedRoutes = await Task.WhenAll(tasks);
            
            // Process results
            foreach (var (RouteId, Vehicles) in updatedRoutes)
            {
                results[RouteId] = Vehicles;
            }
            
            return results;
        }
        
        // Get vehicle updates for a single route
        private async Task<(string RouteId, List<Vehicle> Vehicles)> GetVehicleUpdatesForRouteAsync(string routeId)
        {
            try
            {
                Debug.WriteLine($"[API] Fetching vehicle updates for route {routeId}...");
                
                // Construct API request URL for vehicle positions
                var request = new HttpRequestMessage(HttpMethod.Get, 
                    $"https://busmaps-gtfs-api.p.rapidapi.com/vehicles?route_id={routeId}");
                
                request.Headers.Add("x-rapidapi-key", ApiKey);
                request.Headers.Add("x-rapidapi-host", ApiHost);
                request.Headers.Add("Accept", "application/json");
                
                // Send the request
                var response = await _client.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    // Parse the response
                    using var jsonDoc = JsonDocument.Parse(body);
                    var root = jsonDoc.RootElement;
                    
                    var vehicles = new List<Vehicle>();
                    
                    if (root.TryGetProperty("vehicles", out var vehiclesArray))
                    {
                        foreach (var vehicleElement in vehiclesArray.EnumerateArray())
                        {
                            var vehicle = new Vehicle
                            {
                                VehicleId = vehicleElement.TryGetProperty("id", out var id) ? id.GetString() : $"V-{Guid.NewGuid()}",
                                RouteId = routeId,
                                Latitude = vehicleElement.TryGetProperty("lat", out var lat) ? lat.GetDouble() : 0,
                                Longitude = vehicleElement.TryGetProperty("lng", out var lng) ? lng.GetDouble() : 0,
                                Bearing = vehicleElement.TryGetProperty("bearing", out var bearing) ? bearing.GetDouble() : 0,
                                LastUpdated = DateTime.Now
                            };
                            
                            vehicles.Add(vehicle);
                        }
                    }
                    
                    return (routeId, vehicles);
                }
                else
                {
                    Debug.WriteLine($"[API] Error fetching vehicle updates: {response.StatusCode}");
                    // Return empty list on error
                    return (routeId, new List<Vehicle>());
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] Exception in GetVehicleUpdatesForRouteAsync: {ex.Message}");
                return (routeId, new List<Vehicle>());
            }
        }

        // Generate sample data for testing
        private List<TransportRoute> GenerateSampleRoutes(int count)
        {
            var routes = new List<TransportRoute>();
            var random = new Random();

            for (int i = 0; i < count; i++)
            {
                var route = new TransportRoute
                {
                    RouteId = $"Route{i}",
                    RouteName = $"Bus Line {random.Next(1, 100)}",
                    AgencyName = "City Transport",
                    Duration = random.Next(15, 90),
                    Distance = random.Next(1, 20) + random.NextDouble(),
                    Color = $"#{random.Next(0x1000000):X6}"
                };

                // Add stops
                int stopCount = random.Next(5, 15);
                for (int j = 0; j < stopCount; j++)
                {
                    route.Stops.Add(new TransportStop
                    {
                        StopId = $"Stop{i}-{j}",
                        StopName = $"Stop {j} on Route {i}",
                        Latitude = 51.5 + (random.NextDouble() * 0.1),
                        Longitude = -0.1 + (random.NextDouble() * 0.1),
                        EstimatedArrival = DateTime.Now.AddMinutes(random.Next(5, 60))
                    });
                }

                // Add vehicles
                int vehicleCount = random.Next(1, 4);
                for (int k = 0; k < vehicleCount; k++)
                {
                    route.Vehicles.Add(new Vehicle
                    {
                        VehicleId = $"Vehicle{i}-{k}",
                        RouteId = route.RouteId,
                        Latitude = 51.5 + (random.NextDouble() * 0.1),
                        Longitude = -0.1 + (random.NextDouble() * 0.1),
                        Bearing = random.Next(0, 360),
                        LastUpdated = DateTime.Now.AddSeconds(-random.Next(10, 300))
                    });
                }

                routes.Add(route);
            }

            return routes;
        }

        // Method to generate large dataset for PLINQ demonstration
        public async Task<List<TransportRoute>> GenerateLargeDataset(int count = 100000, bool saveToDatabase = true)
        {
            Debug.WriteLine($"[Dataset] Entered GenerateLargeDataset with count={count}, saveToDatabase={saveToDatabase}");
            
            // Skip database check for performance reasons
            // This avoids the initial database query that can be slow
            Debug.WriteLine("[Dataset] Skipping database check for performance");

            var routes = new List<TransportRoute>();
            var random = new Random();

            Debug.WriteLine($"[Dataset] Generating {count} new routes");
            for (int i = 0; i < count; i++)
            {
                var route = new TransportRoute
                {
                    RouteId = $"Route{i}",
                    RouteName = $"Bus Line {random.Next(1, 500)}",
                    AgencyName = $"Transport Agency {random.Next(1, 20)}",
                    Duration = random.Next(5, 180),
                    Distance = random.Next(1, 50) + random.NextDouble(),
                    Vehicles = new List<Vehicle>()
                };

                int vehicleCount = random.Next(1, 4);
                for (int k = 0; k < vehicleCount; k++)
                {
                    route.Vehicles.Add(new Vehicle
                    {
                        VehicleId = $"Vehicle{i}-{k}",
                        RouteId = route.RouteId,
                        Latitude = 51.5 + (random.NextDouble() * 0.1),
                        Longitude = -0.1 + (random.NextDouble() * 0.1),
                        Bearing = random.Next(0, 360),
                        LastUpdated = DateTime.Now.AddSeconds(-random.Next(10, 300))
                    });
                }

                routes.Add(route);
            }

            if (saveToDatabase)
            {
                Debug.WriteLine($"[Dataset] Saving {routes.Count} routes to DB via SaveLargeDatasetAsync");
                try
                {
                    // Use a timeout for database operations
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    
                    // Save only a subset of the data for better performance (1000 routes max)
                    var saveTask = _databaseService.SaveLargeDatasetAsync(routes, 1000);
                    bool success = await saveTask.WaitAsync(cts.Token);
                    
                    if (success)
                    {
                        Debug.WriteLine($"[Dataset] Successfully saved subset of large dataset to DB");
                    }
                    else
                    {
                        Debug.WriteLine($"[Dataset] Database save operation failed or timed out");
                    }
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine($"[Dataset] Database save operation timed out after 30 seconds");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Dataset] Error saving large dataset to database: {ex.Message}");
                }
        }

        Debug.WriteLine($"[Dataset] Returning generated dataset with {routes.Count} routes");
        return routes;
    }
    }
}
