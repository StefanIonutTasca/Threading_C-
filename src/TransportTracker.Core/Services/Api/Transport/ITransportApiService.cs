using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TransportTracker.Core.Models;

namespace TransportTracker.Core.Services.Api.Transport
{
    /// <summary>
    /// Interface for the transport API service
    /// </summary>
    public interface ITransportApiService
    {
        /// <summary>
        /// Get all available routes
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of routes</returns>
        Task<List<Route>> GetRoutesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a specific route by ID
        /// </summary>
        /// <param name="routeId">ID of the route</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The route if found, null otherwise</returns>
        Task<Route> GetRouteAsync(string routeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all stops for a specific route
        /// </summary>
        /// <param name="routeId">ID of the route</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of stops on the route</returns>
        Task<List<Stop>> GetRouteStopsAsync(string routeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all vehicles for a specific route
        /// </summary>
        /// <param name="routeId">ID of the route</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of vehicles on the route</returns>
        Task<List<Vehicle>> GetRouteVehiclesAsync(string routeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all active vehicles with their locations
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of vehicles with their current locations</returns>
        Task<List<Vehicle>> GetVehicleLocationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get details for a specific vehicle
        /// </summary>
        /// <param name="vehicleId">ID of the vehicle</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The vehicle if found, null otherwise</returns>
        Task<Vehicle> GetVehicleAsync(string vehicleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all stops
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of stops</returns>
        Task<List<Stop>> GetStopsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a specific stop by ID
        /// </summary>
        /// <param name="stopId">ID of the stop</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The stop if found, null otherwise</returns>
        Task<Stop> GetStopAsync(string stopId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get arrival predictions for a specific stop
        /// </summary>
        /// <param name="stopId">ID of the stop</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of predictions for the stop</returns>
        Task<List<ArrivalPrediction>> GetStopPredictionsAsync(string stopId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all active service alerts
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of service alerts</returns>
        Task<List<ServiceAlert>> GetServiceAlertsAsync(CancellationToken cancellationToken = default);
    }
}
