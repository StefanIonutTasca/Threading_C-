using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TransportTracker.Core.Services.Api
{
    /// <summary>
    /// Helper class for serialization and deserialization
    /// </summary>
    public static class SerializationHelper
    {
        private static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Serialize an object to a JSON string
        /// </summary>
        /// <typeparam name="T">Type of object to serialize</typeparam>
        /// <param name="obj">Object to serialize</param>
        /// <param name="options">Optional serializer options</param>
        /// <returns>JSON string</returns>
        public static string Serialize<T>(T obj, JsonSerializerOptions options = null)
        {
            return JsonSerializer.Serialize(obj, options ?? DefaultOptions);
        }

        /// <summary>
        /// Deserialize a JSON string to an object
        /// </summary>
        /// <typeparam name="T">Type to deserialize to</typeparam>
        /// <param name="json">JSON string</param>
        /// <param name="options">Optional serializer options</param>
        /// <returns>Deserialized object</returns>
        public static T Deserialize<T>(string json, JsonSerializerOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;

            return JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions);
        }

        /// <summary>
        /// Deserialize a JSON stream to an object asynchronously
        /// </summary>
        /// <typeparam name="T">Type to deserialize to</typeparam>
        /// <param name="stream">Stream containing JSON data</param>
        /// <param name="options">Optional serializer options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Deserialized object</returns>
        public static async Task<T> DeserializeAsync<T>(
            Stream stream, 
            JsonSerializerOptions options = null, 
            CancellationToken cancellationToken = default)
        {
            if (stream == null)
                return default;

            return await JsonSerializer.DeserializeAsync<T>(
                stream, 
                options ?? DefaultOptions, 
                cancellationToken);
        }

        /// <summary>
        /// Try to deserialize a JSON string to an object, returning a default value if it fails
        /// </summary>
        /// <typeparam name="T">Type to deserialize to</typeparam>
        /// <param name="json">JSON string</param>
        /// <param name="result">Output parameter for the deserialized object</param>
        /// <param name="options">Optional serializer options</param>
        /// <returns>True if deserialization succeeded, false otherwise</returns>
        public static bool TryDeserialize<T>(string json, out T result, JsonSerializerOptions options = null)
        {
            result = default;
            
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                result = Deserialize<T>(json, options);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
