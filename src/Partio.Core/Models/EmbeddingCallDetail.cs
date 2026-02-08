namespace Partio.Core.Models
{
    /// <summary>
    /// Captures details of a single HTTP call made to an upstream embedding endpoint.
    /// </summary>
    public class EmbeddingCallDetail
    {
        /// <summary>
        /// Full URL called.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// HTTP method (e.g. POST).
        /// </summary>
        public string? Method { get; set; }

        /// <summary>
        /// Headers sent upstream.
        /// </summary>
        public Dictionary<string, string>? RequestHeaders { get; set; }

        /// <summary>
        /// Body sent upstream.
        /// </summary>
        public string? RequestBody { get; set; }

        /// <summary>
        /// HTTP status code returned.
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// Response headers.
        /// </summary>
        public Dictionary<string, string>? ResponseHeaders { get; set; }

        /// <summary>
        /// Response body.
        /// </summary>
        public string? ResponseBody { get; set; }

        /// <summary>
        /// Timing for this call in milliseconds.
        /// </summary>
        public long? ResponseTimeMs { get; set; }

        /// <summary>
        /// Whether the call succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the call failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// UTC timestamp when the call was initiated.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
