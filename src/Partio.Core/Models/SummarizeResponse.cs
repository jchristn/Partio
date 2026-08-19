namespace Partio.Core.Models
{
    /// <summary>
    /// Response body for a summarize request: the generated summary text (empty when the input did not
    /// meet the configured minimum length).
    /// </summary>
    public class SummarizeResponse
    {
        /// <summary>
        /// Whether the summarize request succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Result status code.
        /// </summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// Error text when the request fails.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Target completion endpoint ID.
        /// </summary>
        public string? CompletionEndpointId { get; set; }

        /// <summary>
        /// Target model name.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// The generated summary (first, when multiple were produced); empty when none was produced.
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// All generated summaries, in order.
        /// </summary>
        public List<string> Summaries { get; set; } = new List<string>();

        /// <summary>
        /// Overall request duration in milliseconds.
        /// </summary>
        public double ResponseTimeMs { get; set; } = 0;

        /// <summary>
        /// Related request-history entry ID when persisted.
        /// </summary>
        public string? RequestHistoryId { get; set; }

        /// <summary>
        /// Upstream completion calls made by Partio for this request.
        /// </summary>
        public List<CompletionCallDetail> CompletionCalls { get; set; } = new List<CompletionCallDetail>();
    }
}
