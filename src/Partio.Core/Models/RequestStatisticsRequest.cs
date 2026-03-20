namespace Partio.Core.Models
{
    /// <summary>
    /// Request object for retrieving request history statistics.
    /// </summary>
    public class RequestStatisticsRequest
    {
        /// <summary>
        /// Filter by request type: "Embedding", "Inference", or null/empty for all.
        /// Embedding matches URLs containing /process or /embedding.
        /// Inference matches URLs containing /completion.
        /// </summary>
        public string? RequestType { get; set; } = null;

        /// <summary>
        /// Time range: "Hour" (1-min buckets), "Day" (15-min buckets),
        /// "Week" (1-hour buckets), or "Month" (4-hour buckets).
        /// Defaults to "Day" if not specified.
        /// </summary>
        public string? Timeframe { get; set; } = "Day";

        /// <summary>
        /// Optional URL substring filter to narrow results to a specific endpoint.
        /// </summary>
        public string? EndpointFilter { get; set; } = null;
    }
}
