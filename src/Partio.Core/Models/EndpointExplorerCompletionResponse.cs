namespace Partio.Core.Models
{
    /// <summary>
    /// Response payload for inference endpoint explorer requests.
    /// </summary>
    public class EndpointExplorerCompletionResponse
    {
        /// <summary>
        /// Whether the explorer request succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Explorer result status code.
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
        public long ResponseTimeMs { get; set; } = 0;

        /// <summary>
        /// Related request-history entry ID when persisted.
        /// </summary>
        public string? RequestHistoryId { get; set; }

        /// <summary>
        /// Upstream completion calls made by Partio for this explorer request.
        /// </summary>
        public List<CompletionCallDetail> CompletionCalls { get; set; } = new List<CompletionCallDetail>();
    }
}
