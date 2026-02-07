namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class WhoAmIResponse
    {
        [JsonPropertyName("Role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("TenantName")]
        public string TenantName { get; set; } = string.Empty;
    }
}
