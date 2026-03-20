namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class EndpointExplorerEmbeddingResponse
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

        [JsonPropertyName("Input")]
        public string? Input { get; set; }

        [JsonPropertyName("Embedding")]
        public List<float>? Embedding { get; set; }

        [JsonPropertyName("Dimensions")]
        public int Dimensions { get; set; }

        [JsonPropertyName("ResponseTimeMs")]
        public long ResponseTimeMs { get; set; }

        [JsonPropertyName("RequestHistoryId")]
        public string? RequestHistoryId { get; set; }

        [JsonPropertyName("EmbeddingCalls")]
        public List<EmbeddingCallDetail>? EmbeddingCalls { get; set; }
    }
}
