using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TransportTracker.Core.Services.Api.Transport.Models
{
    /// <summary>
    /// Response from the GTFS feeds downloads endpoint
    /// </summary>
    public class GtfsFeedsDownloadsResponse
    {
        /// <summary>
        /// List of GTFS feeds
        /// </summary>
        [JsonPropertyName("feeds")]
        public List<GtfsFeed> Feeds { get; set; } = new List<GtfsFeed>();
        
        /// <summary>
        /// Request processing time at server in milliseconds
        /// </summary>
        [JsonPropertyName("processingTimeMs")]
        public double ProcessingTimeMs { get; set; }
    }
    
    /// <summary>
    /// GTFS feed information
    /// </summary>
    public class GtfsFeed
    {
        /// <summary>
        /// Feed identifier
        /// </summary>
        [JsonPropertyName("feedId")]
        public string FeedId { get; set; }
        
        /// <summary>
        /// ISO country code (e.g., "GBR")
        /// </summary>
        [JsonPropertyName("countryIso")]
        public string CountryIso { get; set; }
        
        /// <summary>
        /// Name of the country
        /// </summary>
        [JsonPropertyName("countryName")]
        public string CountryName { get; set; }
        
        /// <summary>
        /// Name of the feed
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        /// <summary>
        /// URL to download the feed
        /// </summary>
        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; }
        
        /// <summary>
        /// URL of the provider's website
        /// </summary>
        [JsonPropertyName("providerUrl")]
        public string ProviderUrl { get; set; }
        
        /// <summary>
        /// License information
        /// </summary>
        [JsonPropertyName("license")]
        public License License { get; set; }
        
        /// <summary>
        /// Type of feed (e.g., "gtfs", "gtfs-rt")
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        /// <summary>
        /// Last time the feed was fetched
        /// </summary>
        [JsonPropertyName("lastFetched")]
        public string LastFetched { get; set; }
        
        /// <summary>
        /// Gets the DateTime when the feed was last fetched
        /// </summary>
        /// <returns>DateTime representation of the last fetched time, or null if not available</returns>
        public DateTime? GetLastFetchedDateTime()
        {
            if (string.IsNullOrEmpty(LastFetched))
                return null;
                
            DateTime result;
            if (DateTime.TryParse(LastFetched, out result))
                return result;
                
            return null;
        }
    }
    
    /// <summary>
    /// License information
    /// </summary>
    public class License
    {
        /// <summary>
        /// Type of license (e.g., "CC0", "CC-BY")
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        /// <summary>
        /// URL of the license text
        /// </summary>
        [JsonPropertyName("url")]
        public string Url { get; set; }
        
        /// <summary>
        /// Attribution text for the license
        /// </summary>
        [JsonPropertyName("attribution")]
        public string Attribution { get; set; }
    }
}
