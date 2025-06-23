using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using ThreadingCS.Models;

namespace ThreadingCS.Services
{
    public class TransportApiService
    {
        private readonly HttpClient _client;
        private const string ApiKey = "d9b31e3854msh332940292b70607p17060fjsn98ba936d56cb";
        private const string ApiHost = "busmaps-gtfs-api.p.rapidapi.com";
        
        public TransportApiService()
        {
            _client = new HttpClient();
        }

        public async Task<TransportApiResponse> GetRoutesAsync(double originLat, double originLng, double destLat, double destLng)
        {
            try
            {
                Debug.WriteLine("Attempting to fetch routes from API...");
                
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"https://busmaps-gtfs-api.p.rapidapi.com/routes?origin={originLat}%2C{originLng}&destination={destLat}%2C{destLng}&departureTime={DateTime.Now:yyyy-MM-ddTHH%3Amm%3Ass}&arrivalTime={DateTime.Now.AddHours(1):yyyy-MM-ddTHH%3Amm%3Ass}&transfers=1"),
                    Headers =
                    {
                        { "x-rapidapi-key", ApiKey },
                        { "x-rapidapi-host", ApiHost },
                    },
                };

                using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    
                    Debug.WriteLine($"API Status Code: {response.StatusCode}");
                    Debug.WriteLine($"API Response: {body}");

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorMsg = $"API Error: {response.StatusCode} - {response.ReasonPhrase}";
                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            errorMsg += "\nAuthentication failed. Please check your API key and subscription.";
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                        {
                            errorMsg += "\nThe API server encountered an error. Falling back to sample data.";
                            Debug.WriteLine(errorMsg);
                            return new TransportApiResponse
                            {
                                IsSuccess = false,
                                ErrorMessage = errorMsg,
                                Routes = GenerateSampleRoutes(10),
                                TotalCount = 10
                            };
                        }
                        Debug.WriteLine(errorMsg);
                        throw new HttpRequestException(errorMsg, null, response.StatusCode);
                    }

                    try
                    {
                        // Parse the API response using System.Text.Json
                        using var jsonDoc = JsonDocument.Parse(body);
                        var root = jsonDoc.RootElement;
                        
                        // Extract routes from the API response
                        var apiRoutes = new List<TransportRoute>();
                        
                        if (root.TryGetProperty("routes", out var routesArray))
                        {
                            foreach (var routeElement in routesArray.EnumerateArray())
                            {
                                var route = new TransportRoute
                                {
                                    RouteId = routeElement.TryGetProperty("route_id", out var routeId) ? routeId.GetString() : $"Route-{Guid.NewGuid()}",
                                    RouteName = routeElement.TryGetProperty("route_short_name", out var routeName) ? routeName.GetString() : "Unknown Route",
                                    AgencyName = routeElement.TryGetProperty("agency_name", out var agencyName) ? agencyName.GetString() : "Transit Agency",
                                    Color = routeElement.TryGetProperty("route_color", out var routeColor) ? $"#{routeColor.GetString()}" : "#FF0000"
                                };
                                
                                // Add stops if available
                                if (routeElement.TryGetProperty("stops", out var stopsArray))
                                {
                                    foreach (var stopElement in stopsArray.EnumerateArray())
                                    {
                                        var stop = new TransportStop
                                        {
                                            StopId = stopElement.TryGetProperty("stop_id", out var stopId) ? stopId.GetString() : $"Stop-{Guid.NewGuid()}",
                                            StopName = stopElement.TryGetProperty("stop_name", out var stopName) ? stopName.GetString() : "Unknown Stop",
                                        };
                                        
                                        if (stopElement.TryGetProperty("stop_lat", out var stopLat) && 
                                            stopElement.TryGetProperty("stop_lon", out var stopLon))
                                        {
                                            stop.Latitude = stopLat.GetDouble();
                                            stop.Longitude = stopLon.GetDouble();
                                        }
                                        
                                        route.Stops.Add(stop);
                                    }
                                }
                                
                                // Add vehicles (since the API might not provide real-time vehicle data, we'll simulate this)
                                var random = new Random();
                                int vehicleCount = random.Next(1, 5);
                                
                                for (int i = 0; i < vehicleCount; i++)
                                {
                                    // Create vehicle positions along the route's stops
                                    var stopIdx = i % Math.Max(1, route.Stops.Count);
                                    var baseStop = route.Stops.Count > 0 ? route.Stops[stopIdx] : 
                                        new TransportStop { Latitude = originLat, Longitude = originLng };
                                    
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
                            }
                            
                            return new TransportApiResponse
                            {
                                IsSuccess = true,
                                Routes = apiRoutes,
                                TotalCount = apiRoutes.Count
                            };
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Debug.WriteLine($"JSON parsing error: {jsonEx.Message}");
                        Debug.WriteLine($"JSON parsing error details: {jsonEx.StackTrace}");
                        throw new HttpRequestException("Failed to parse API response", jsonEx);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing API response: {ex.Message}");
                        Debug.WriteLine($"Error details: {ex.StackTrace}");
                        throw new HttpRequestException("Failed to process API response", ex);
                    }
                    
                    // This should never be reached due to the throws above
                    throw new InvalidOperationException("Unexpected error in API response processing");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"API request error: {ex.Message}");
                return new TransportApiResponse
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
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
        public List<TransportRoute> GenerateLargeDataset(int count = 100000)
        {
            var routes = new List<TransportRoute>();
            var random = new Random();

            for (int i = 0; i < count; i++)
            {
                var route = new TransportRoute
                {
                    RouteId = $"Route{i}",
                    RouteName = $"Bus Line {random.Next(1, 500)}",
                    AgencyName = $"Transport Agency {random.Next(1, 20)}",
                    Duration = random.Next(5, 180),
                    Distance = random.Next(1, 50) + random.NextDouble()
                };

                routes.Add(route);
            }

            return routes;
        }
    }
}
