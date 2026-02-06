namespace Partio.Sdk
{
    using Partio.Sdk.Models;

    /// <summary>
    /// Exception thrown when a Partio API call fails.
    /// </summary>
    public class PartioException : Exception
    {
        /// <summary>HTTP status code.</summary>
        public int StatusCode { get; }

        /// <summary>Error response from the server, if any.</summary>
        public ApiErrorResponse? Response { get; }

        /// <summary>
        /// Initialize a new PartioException.
        /// </summary>
        public PartioException(string message, int statusCode, ApiErrorResponse? response = null)
            : base(message)
        {
            StatusCode = statusCode;
            Response = response;
        }
    }
}
