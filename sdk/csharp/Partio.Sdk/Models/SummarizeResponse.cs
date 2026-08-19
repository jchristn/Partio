namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Response body for a summarize request: the generated summary text.
    /// </summary>
    public class SummarizeResponse
    {
        [JsonPropertyName("Success")]
        public bool Success { get; set; }

        [JsonPropertyName("StatusCode")]
        public int StatusCode { get; set; }

        [JsonPropertyName("Error")]
        public string? Error { get; set; }

        [JsonPropertyName("CompletionEndpointId")]
        public string? CompletionEndpointId { get; set; }

        [JsonPropertyName("Model")]
        public string? Model { get; set; }

        [JsonPropertyName("Summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("Summaries")]
        public List<string> Summaries { get; set; } = new List<string>();

        [JsonPropertyName("ResponseTimeMs")]
        public double ResponseTimeMs { get; set; }

        [JsonPropertyName("RequestHistoryId")]
        public string? RequestHistoryId { get; set; }

        [JsonPropertyName("CompletionCalls")]
        public List<CompletionCallDetail>? CompletionCalls { get; set; }
    }
}
