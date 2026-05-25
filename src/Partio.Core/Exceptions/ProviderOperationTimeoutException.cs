namespace Partio.Core.Exceptions
{
    /// <summary>
    /// Indicates that an upstream embedding or inference provider operation exceeded its allowed timeout.
    /// </summary>
    public class ProviderOperationTimeoutException : Exception
    {
        /// <summary>
        /// Timeout budget, in milliseconds, that was exceeded.
        /// </summary>
        public int TimeoutMs { get; }

        /// <summary>
        /// Initialize a new timeout exception.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="timeoutMs">Timeout budget in milliseconds.</param>
        /// <param name="innerException">Optional inner exception.</param>
        public ProviderOperationTimeoutException(string message, int timeoutMs, Exception? innerException = null)
            : base(message, innerException)
        {
            TimeoutMs = timeoutMs;
        }
    }
}
