namespace Partio.Core.Exceptions
{
    /// <summary>
    /// Thrown when a request targets an embedding endpoint that is currently unhealthy.
    /// </summary>
    public class EndpointUnhealthyException : Exception
    {
        /// <summary>
        /// The ID of the unhealthy endpoint.
        /// </summary>
        public string EndpointId { get; }

        /// <summary>
        /// Initialize a new EndpointUnhealthyException.
        /// </summary>
        /// <param name="endpointId">Endpoint ID.</param>
        /// <param name="message">Error message.</param>
        public EndpointUnhealthyException(string endpointId, string message)
            : base(message)
        {
            EndpointId = endpointId;
        }
    }
}
