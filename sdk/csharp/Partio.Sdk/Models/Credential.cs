namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class Credential
    {
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        [JsonPropertyName("TenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonPropertyName("UserId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("BearerToken")]
        public string? BearerToken { get; set; }

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
