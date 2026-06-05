namespace Partio.Core.Models
{
    using Partio.Core.Enums;

    /// <summary>
    /// Response payload for model load or warm requests.
    /// </summary>
    public class ModelLoadResponse
    {
        /// <summary>
        /// Whether the load or warm request succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Partio-mapped status code for the operation.
        /// </summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// Operation outcome.
        /// </summary>
        public ModelLoadOutcomeEnum Outcome { get; set; } = ModelLoadOutcomeEnum.Warmed;

        /// <summary>
        /// Target endpoint type.
        /// </summary>
        public EndpointTypeEnum EndpointType { get; set; }

        /// <summary>
        /// Target endpoint ID.
        /// </summary>
        public string EndpointId { get; set; } = string.Empty;

        /// <summary>
        /// Endpoint tenant ID.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Configured provider API format.
        /// </summary>
        public ApiFormatEnum ApiFormat { get; set; }

        /// <summary>
        /// Configured model name.
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Effective load strategy.
        /// </summary>
        public ModelLoadStrategyEnum Strategy { get; set; } = ModelLoadStrategyEnum.Auto;

        /// <summary>
        /// Operator-safe result message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Total operation duration in milliseconds.
        /// </summary>
        public double ResponseTimeMs { get; set; }

        /// <summary>
        /// UTC timestamp when the operation started.
        /// </summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp when the operation completed.
        /// </summary>
        public DateTime CompletedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Related request-history entry ID when persisted.
        /// </summary>
        public string? RequestHistoryId { get; set; }

        /// <summary>
        /// Captured upstream embedding calls.
        /// </summary>
        public List<EmbeddingCallDetail>? EmbeddingCalls { get; set; }

        /// <summary>
        /// Captured upstream completion calls.
        /// </summary>
        public List<CompletionCallDetail>? CompletionCalls { get; set; }
    }
}
