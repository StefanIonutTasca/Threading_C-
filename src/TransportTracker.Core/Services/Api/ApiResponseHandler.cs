using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Services.Api
{
    /// <summary>
    /// Handles API responses with resilience policies, retry logic, and error handling
    /// </summary>
    internal class ApiResponseHandler
    {
        private readonly ILogger _logger;
        private readonly ApiUsageStatistics _apiUsageStatistics;
        
        /// <summary>
        /// Creates a new API response handler
        /// </summary>
        /// <param name="logger">Logger for diagnostics</param>
        /// <param name="apiUsageStatistics">Statistics to update</param>
        public ApiResponseHandler(ILogger logger, ApiUsageStatistics apiUsageStatistics)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiUsageStatistics = apiUsageStatistics ?? throw new ArgumentNullException(nameof(apiUsageStatistics));
        }
        
        /// <summary>
        /// Processes an API response with retry logic
        /// </summary>
        /// <typeparam name="T">Type of response to deserialize</typeparam>
        /// <param name="apiCall">Function that makes the API call</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="backoffMilliseconds">Base backoff time between retries in milliseconds</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Deserialized response</returns>
        public async Task<T> ProcessWithRetriesAsync<T>(
            Func<CancellationToken, Task<HttpResponseMessage>> apiCall,
            int maxRetries = 3,
            int backoffMilliseconds = 200,
            CancellationToken cancellationToken = default)
        {
            int attempt = 0;
            DateTime startTime = DateTime.UtcNow;
            
            while (true)
            {
                attempt++;
                
                try
                {
                    // Make the API call
                    using var response = await apiCall(cancellationToken);
                    
                    // Update statistics
                    _apiUsageStatistics.TotalApiCalls++;
                    _apiUsageStatistics.CurrentPeriodCalls++;
                    
                    // Check response headers for rate limiting
                    UpdateRateLimitInfo(response);
                    
                    // Process the response
                    if (response.IsSuccessStatusCode)
                    {
                        _apiUsageStatistics.SuccessfulCalls++;
                        
                        // Deserialize the response content
                        T result = await response.Content.ReadAsAsync<T>(cancellationToken);
                        
                        // Update response time metrics
                        UpdateResponseTimeMetrics(startTime);
                        
                        return result;
                    }
                    
                    // Handle specific error status codes
                    _apiUsageStatistics.FailedCalls++;
                    
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.TooManyRequests:
                            _logger.LogWarning("API rate limit exceeded. Waiting before retry.");
                            
                            // Check for Retry-After header
                            if (response.Headers.RetryAfter?.Delta.HasValue == true)
                            {
                                await Task.Delay(response.Headers.RetryAfter.Delta.Value, cancellationToken);
                            }
                            else
                            {
                                // Use exponential backoff with jitter
                                await ExponentialBackoffAsync(attempt, backoffMilliseconds, cancellationToken);
                            }
                            
                            // Check if we've exceeded max retries
                            if (attempt >= maxRetries)
                            {
                                _logger.LogError("Maximum retry attempts reached for API call after rate limiting.");
                                throw new HttpRequestException($"Rate limit exceeded. Status: {response.StatusCode}");
                            }
                            
                            // Retry the request
                            continue;
                            
                        case HttpStatusCode.Unauthorized:
                            _logger.LogError("API authentication failed with status code: {StatusCode}", response.StatusCode);
                            throw new UnauthorizedAccessException("API authentication failed. Please check your API key.");
                            
                        case HttpStatusCode.NotFound:
                            _logger.LogWarning("Resource not found: {StatusCode}", response.StatusCode);
                            return default; // Return null/default for the requested resource
                            
                        default:
                            // If status code indicates a server error, we can retry
                            if ((int)response.StatusCode >= 500)
                            {
                                if (attempt >= maxRetries)
                                {
                                    _logger.LogError("Maximum retry attempts reached for API call. Status: {StatusCode}", response.StatusCode);
                                    throw new HttpRequestException($"Failed after {maxRetries} attempts. Status: {response.StatusCode}");
                                }
                                
                                _logger.LogWarning("Server error: {StatusCode}. Retrying attempt {Attempt} of {MaxRetries}", 
                                    response.StatusCode, attempt, maxRetries);
                                
                                // Use exponential backoff with jitter
                                await ExponentialBackoffAsync(attempt, backoffMilliseconds, cancellationToken);
                                continue;
                            }
                            
                            // For other client errors, don't retry
                            _logger.LogError("API error with status code: {StatusCode}", response.StatusCode);
                            throw new HttpRequestException($"API returned error status: {response.StatusCode}");
                    }
                }
                catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Propagate cancellation
                    _logger.LogInformation("API call was cancelled by request");
                    throw;
                }
                catch (HttpRequestException ex) when (attempt < maxRetries && 
                                                     (ex.InnerException is TimeoutException || 
                                                      ex.InnerException is System.Net.Sockets.SocketException))
                {
                    // Network-related exceptions that we can retry
                    _apiUsageStatistics.FailedCalls++;
                    
                    _logger.LogWarning(ex, "Network error during API call. Retrying attempt {Attempt} of {MaxRetries}", 
                        attempt, maxRetries);
                    
                    // Use exponential backoff with jitter
                    await ExponentialBackoffAsync(attempt, backoffMilliseconds, cancellationToken);
                }
                catch (Exception ex) when (ex is not UnauthorizedAccessException && ex is not HttpRequestException)
                {
                    // Unexpected exception
                    _apiUsageStatistics.FailedCalls++;
                    _logger.LogError(ex, "Unexpected error during API call");
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Updates rate limit information from response headers
        /// </summary>
        private void UpdateRateLimitInfo(HttpResponseMessage response)
        {
            // Check for rate limit headers - names will vary by API
            if (response.Headers.TryGetValues("X-RateLimit-Limit", out var limits))
            {
                if (int.TryParse(string.Join("", limits), out int limit))
                {
                    _apiUsageStatistics.RateLimitPerPeriod = limit;
                }
            }
            
            if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining))
            {
                if (int.TryParse(string.Join("", remaining), out int rem))
                {
                    _apiUsageStatistics.CurrentPeriodCalls = _apiUsageStatistics.RateLimitPerPeriod - rem;
                }
            }
            
            if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resetTime))
            {
                if (long.TryParse(string.Join("", resetTime), out long epochTime))
                {
                    _apiUsageStatistics.RateLimitResetTime = DateTimeOffset.FromUnixTimeSeconds(epochTime).DateTime;
                }
            }
        }
        
        /// <summary>
        /// Updates response time metrics
        /// </summary>
        private void UpdateResponseTimeMetrics(DateTime startTime)
        {
            double responseTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            
            // Simple moving average calculation
            if (_apiUsageStatistics.AverageResponseTimeMs == 0)
            {
                _apiUsageStatistics.AverageResponseTimeMs = responseTimeMs;
            }
            else
            {
                // Weighted average giving 70% weight to historical data
                _apiUsageStatistics.AverageResponseTimeMs = 
                    (_apiUsageStatistics.AverageResponseTimeMs * 0.7) + (responseTimeMs * 0.3);
            }
        }
        
        /// <summary>
        /// Implements exponential backoff with jitter for retries
        /// </summary>
        private static async Task ExponentialBackoffAsync(int attempt, int baseBackoffMs, CancellationToken cancellationToken)
        {
            // Calculate exponential backoff
            int maxBackoff = baseBackoffMs * (int)Math.Pow(2, attempt - 1);
            
            // Add jitter to prevent synchronized retries from multiple clients
            int jitteredBackoff = new Random().Next((int)(maxBackoff * 0.8), maxBackoff + 1);
            
            await Task.Delay(jitteredBackoff, cancellationToken);
        }
    }
}
