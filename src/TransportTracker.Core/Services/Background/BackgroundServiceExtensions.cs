using System;
using Microsoft.Extensions.DependencyInjection;

namespace TransportTracker.Core.Services.Background
{
    /// <summary>
    /// Extension methods for registering background services with the dependency injection container
    /// </summary>
    public static class BackgroundServiceExtensions
    {
        /// <summary>
        /// Adds transport API polling service to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddTransportApiPollingService(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Register transport polling service as singleton
            services.AddSingleton<IBackgroundPollingService, TransportApiPollingService>();
            
            return services;
        }
        
        /// <summary>
        /// Adds all background services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddBackgroundServices(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Add transport polling service
            services.AddTransportApiPollingService();
            
            // Register background services host as singleton
            services.AddSingleton<BackgroundServicesHost>();
            
            return services;
        }
    }
}
