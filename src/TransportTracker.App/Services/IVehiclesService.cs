using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TransportTracker.App.Views.Maps;

namespace TransportTracker.App.Services
{
    /// <summary>
    /// Service for accessing and manipulating transport vehicle data
    /// </summary>
    public interface IVehiclesService
    {
        /// <summary>
        /// Gets all vehicles
        /// </summary>
        /// <returns>A list of all transport vehicles</returns>
        Task<IEnumerable<TransportVehicle>> GetAllVehiclesAsync();
        
        /// <summary>
        /// Gets a vehicle by its unique identifier
        /// </summary>
        /// <param name="id">The ID of the vehicle to retrieve</param>
        /// <returns>The vehicle with the specified ID, or null if not found</returns>
        Task<TransportVehicle> GetVehicleByIdAsync(string id);
        
        /// <summary>
        /// Gets vehicles based on search criteria and pagination parameters
        /// </summary>
        /// <param name="searchText">Optional text to search for in vehicle properties</param>
        /// <param name="typeFilters">Optional dictionary of vehicle type filters</param>
        /// <param name="statusFilters">Optional dictionary of status filters</param>
        /// <param name="page">The page number to retrieve</param>
        /// <param name="pageSize">The number of items per page</param>
        /// <returns>A list of vehicles matching the criteria</returns>
        Task<IEnumerable<TransportVehicle>> GetVehiclesAsync(
            string searchText = null,
            Dictionary<string, bool> typeFilters = null,
            Dictionary<string, bool> statusFilters = null,
            int page = 1,
            int pageSize = 20);
    }
}
