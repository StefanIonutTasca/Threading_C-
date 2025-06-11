using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TransportTracker.Core.Services.Api
{
    /// <summary>
    /// Interface for the transport API client
    /// </summary>
    public interface IApiClient
    {
        /// <summary>
        /// Send a GET request to the specified endpoint
        /// </summary>
        /// <typeparam name="TResponse">The type to deserialize the response to</typeparam>
        /// <param name="endpoint">API endpoint path</param>
        /// <param name="queryParams">Optional query parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized response</returns>
        Task<TResponse> GetAsync<TResponse>(
            string endpoint, 
            Dictionary<string, string> queryParams = null, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a POST request to the specified endpoint
        /// </summary>
        /// <typeparam name="TRequest">The type of the request body</typeparam>
        /// <typeparam name="TResponse">The type to deserialize the response to</typeparam>
        /// <param name="endpoint">API endpoint path</param>
        /// <param name="requestBody">Request body object to serialize</param>
        /// <param name="queryParams">Optional query parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized response</returns>
        Task<TResponse> PostAsync<TRequest, TResponse>(
            string endpoint, 
            TRequest requestBody, 
            Dictionary<string, string> queryParams = null, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a PUT request to the specified endpoint
        /// </summary>
        /// <typeparam name="TRequest">The type of the request body</typeparam>
        /// <typeparam name="TResponse">The type to deserialize the response to</typeparam>
        /// <param name="endpoint">API endpoint path</param>
        /// <param name="requestBody">Request body object to serialize</param>
        /// <param name="queryParams">Optional query parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized response</returns>
        Task<TResponse> PutAsync<TRequest, TResponse>(
            string endpoint, 
            TRequest requestBody, 
            Dictionary<string, string> queryParams = null, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a DELETE request to the specified endpoint
        /// </summary>
        /// <typeparam name="TResponse">The type to deserialize the response to</typeparam>
        /// <param name="endpoint">API endpoint path</param>
        /// <param name="queryParams">Optional query parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized response</returns>
        Task<TResponse> DeleteAsync<TResponse>(
            string endpoint, 
            Dictionary<string, string> queryParams = null, 
            CancellationToken cancellationToken = default);
    }
}
