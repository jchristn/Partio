namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class EmbeddingEndpoint
    {
        [JsonPropertyName("Id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("TenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonPropertyName("Model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("Endpoint")]
        public string Endpoint { get; set; } = string.Empty;

        [JsonPropertyName("ApiFormat")]
        public string ApiFormat { get; set; } = "Ollama";

        [JsonPropertyName("ApiKey")]
        public string? ApiKey { get; set; }

        [JsonPropertyName("Active")]
        public bool Active { get; set; } = true;

        [JsonPropertyName("Labels")]
        public List<string>? Labels { get; set; }

        [JsonPropertyName("Tags")]
        public Dictionary<string, string>? Tags { get; set; }

        [JsonPropertyName("CreatedUtc")]
        public DateTime CreatedUtc { get; set; }

        [JsonPropertyName("LastUpdateUtc")]
        public DateTime LastUpdateUtc { get; set; }
    }
}
