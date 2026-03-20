namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class EndpointExplorerCompletionRequest
    {
        [JsonPropertyName("EndpointId")]
        public string EndpointId { get; set; } = string.Empty;

        [JsonPropertyName("Prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("SystemPrompt")]
        public string? SystemPrompt { get; set; }

        [JsonPropertyName("MaxTokens")]
        public int MaxTokens { get; set; } = 512;

        [JsonPropertyName("TimeoutMs")]
        public int TimeoutMs { get; set; } = 60000;
    }
}
