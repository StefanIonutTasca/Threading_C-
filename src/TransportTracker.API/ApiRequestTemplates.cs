// Sample API request templates for the Transport Tracker application
// Created by Dev B on June 9, 2025

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TransportTracker.API.Templates
{
    /// <summary>
    /// Sample request templates for transport APIs
    /// </summary>
    public static class ApiRequestTemplates
    {
        // OpenTransport API example request
        public static async Task<string> GetVehiclesNearLocationTemplate(HttpClient client, 
            double latitude, double longitude, int radius, string[] types)
        {
            string endpoint = $"https://api.opentransport.com/v1/vehicles?latitude={latitude}&longitude={longitude}&radius={radius}&types={string.Join(",", types)}";
            
            // Note: In actual implementation, use HttpClientFactory and proper authentication
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("X-API-Key", "YOUR_API_KEY");
            
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }
        
        // CityTransit API example for getting arrival predictions
        public static async Task<string> GetArrivalPredictionsTemplate(HttpClient client,
            string stopId, string[] routeIds, int limit = 10)
        {
            string routeIdsParam = string.Join(",", routeIds);
            string endpoint = $"https://citytransit.api.com/v2/transit/stops/predictions?stop_id={stopId}&route_ids={routeIdsParam}&limit={limit}";
            
            // OAuth authentication would be implemented in production
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "YOUR_OAUTH_TOKEN");
            
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }
        
        // Example of a paginated request to handle large datasets
        public static async Task<List<VehicleDto>> GetAllVehiclesWithPaginationTemplate(HttpClient client, 
            string agencyId, CancellationToken cancellationToken = default)
        {
            List<VehicleDto> allVehicles = new List<VehicleDto>();
            string nextPageToken = null;
            bool hasMorePages = true;
            
            while (hasMorePages && !cancellationToken.IsCancellationRequested)
            {
                string endpoint = $"https://api.opentransport.com/v1/agencies/{agencyId}/vehicles?limit=1000";
                if (!string.IsNullOrEmpty(nextPageToken))
                {
                    endpoint += $"&page_token={nextPageToken}";
                }
                
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                request.Headers.Add("X-API-Key", "YOUR_API_KEY");
                
                var response = await client.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadFromJsonAsync<PagedVehicleResponse>(cancellationToken: cancellationToken);
                allVehicles.AddRange(content.Vehicles);
                
                nextPageToken = content.NextPageToken;
                hasMorePages = !string.IsNullOrEmpty(nextPageToken);
                
                // Implement rate limiting to avoid hitting API limits
                await Task.Delay(200, cancellationToken);
            }
            
            return allVehicles;
        }
        
        // Example of parallel API requests using PLINQ
        public static async Task<Dictionary<string, RouteDto>> GetMultipleRoutesParallelTemplate(HttpClient client,
            IEnumerable<string> routeIds)
        {
            var result = new Dictionary<string, RouteDto>();
            
            // Warning: Use this pattern carefully to avoid overwhelming the API
            // Consider implementing a semaphore to limit parallel requests
            var tasks = routeIds.Select(async routeId =>
            {
                string endpoint = $"https://api.opentransport.com/v1/routes/{routeId}";
                
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                request.Headers.Add("X-API-Key", "YOUR_API_KEY");
                
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var route = await response.Content.ReadFromJsonAsync<RouteDto>();
                return (routeId, route);
            });
            
            var results = await Task.WhenAll(tasks);
            
            foreach (var (routeId, route) in results)
            {
                result[routeId] = route;
            }
            
            return result;
        }
    }
    
    // Sample DTOs for API responses
    public class VehicleDto
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string RouteId { get; set; }
        public Position Position { get; set; }
        public int Heading { get; set; }
        public int Speed { get; set; }
        public string Status { get; set; }
        public string Occupancy { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
    
    public class Position
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
    
    public class RouteDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
        public List<string> StopIds { get; set; }
    }
    
    public class PagedVehicleResponse
    {
        public List<VehicleDto> Vehicles { get; set; }
        public string NextPageToken { get; set; }
        public int TotalCount { get; set; }
    }
}
