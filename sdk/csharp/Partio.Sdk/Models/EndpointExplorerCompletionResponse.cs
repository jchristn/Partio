namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class EndpointExplorerCompletionResponse
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
        public long ResponseTimeMs { get; set; }

        [JsonPropertyName("RequestHistoryId")]
        public string? RequestHistoryId { get; set; }

        [JsonPropertyName("CompletionCalls")]
        public List<CompletionCallDetail>? CompletionCalls { get; set; }
    }
}
