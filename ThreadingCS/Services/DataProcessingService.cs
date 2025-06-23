using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThreadingCS.Models;

namespace ThreadingCS.Services
{
    public class DataProcessingService
    {
        // Use PLINQ to filter routes by duration
        public List<TransportRoute> FilterRoutesByDuration(List<TransportRoute> routes, double maxDuration)
        {
            return routes.AsParallel()
                .Where(r => r.Duration <= maxDuration)
                .OrderBy(r => r.Duration)
                .ToList();
        }

        // Use PLINQ to filter routes by distance
        public List<TransportRoute> FilterRoutesByDistance(List<TransportRoute> routes, double maxDistance)
        {
            return routes.AsParallel()
                .Where(r => r.Distance <= maxDistance)
                .OrderBy(r => r.Distance)
                .ToList();
        }

        // Use PLINQ to search routes by name
        public List<TransportRoute> SearchRoutesByName(List<TransportRoute> routes, string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return routes;

            return routes.AsParallel()
                .Where(r => r.RouteName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Use PLINQ to find routes by agency
        public List<TransportRoute> FindRoutesByAgency(List<TransportRoute> routes, string agencyName)
        {
            return routes.AsParallel()
                .Where(r => r.AgencyName.Contains(agencyName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Use PLINQ to group routes by agency
        public Dictionary<string, int> GroupRoutesByAgency(List<TransportRoute> routes)
        {
            return routes.AsParallel()
                .GroupBy(r => r.AgencyName)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        // Use PLINQ with multiple operations to demonstrate complex processing
        public List<TransportRoute> GetOptimalRoutes(List<TransportRoute> routes, double maxDuration, double maxDistance)
        {
            return routes.AsParallel()
                .Where(r => r.Duration <= maxDuration && r.Distance <= maxDistance)
                .OrderBy(r => r.Duration)
                .ThenBy(r => r.Distance)
                .Take(10)
                .ToList();
        }

        // Process routes in batches using parallel processing
        public async Task<List<List<TransportRoute>>> ProcessRoutesInBatchesAsync(List<TransportRoute> routes, int batchSize = 10000)
        {
            var batches = new List<List<TransportRoute>>();
            var batchCount = (int)Math.Ceiling(routes.Count / (double)batchSize);
            
            var tasks = new List<Task<List<TransportRoute>>>();
            
            for (int i = 0; i < batchCount; i++)
            {
                var startIndex = i * batchSize;
                var count = Math.Min(batchSize, routes.Count - startIndex);
                var batch = routes.GetRange(startIndex, count);
                
                tasks.Add(Task.Run(() => ProcessBatch(batch)));
            }
            
            var results = await Task.WhenAll(tasks);
            batches.AddRange(results);
            
            return batches;
        }
        
        private List<TransportRoute> ProcessBatch(List<TransportRoute> batch)
        {
            // Simulate some processing work
            return batch.AsParallel()
                .Select(r => 
                {
                    // Do some processing for each route
                    r.RouteName = $"Processed: {r.RouteName}";
                    return r;
                })
                .ToList();
        }
    }
}
