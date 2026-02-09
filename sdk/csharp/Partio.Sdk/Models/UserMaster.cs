namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class UserMaster
    {
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        [JsonPropertyName("TenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonPropertyName("Email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("PasswordSha256")]
        public string? PasswordSha256 { get; set; }

        [JsonPropertyName("FirstName")]
        public string? FirstName { get; set; }

        [JsonPropertyName("LastName")]
        public string? LastName { get; set; }

        [JsonPropertyName("IsAdmin")]
        public bool IsAdmin { get; set; }

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

        [JsonPropertyName("Password")]
        public string? Password { get; set; }
    }
}
