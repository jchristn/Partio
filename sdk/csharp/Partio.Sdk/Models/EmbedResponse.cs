namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Response body for an embed request: one embedding vector per input string, in order.
    /// </summary>
    public class EmbedResponse
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

        [JsonPropertyName("Embeddings")]
        public List<List<float>> Embeddings { get; set; } = new List<List<float>>();

        [JsonPropertyName("Count")]
        public int Count { get; set; }

        [JsonPropertyName("Dimensions")]
        public int Dimensions { get; set; }

        [JsonPropertyName("L2Normalization")]
        public bool L2Normalization { get; set; }

        [JsonPropertyName("ResponseTimeMs")]
        public double ResponseTimeMs { get; set; }

        [JsonPropertyName("RequestHistoryId")]
        public string? RequestHistoryId { get; set; }

        [JsonPropertyName("EmbeddingCalls")]
        public List<EmbeddingCallDetail>? EmbeddingCalls { get; set; }

        [JsonPropertyName("TokenizationProfile")]
        public ResolvedTokenizationProfile? TokenizationProfile { get; set; }
    }
}
