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

        [JsonPropertyName("EnableRequestHistory")]
        public bool EnableRequestHistory { get; set; } = true;

        [JsonPropertyName("HealthCheckEnabled")]
        public bool HealthCheckEnabled { get; set; } = false;

        [JsonPropertyName("HealthCheckUrl")]
        public string? HealthCheckUrl { get; set; }

        [JsonPropertyName("HealthCheckMethod")]
        public string HealthCheckMethod { get; set; } = "GET";

        [JsonPropertyName("HealthCheckIntervalMs")]
        public int HealthCheckIntervalMs { get; set; } = 5000;

        [JsonPropertyName("HealthCheckTimeoutMs")]
        public int HealthCheckTimeoutMs { get; set; } = 2000;

        [JsonPropertyName("HealthCheckExpectedStatusCode")]
        public int HealthCheckExpectedStatusCode { get; set; } = 200;

        [JsonPropertyName("HealthyThreshold")]
        public int HealthyThreshold { get; set; } = 3;

        [JsonPropertyName("UnhealthyThreshold")]
        public int UnhealthyThreshold { get; set; } = 3;

        [JsonPropertyName("HealthCheckUseAuth")]
        public bool HealthCheckUseAuth { get; set; } = false;

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
