namespace TransportTracker.Core.Services.Api
{
    /// <summary>
    /// Factory interface for creating API clients
    /// </summary>
    public interface IApiClientFactory
    {
        /// <summary>
        /// Creates an API client for the given API
        /// </summary>
        /// <param name="apiName">Name of the API to create a client for</param>
        /// <returns>An API client instance</returns>
        IApiClient CreateClient(string apiName);
    }
}
