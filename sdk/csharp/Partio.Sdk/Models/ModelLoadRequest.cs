namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request payload for loading or warming a configured model endpoint.
    /// </summary>
    public class ModelLoadRequest
    {
        /// <summary>
        /// Requested strategy: Auto, NativeProviderLoad, or WarmRequest.
        /// </summary>
        [JsonPropertyName("Strategy")]
        public string Strategy { get; set; } = "Auto";

        /// <summary>
        /// Requested timeout in milliseconds. Zero uses the endpoint maximum.
        /// </summary>
        [JsonPropertyName("TimeoutMs")]
        public int TimeoutMs { get; set; }

        /// <summary>
        /// Provider keep-alive value. Applies to Ollama native load paths.
        /// </summary>
        [JsonPropertyName("KeepAlive")]
        public string? KeepAlive { get; set; } = "30m";

        /// <summary>
        /// Short non-sensitive input used for warm requests.
        /// </summary>
        [JsonPropertyName("SampleInput")]
        public string SampleInput { get; set; } = "Partio model load probe";

        /// <summary>
        /// Maximum completion tokens for warm completion requests.
        /// </summary>
        [JsonPropertyName("MaxTokens")]
        public int MaxTokens { get; set; } = 1;

        /// <summary>
        /// Whether detailed request history should include the load attempt.
        /// </summary>
        [JsonPropertyName("RecordRequestHistory")]
        public bool RecordRequestHistory { get; set; } = true;

        /// <summary>
        /// Whether the caller requires a provider-native load operation.
        /// </summary>
        [JsonPropertyName("RequireNativeLoad")]
        public bool RequireNativeLoad { get; set; }
    }
}
