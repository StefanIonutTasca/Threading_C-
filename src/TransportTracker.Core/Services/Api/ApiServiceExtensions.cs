using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TransportTracker.Core.Services.Api.Transport;

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

            return AddApiServices(services, apiConfigurations);
        }
    }
}
