using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Services.Api
{
    /// <summary>
    /// Factory for creating and caching API clients
    /// </summary>
    public class ApiClientFactory : IApiClientFactory
    {
        private readonly ILogger<ApiClient> _logger;
        private readonly ConcurrentDictionary<string, IApiClient> _clientCache = new ConcurrentDictionary<string, IApiClient>();
        private readonly Dictionary<string, ApiConfiguration> _apiConfigurations;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiClientFactory"/> class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="apiConfigurations">Configuration for various APIs</param>
        public ApiClientFactory(ILogger<ApiClient> logger, Dictionary<string, ApiConfiguration> apiConfigurations)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiConfigurations = apiConfigurations ?? throw new ArgumentNullException(nameof(apiConfigurations));
        }

        /// <inheritdoc />
        public IApiClient CreateClient(string apiName)
        {
            if (string.IsNullOrWhiteSpace(apiName))
            {
                throw new ArgumentException("API name cannot be null or empty", nameof(apiName));
            }

            // Try to get from cache first
            if (_clientCache.TryGetValue(apiName, out IApiClient cachedClient))
            {
                return cachedClient;
            }

            // Get configuration for the specified API
            if (!_apiConfigurations.TryGetValue(apiName, out ApiConfiguration config))
            {
                throw new ArgumentException($"No configuration found for API: {apiName}", nameof(apiName));
            }

            // Create a new client
            var client = new ApiClient(config.BaseUrl, _logger, config.ApiKey);
            
            // Cache the client
            _clientCache.TryAdd(apiName, client);
            
            return client;
        }
    }

    /// <summary>
    /// Configuration for an API
    /// </summary>
    public class ApiConfiguration
    {
        /// <summary>
        /// The base URL of the API
        /// </summary>
        public string BaseUrl { get; set; }
        
        /// <summary>
        /// API key for authentication, if required
        /// </summary>
        public string ApiKey { get; set; }
        
        /// <summary>
        /// Timeout in seconds for API requests
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
    }
}
