using System;
using Microsoft.Extensions.DependencyInjection;
using TransportTracker.Core.Threading.Coordination;

namespace TransportTracker.Core.Threading
{
    /// <summary>
    /// Extension methods for registering threading services with the dependency injection container
    /// </summary>
    public static class ThreadingServiceExtensions
    {
        /// <summary>
        /// Adds core threading services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddThreadingServices(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Register thread factory as a singleton
            services.AddSingleton<IThreadFactory, ThreadFactory>();

            // Register thread coordinator as a singleton
            services.AddSingleton<ThreadCoordinator>();

            return services;
        }

        /// <summary>
        /// Adds advanced threading services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configureBatchProcessing">Whether to configure batch processing components</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddAdvancedThreadingServices(
            this IServiceCollection services,
            bool configureBatchProcessing = true)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Add core threading services
            services.AddThreadingServices();

            // Register batch processing components if requested
            if (configureBatchProcessing)
            {
                // These will be implemented later in Day 11: Batch Processing Implementation
                // For now we just have placeholders to prepare the architecture
            }

            return services;
        }
    }
}
