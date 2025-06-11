using System;
using System.Net;

namespace TransportTracker.Core.Services.Api
{
    /// <summary>
    /// Exception thrown when an API request fails
    /// </summary>
    public class ApiClientException : Exception
    {
        /// <summary>
        /// HTTP status code of the response
        /// </summary>
        public HttpStatusCode StatusCode { get; set; }
        
        /// <summary>
        /// Raw content of the response
        /// </summary>
        public string ResponseContent { get; set; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiClientException"/> class
        /// </summary>
        public ApiClientException() : base()
        {
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiClientException"/> class with a message
        /// </summary>
        /// <param name="message">Exception message</param>
        public ApiClientException(string message) : base(message)
        {
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiClientException"/> class with a message and inner exception
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="innerException">Inner exception</param>
        public ApiClientException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
