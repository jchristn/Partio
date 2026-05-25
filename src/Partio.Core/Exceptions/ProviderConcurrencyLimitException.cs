namespace Partio.Core.Exceptions
{
    /// <summary>
    /// Thrown when an endpoint has reached its maximum number of concurrent upstream provider requests.
    /// </summary>
    public class ProviderConcurrencyLimitException : Exception
    {
        /// <summary>
        /// Endpoint concurrency key that was rejected.
        /// </summary>
        public string ConcurrencyKey { get; }

        /// <summary>
        /// Maximum allowed concurrent upstream requests for the endpoint.
        /// </summary>
        public int MaxConcurrentRequests { get; }

        /// <summary>
        /// Initialize a new ProviderConcurrencyLimitException.
        /// </summary>
        public ProviderConcurrencyLimitException(string concurrencyKey, int maxConcurrentRequests, string message)
            : base(message)
        {
            ConcurrencyKey = concurrencyKey;
            MaxConcurrentRequests = maxConcurrentRequests < 1 ? 1 : maxConcurrentRequests;
        }
    }
}
