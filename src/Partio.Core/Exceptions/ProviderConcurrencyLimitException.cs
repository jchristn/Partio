namespace Partio.Core.Exceptions
{
    /// <summary>
    /// Thrown when an endpoint has reached its maximum number of concurrent upstream provider requests
    /// and its wait queue (if any) is full.
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
        /// Maximum number of requests allowed to wait for a concurrency slot before rejection.
        /// A value of <c>0</c> means requests are rejected immediately when the concurrency limit is reached.
        /// </summary>
        public int MaxQueueDepth { get; }

        /// <summary>
        /// Initialize a new ProviderConcurrencyLimitException with no wait queue (<see cref="MaxQueueDepth"/> defaults to <c>0</c>).
        /// </summary>
        /// <param name="concurrencyKey">Endpoint concurrency key that was rejected.</param>
        /// <param name="maxConcurrentRequests">Maximum allowed concurrent upstream requests for the endpoint.</param>
        /// <param name="message">Human-readable error message.</param>
        public ProviderConcurrencyLimitException(string concurrencyKey, int maxConcurrentRequests, string message)
            : this(concurrencyKey, maxConcurrentRequests, 0, message)
        {
        }

        /// <summary>
        /// Initialize a new ProviderConcurrencyLimitException.
        /// </summary>
        /// <param name="concurrencyKey">Endpoint concurrency key that was rejected.</param>
        /// <param name="maxConcurrentRequests">Maximum allowed concurrent upstream requests for the endpoint.</param>
        /// <param name="maxQueueDepth">Maximum number of requests allowed to wait for a slot.</param>
        /// <param name="message">Human-readable error message.</param>
        public ProviderConcurrencyLimitException(string concurrencyKey, int maxConcurrentRequests, int maxQueueDepth, string message)
            : base(message)
        {
            ConcurrencyKey = concurrencyKey;
            MaxConcurrentRequests = maxConcurrentRequests < 1 ? 1 : maxConcurrentRequests;
            MaxQueueDepth = maxQueueDepth < 0 ? 0 : maxQueueDepth;
        }
    }
}
