namespace TransportTracker.Core.Services.Api
{
    /// <summary>
    /// Configuration options for the Transport API client
    /// </summary>
    public class TransportApiConfiguration
    {
        /// <summary>
        /// Gets or sets the base URL for the API
        /// </summary>
        public string BaseUrl { get; set; }
        
        /// <summary>
        /// Gets or sets the API key for authentication
        /// </summary>
        public string ApiKey { get; set; }
        
        /// <summary>
        /// Gets or sets the timeout in seconds for API requests
        /// Default: 30 seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
        
        /// <summary>
        /// Gets or sets the maximum number of retry attempts for API requests
        /// Default: 3 attempts
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;
        
        /// <summary>
        /// Gets or sets the exponential backoff base in milliseconds
        /// Default: 200 ms
        /// </summary>
        public int BackoffBaseMs { get; set; } = 200;
        
        /// <summary>
        /// Gets or sets whether to use mock API for development/testing
        /// Default: false (use real API)
        /// </summary>
        public bool UseMockApi { get; set; } = false;
        
        /// <summary>
        /// Gets or sets the cache expiration for routes in minutes
        /// Default: 60 minutes (1 hour)
        /// </summary>
        public int RouteCacheExpirationMinutes { get; set; } = 60;
        
        /// <summary>
        /// Gets or sets the cache expiration for stops in minutes
        /// Default: 120 minutes (2 hours)
        /// </summary>
        public int StopCacheExpirationMinutes { get; set; } = 120;
        
        /// <summary>
        /// Gets or sets the cache expiration for vehicles in seconds
        /// Default: 30 seconds
        /// </summary>
        public int VehicleCacheExpirationSeconds { get; set; } = 30;
        
        /// <summary>
        /// Gets or sets the cache expiration for schedules in minutes
        /// Default: 5 minutes
        /// </summary>
        public int ScheduleCacheExpirationMinutes { get; set; } = 5;
        
        /// <summary>
        /// Gets or sets the vehicle position update interval in seconds
        /// Default: 5 seconds
        /// </summary>
        public int VehicleUpdateIntervalSeconds { get; set; } = 5;
        
        /// <summary>
        /// Gets or sets the schedule update interval in seconds
        /// Default: 60 seconds
        /// </summary>
        public int ScheduleUpdateIntervalSeconds { get; set; } = 60;
    }
}
