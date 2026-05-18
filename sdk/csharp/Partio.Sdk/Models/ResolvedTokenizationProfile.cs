namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class ResolvedTokenizationProfile
    {
        [JsonPropertyName("TokenizerKind")]
        public string? TokenizerKind { get; set; }

        [JsonPropertyName("TokenizerModel")]
        public string? TokenizerModel { get; set; }

        [JsonPropertyName("MaxInputTokens")]
        public int MaxInputTokens { get; set; }

        [JsonPropertyName("ReservedInputTokens")]
        public int ReservedInputTokens { get; set; }

        [JsonPropertyName("EffectiveInputBudget")]
        public int EffectiveInputBudget { get; set; }

        [JsonPropertyName("BatchLimitMode")]
        public string? BatchLimitMode { get; set; }

        [JsonPropertyName("ProfileSource")]
        public string? ProfileSource { get; set; }

        [JsonPropertyName("UsedFallback")]
        public bool UsedFallback { get; set; }

        [JsonPropertyName("ProviderMetadata")]
        public Dictionary<string, string>? ProviderMetadata { get; set; }
    }
}
