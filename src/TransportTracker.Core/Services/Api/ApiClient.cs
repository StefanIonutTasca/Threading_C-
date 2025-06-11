using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;
using Polly.Timeout;

namespace TransportTracker.Core.Services.Api
{
    /// <summary>
    /// Implementation of the API client for transport data
    /// </summary>
    public class ApiClient : IApiClient, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiClient> _logger;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly AsyncTimeoutPolicy _timeoutPolicy;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiClient"/> class
        /// </summary>
        /// <param name="baseUrl">Base URL for the API</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="apiKey">Optional API key for authentication</param>
        public ApiClient(string baseUrl, ILogger<ApiClient> logger, string apiKey = null)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Configure default headers
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            // Add API key if provided
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            }

            // Configure JSON serializer
            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            // Create retry policy with exponential backoff
            _retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError() // HttpRequestException, 5XX and 408 status codes
                .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests) // 429 status code
                .WaitAndRetryAsync(
                    3, // Retry 3 times
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                    onRetry: (outcome, timespan, retryAttempt, context) =>
                    {
                        _logger.LogWarning(
                            "Request failed. Retrying in {RetryTimespan}s. Attempt {RetryCount}",
                            timespan.TotalSeconds, retryAttempt);
                    });

            // Create timeout policy
            _timeoutPolicy = Policy.TimeoutAsync(10, TimeoutStrategy.Optimistic, (context, timespan, task) =>
            {
                _logger.LogWarning("Request timed out after {TimeoutSeconds}s", timespan.TotalSeconds);
                return Task.CompletedTask;
            });
        }

        /// <inheritdoc />
        public async Task<TResponse> GetAsync<TResponse>(
            string endpoint, 
            Dictionary<string, string> queryParams = null, 
            CancellationToken cancellationToken = default)
        {
            string url = BuildUrl(endpoint, queryParams);
            
            _logger.LogDebug("Sending GET request to {Url}", url);

            HttpResponseMessage response = await SendWithPoliciesAsync(
                () => _httpClient.GetAsync(url, cancellationToken),
                cancellationToken);

            return await ProcessResponseAsync<TResponse>(response, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<TResponse> PostAsync<TRequest, TResponse>(
            string endpoint, 
            TRequest requestBody, 
            Dictionary<string, string> queryParams = null, 
            CancellationToken cancellationToken = default)
        {
            string url = BuildUrl(endpoint, queryParams);
            
            _logger.LogDebug("Sending POST request to {Url}", url);

            string jsonContent = JsonSerializer.Serialize(requestBody, _serializerOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await SendWithPoliciesAsync(
                () => _httpClient.PostAsync(url, content, cancellationToken),
                cancellationToken);

            return await ProcessResponseAsync<TResponse>(response, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<TResponse> PutAsync<TRequest, TResponse>(
            string endpoint, 
            TRequest requestBody, 
            Dictionary<string, string> queryParams = null, 
            CancellationToken cancellationToken = default)
        {
            string url = BuildUrl(endpoint, queryParams);
            
            _logger.LogDebug("Sending PUT request to {Url}", url);

            string jsonContent = JsonSerializer.Serialize(requestBody, _serializerOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await SendWithPoliciesAsync(
                () => _httpClient.PutAsync(url, content, cancellationToken),
                cancellationToken);

            return await ProcessResponseAsync<TResponse>(response, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<TResponse> DeleteAsync<TResponse>(
            string endpoint, 
            Dictionary<string, string> queryParams = null, 
            CancellationToken cancellationToken = default)
        {
            string url = BuildUrl(endpoint, queryParams);
            
            _logger.LogDebug("Sending DELETE request to {Url}", url);

            HttpResponseMessage response = await SendWithPoliciesAsync(
                () => _httpClient.DeleteAsync(url, cancellationToken),
                cancellationToken);

            return await ProcessResponseAsync<TResponse>(response, cancellationToken);
        }

        /// <summary>
        /// Builds a URL from an endpoint and query parameters
        /// </summary>
        /// <param name="endpoint">API endpoint</param>
        /// <param name="queryParams">Query parameters</param>
        /// <returns>The complete URL</returns>
        private string BuildUrl(string endpoint, Dictionary<string, string> queryParams)
        {
            if (queryParams == null || queryParams.Count == 0)
            {
                return endpoint;
            }

            var queryString = new StringBuilder("?");
            
            foreach (var param in queryParams)
            {
                if (queryString.Length > 1)
                {
                    queryString.Append('&');
                }
                
                queryString.Append(WebUtility.UrlEncode(param.Key));
                queryString.Append('=');
                queryString.Append(WebUtility.UrlEncode(param.Value));
            }

            return endpoint + queryString.ToString();
        }

        /// <summary>
        /// Sends a request with retry and timeout policies
        /// </summary>
        /// <param name="request">The request to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The response message</returns>
        private async Task<HttpResponseMessage> SendWithPoliciesAsync(
            Func<Task<HttpResponseMessage>> request,
            CancellationToken cancellationToken)
        {
            // Apply timeout and retry policies
            return await _timeoutPolicy
                .WrapAsync(_retryPolicy)
                .ExecuteAsync(async (ct) =>
                {
                    try
                    {
                        return await request();
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Error sending HTTP request");
                        throw;
                    }
                }, cancellationToken);
        }

        /// <summary>
        /// Processes an HTTP response
        /// </summary>
        /// <typeparam name="T">The type to deserialize to</typeparam>
        /// <param name="response">The HTTP response</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deserialized object</returns>
        private async Task<T> ProcessResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            try
            {
                // Ensure the response was successful
                response.EnsureSuccessStatusCode();

                // Read the response content
                string content = await response.Content.ReadAsStringAsync(cancellationToken);

                // Deserialize the content
                T result = JsonSerializer.Deserialize<T>(content, _serializerOptions);
                
                return result;
            }
            catch (HttpRequestException ex)
            {
                string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(ex, "API request failed with status code {StatusCode}. Response content: {ResponseContent}", 
                    response.StatusCode, responseContent);
                throw new ApiClientException($"API request failed with status code {response.StatusCode}", ex)
                {
                    StatusCode = response.StatusCode,
                    ResponseContent = responseContent
                };
            }
            catch (JsonException ex)
            {
                string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(ex, "Failed to deserialize API response: {ResponseContent}", responseContent);
                throw new ApiClientException("Failed to deserialize API response", ex)
                {
                    StatusCode = response.StatusCode,
                    ResponseContent = responseContent
                };
            }
        }

        /// <summary>
        /// Disposes the HttpClient
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes managed resources
        /// </summary>
        /// <param name="disposing">Whether we're disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~ApiClient()
        {
            Dispose(false);
        }
    }
}
