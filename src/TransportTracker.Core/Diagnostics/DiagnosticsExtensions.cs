using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Diagnostics
{
    /// <summary>
    /// Extensions for registering diagnostics tools with the dependency injection container
    /// </summary>
    public static class DiagnosticsExtensions
    {
        /// <summary>
        /// Adds core diagnostics services to the service collection
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddDiagnostics(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            
            // Register ThreadingMetricsCollector
            services.AddSingleton(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ThreadingMetricsCollector>>();
                return new ThreadingMetricsCollector(logger);
            });
            
            // Register DiagnosticLogger
            services.AddSingleton(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<DiagnosticLogger>>();
                return new DiagnosticLogger(logger);
            });
            
            // Register PerformanceMonitoringDashboard
            services.AddSingleton(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<PerformanceMonitoringDashboard>>();
                var metricsCollector = sp.GetRequiredService<ThreadingMetricsCollector>();
                return new PerformanceMonitoringDashboard(logger, metricsCollector);
            });
            
            // Register ThreadDeadlockDetector
            services.AddSingleton(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ThreadDeadlockDetector>>();
                var diagnosticLogger = sp.GetRequiredService<DiagnosticLogger>();
                return new ThreadDeadlockDetector(logger, diagnosticLogger);
            });

            return services;
        }
        
        /// <summary>
        /// Adds and automatically starts all diagnostics services
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="startImmediately">Whether to start the services immediately</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddAndStartDiagnostics(
            this IServiceCollection services,
            bool startImmediately = true)
        {
            services.AddDiagnostics();
            
            if (startImmediately)
            {
                // Register hosted service to start diagnostics automatically
                services.AddHostedService<DiagnosticsStartupService>();
            }
            
            return services;
        }
        
        /// <summary>
        /// Hosted service to start diagnostics services automatically
        /// </summary>
        private class DiagnosticsStartupService : Microsoft.Extensions.Hosting.BackgroundService
        {
            private readonly ThreadingMetricsCollector _metricsCollector;
            private readonly PerformanceMonitoringDashboard _dashboard;
            private readonly ThreadDeadlockDetector _deadlockDetector;
            private readonly ILogger<DiagnosticsStartupService> _logger;
            
            public DiagnosticsStartupService(
                ThreadingMetricsCollector metricsCollector,
                PerformanceMonitoringDashboard dashboard,
                ThreadDeadlockDetector deadlockDetector,
                ILogger<DiagnosticsStartupService> logger)
            {
                _metricsCollector = metricsCollector;
                _dashboard = dashboard;
                _deadlockDetector = deadlockDetector;
                _logger = logger;
            }
            
            protected override System.Threading.Tasks.Task ExecuteAsync(System.Threading.CancellationToken stoppingToken)
            {
                _logger.LogInformation("Starting diagnostics services");
                
                try
                {
                    // Start all diagnostic services
                    _metricsCollector.Start();
                    _dashboard.Start();
                    _deadlockDetector.Start();
                    
                    _logger.LogInformation("All diagnostics services started successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start one or more diagnostics services");
                }
                
                return System.Threading.Tasks.Task.CompletedTask;
            }
            
            public override async System.Threading.Tasks.Task StopAsync(System.Threading.CancellationToken cancellationToken)
            {
                _logger.LogInformation("Stopping diagnostics services");
                
                _metricsCollector.Stop();
                _dashboard.Stop();
                _deadlockDetector.Stop();
                
                await base.StopAsync(cancellationToken);
            }
        }
    }
}
