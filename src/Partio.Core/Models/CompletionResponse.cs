namespace Partio.Core.Models
{
    /// <summary>
    /// Response body for a completion request: the generated text plus timing and upstream call details.
    /// On an upstream failure (for example a timeout), the route still returns HTTP 200 with
    /// <see cref="Success"/> set to false and <see cref="StatusCode"/> reflecting the upstream failure.
    /// </summary>
    public class CompletionResponse
    {
        /// <summary>
        /// Whether the completion request succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Result status code. May differ from the HTTP status when an upstream call fails
        /// (for example 504 on timeout) while the route itself returns 200.
        /// </summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// Error text when the request fails.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Target completion endpoint ID.
        /// </summary>
        public string? EndpointId { get; set; }

        /// <summary>
        /// Target model name.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Prompt sent to the model.
        /// </summary>
        public string? Prompt { get; set; }

        /// <summary>
        /// Optional system prompt sent to the model.
        /// </summary>
        public string? SystemPrompt { get; set; }

        /// <summary>
        /// Completion text returned by the model.
        /// </summary>
        public string? Output { get; set; }

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
