namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Response body for a completion request: the generated text plus timing and upstream call details.
    /// On an upstream failure (for example a timeout) the route returns HTTP 200 with
    /// <see cref="Success"/> false and <see cref="StatusCode"/> reflecting the upstream failure.
    /// </summary>
    public class CompletionResponse
    {
        [JsonPropertyName("Success")]
        public bool Success { get; set; }

        [JsonPropertyName("StatusCode")]
        public int StatusCode { get; set; }

        [JsonPropertyName("Error")]
        public string? Error { get; set; }

        [JsonPropertyName("EndpointId")]
        public string? EndpointId { get; set; }

        [JsonPropertyName("Model")]
        public string? Model { get; set; }

        [JsonPropertyName("Prompt")]
        public string? Prompt { get; set; }

        [JsonPropertyName("SystemPrompt")]
        public string? SystemPrompt { get; set; }

        [JsonPropertyName("Output")]
        public string? Output { get; set; }

        [JsonPropertyName("ResponseTimeMs")]
        public double ResponseTimeMs { get; set; }

        [JsonPropertyName("RequestHistoryId")]
        public string? RequestHistoryId { get; set; }

        [JsonPropertyName("CompletionCalls")]
        public List<CompletionCallDetail>? CompletionCalls { get; set; }
    }
}
