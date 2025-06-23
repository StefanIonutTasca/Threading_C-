namespace TransportTracker.Core.Services.Api
{
    /// <summary>
    /// Represents configuration for an API client.
    /// </summary>
    public class ApiClientConfiguration
    {
        public string BaseUrl { get; set; }
        public string ApiKey { get; set; }
        public int TimeoutSeconds { get; set; }
        // Add other properties as needed for your API clients
    }
}
