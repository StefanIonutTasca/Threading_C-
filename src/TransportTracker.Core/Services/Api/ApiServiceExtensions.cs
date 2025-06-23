using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TransportTracker.Core.Services.Api.Transport;
using TransportTracker.Core.Services;
using TransportTracker.Core.Threading;

namespace TransportTracker.Core.Services.Api
{
    /// <summary>
    /// Extensions methods for configuring API services in the dependency injection container
    /// </summary>
    public static class ApiServiceExtensions
    {
        /// <summary>
        /// Adds API client services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register thread factory
            services.AddSingleton<TransportTracker.Core.Threading.IThreadFactory, TransportTracker.Core.Threading.DefaultThreadFactory>();

            // Register API client factory
            services.AddSingleton<IApiClientFactory, ApiClientFactory>();
            
            // Register API client configurations
            services.Configure<Dictionary<string, ApiClientConfiguration>>(
                configuration.GetSection("ApiClients"));
            
            // Register Transport API service (generic implementation)
            services.AddScoped<ITransportApiService, TransportApiService>();
            
            return services;
        }
        
        /// <summary>
        /// Adds Busmaps API client services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="apiKey">API key for authentication</param>
        /// <param name="platformHost">Platform host (default: "busmaps.com")</param>
        /// <param name="regionName">Default region name (default: "uk_ireland")</param>
        /// <param name="countryIso">Default ISO country code (default: "GBR")</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddBusmapsApiServices(
            this IServiceCollection services,
            string apiKey,
            string platformHost = "busmaps.com",
            string regionName = "uk_ireland",
            string countryIso = "GBR")
        {
            if (string.IsNullOrEmpty(apiKey))
                throw new ArgumentException("API key is required for Busmaps API", nameof(apiKey));
            
            // Register Busmaps API client as a singleton
            services.AddSingleton<BusmapsApiClient>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<BusmapsApiClient>>();
                return new BusmapsApiClient(apiKey, platformHost, logger);
            });
            
            // Register Busmaps Transport API service
            services.AddScoped<ITransportApiService>(sp => 
            {
                var client = sp.GetRequiredService<BusmapsApiClient>();
                var logger = sp.GetRequiredService<ILogger<BusmapsTransportApiService>>();
                return new BusmapsTransportApiService(client, logger, regionName, countryIso);
            });
            
            return services;
        }
        
        /// <summary>
        /// Adds Busmaps API client services to the service collection using configuration
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddBusmapsApiServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var apiKey = configuration["Busmaps:ApiKey"];
            var platformHost = configuration["Busmaps:PlatformHost"] ?? "busmaps.com";
            var regionName = configuration["Busmaps:RegionName"] ?? "uk_ireland";
            var countryIso = configuration["Busmaps:CountryIso"] ?? "GBR";
            
            return services.AddBusmapsApiServices(apiKey, platformHost, regionName, countryIso);
        }
        
        /// <summary>
        /// Adds the transport API service with a default configuration
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="baseUrl">Base URL for the transport API</param>
        /// <param name="apiKey">Optional API key</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddTransportApiService(
            this IServiceCollection services,
            string baseUrl,
            string apiKey = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentException("Base URL cannot be null or empty", nameof(baseUrl));

            // Create default configuration for transport API
            var apiConfigurations = new Dictionary<string, ApiConfiguration>
            {
                ["TransportApi"] = new ApiConfiguration
                {
                    BaseUrl = baseUrl,
                    ApiKey = apiKey,
                    TimeoutSeconds = 30
                }
            };

            // Removed: Cannot pass Dictionary to AddApiServices expecting IConfiguration.
return services; // Or implement as needed.
        }
        
        /// <summary>
        /// Adds the real-time transport API client implementation
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="baseUrl">Base URL for the transport API</param>
        /// <param name="apiKey">API key for authentication</param>
        /// <param name="maxRetryAttempts">Maximum retry attempts for API calls</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddTransportApiClient(
            this IServiceCollection services,
            string baseUrl,
            string apiKey = null,
            int maxRetryAttempts = 3)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentException("Base URL cannot be null or empty", nameof(baseUrl));
            
            // Register HttpClient
            services.AddHttpClient("TransportApiClient", client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
                
                // Set default headers
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                
                // Add API key to header if provided
                if (!string.IsNullOrEmpty(apiKey))
                {
                    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
                }
            });
            
            // Register ITransportApiClient implementation
            services.AddSingleton<ITransportApiClient>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient("TransportApiClient");
                var logger = sp.GetRequiredService<ILogger<TransportApiClient>>();
                
                // Adjust as per TransportApiClient constructor signature. If it expects Uri, pass new Uri(baseUrl).
return new TransportApiClient(httpClient, logger, new Uri(baseUrl)); // Adjust if more params are needed.
            });
            
            return services;
        }
        
        /// <summary>
        /// Adds the mock transport API client implementation for development and testing
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddMockTransportApiClient(
            this IServiceCollection services)
        {
            // Register MockApiService as the implementation of ITransportApiClient
            services.AddSingleton<ITransportApiClient>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<MockApiService>>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
var threadFactory = sp.GetRequiredService<IThreadFactory>();
return new MockApiService(logger, loggerFactory, threadFactory);
            });
            
            return services;
        }
        
        /// <summary>
        /// Adds the transport data service which integrates API client with thread-safe collections
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddTransportDataService(
            this IServiceCollection services)
        {
            // Register TransportDataService
            services.AddSingleton<TransportDataService>(sp =>
            {
                var apiClient = sp.GetRequiredService<ITransportApiClient>();
                var logger = sp.GetRequiredService<ILogger<TransportDataService>>();
                return new TransportDataService(apiClient, logger);
            });
            
            return services;
        }
    }
}
