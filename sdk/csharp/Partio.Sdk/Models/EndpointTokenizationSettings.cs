namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class EndpointTokenizationSettings
    {
        [JsonPropertyName("TokenizerKind")]
        public string? TokenizerKind { get; set; }

        [JsonPropertyName("TokenizerModel")]
        public string? TokenizerModel { get; set; }

        [JsonPropertyName("MaxInputTokens")]
        public int? MaxInputTokens { get; set; }

        [JsonPropertyName("ReservedInputTokens")]
        public int? ReservedInputTokens { get; set; }

        [JsonPropertyName("BatchLimitMode")]
        public string? BatchLimitMode { get; set; }

        [JsonPropertyName("AutoDetect")]
        public bool AutoDetect { get; set; } = true;
    }
}
