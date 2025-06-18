using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TransportTracker.Core.Services.Api
{
    /// <summary>
    /// Extension methods for HttpContent to support JSON deserialization
    /// </summary>
    public static class HttpContentExtensions
    {
        private static readonly JsonSerializerOptions DefaultJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
        
        /// <summary>
        /// Reads HTTP content as the specified type, deserializing from JSON
        /// </summary>
        /// <typeparam name="T">Type to deserialize into</typeparam>
        /// <param name="content">The HTTP content</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The deserialized object</returns>
        public static async Task<T> ReadAsAsync<T>(this HttpContent content, CancellationToken cancellationToken = default)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
                
            string jsonContent = await content.ReadAsStringAsync(cancellationToken);
            
            if (string.IsNullOrEmpty(jsonContent))
                return default;
                
            return JsonSerializer.Deserialize<T>(jsonContent, DefaultJsonOptions);
        }
        
        /// <summary>
        /// Reads HTTP content as the specified type, deserializing from JSON with custom options
        /// </summary>
        /// <typeparam name="T">Type to deserialize into</typeparam>
        /// <param name="content">The HTTP content</param>
        /// <param name="options">JSON deserialization options</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The deserialized object</returns>
        public static async Task<T> ReadAsAsync<T>(this HttpContent content, JsonSerializerOptions options, CancellationToken cancellationToken = default)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
                
            string jsonContent = await content.ReadAsStringAsync(cancellationToken);
            
            if (string.IsNullOrEmpty(jsonContent))
                return default;
                
            return JsonSerializer.Deserialize<T>(jsonContent, options);
        }
    }
}
