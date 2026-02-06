namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class RequestHistoryEntry
    {
        [JsonPropertyName("Id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("TenantId")]
        public string? TenantId { get; set; }

        [JsonPropertyName("UserId")]
        public string? UserId { get; set; }

        [JsonPropertyName("CredentialId")]
        public string? CredentialId { get; set; }

        [JsonPropertyName("RequestorIp")]
        public string? RequestorIp { get; set; }

        [JsonPropertyName("HttpMethod")]
        public string? HttpMethod { get; set; }

        [JsonPropertyName("HttpUrl")]
        public string? HttpUrl { get; set; }

        [JsonPropertyName("RequestBodyLength")]
        public long? RequestBodyLength { get; set; }

        [JsonPropertyName("ResponseBodyLength")]
        public long? ResponseBodyLength { get; set; }

        [JsonPropertyName("HttpStatus")]
        public int? HttpStatus { get; set; }

        [JsonPropertyName("ResponseTimeMs")]
        public long? ResponseTimeMs { get; set; }

        [JsonPropertyName("ObjectKey")]
        public string? ObjectKey { get; set; }

        [JsonPropertyName("CreatedUtc")]
        public DateTime CreatedUtc { get; set; }

        [JsonPropertyName("CompletedUtc")]
        public DateTime? CompletedUtc { get; set; }
    }
}
