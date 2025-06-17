using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TransportTracker.App.Models;

namespace TransportTracker.App.Services
{
    /// <summary>
    /// Interface for the transport API service
    /// </summary>
    public interface ITransportApiService
    {
        /// <summary>
        /// Set API authentication key
        /// </summary>
        void SetApiKey(string apiKey);
        
        /// <summary>
        /// Set the city for API requests
        /// </summary>
        void SetCity(string city);
        
        /// <summary>
        /// Get transport vehicles with caching and retry support
        /// </summary>
        Task<List<TransportVehicle>> GetVehiclesAsync(
            string transportType = null, 
            bool bypassCache = false,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get transport routes with caching and retry support
        /// </summary>
        Task<List<RouteInfo>> GetRoutesAsync(
            string transportType = null,
            bool bypassCache = false,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get transport stops with caching and retry support
        /// </summary>
        Task<List<TransportStop>> GetStopsAsync(
            string routeId = null,
            bool bypassCache = false,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Check API service status
        /// </summary>
        Task<bool> CheckApiStatusAsync(CancellationToken cancellationToken = default);
    }
}
