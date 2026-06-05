namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Response payload for model load or warm requests.
    /// </summary>
    public class ModelLoadResponse
    {
        /// <summary>
        /// Whether the load or warm request succeeded.
        /// </summary>
        [JsonPropertyName("Success")]
        public bool Success { get; set; }

        /// <summary>
        /// Partio-mapped status code for the operation.
        /// </summary>
        [JsonPropertyName("StatusCode")]
        public int StatusCode { get; set; }

        /// <summary>
        /// Operation outcome.
        /// </summary>
        [JsonPropertyName("Outcome")]
        public string? Outcome { get; set; }

        /// <summary>
        /// Target endpoint type.
        /// </summary>
        [JsonPropertyName("EndpointType")]
        public string? EndpointType { get; set; }

        /// <summary>
        /// Target endpoint ID.
        /// </summary>
        [JsonPropertyName("EndpointId")]
        public string? EndpointId { get; set; }

        /// <summary>
        /// Endpoint tenant ID.
        /// </summary>
        [JsonPropertyName("TenantId")]
        public string? TenantId { get; set; }

        /// <summary>
        /// Configured provider API format.
        /// </summary>
        [JsonPropertyName("ApiFormat")]
        public string? ApiFormat { get; set; }

        /// <summary>
        /// Configured model name.
        /// </summary>
        [JsonPropertyName("Model")]
        public string? Model { get; set; }

        /// <summary>
        /// Effective load strategy.
        /// </summary>
        [JsonPropertyName("Strategy")]
        public string? Strategy { get; set; }

        /// <summary>
        /// Operator-safe result message.
        /// </summary>
        [JsonPropertyName("Message")]
        public string? Message { get; set; }

        /// <summary>
        /// Total operation duration in milliseconds.
        /// </summary>
        [JsonPropertyName("ResponseTimeMs")]
        public double ResponseTimeMs { get; set; }

        /// <summary>
        /// UTC timestamp when the operation started.
        /// </summary>
        [JsonPropertyName("StartedUtc")]
        public DateTime StartedUtc { get; set; }

        /// <summary>
        /// UTC timestamp when the operation completed.
        /// </summary>
        [JsonPropertyName("CompletedUtc")]
        public DateTime CompletedUtc { get; set; }

        /// <summary>
        /// Related request-history entry ID when persisted.
        /// </summary>
        [JsonPropertyName("RequestHistoryId")]
        public string? RequestHistoryId { get; set; }

        /// <summary>
        /// Captured upstream embedding calls.
        /// </summary>
        [JsonPropertyName("EmbeddingCalls")]
        public List<EmbeddingCallDetail>? EmbeddingCalls { get; set; }

        /// <summary>
        /// Captured upstream completion calls.
        /// </summary>
        [JsonPropertyName("CompletionCalls")]
        public List<CompletionCallDetail>? CompletionCalls { get; set; }
    }
}
