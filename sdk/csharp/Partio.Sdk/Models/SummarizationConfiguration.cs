namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class SummarizationConfiguration
    {
        [JsonPropertyName("CompletionEndpointId")]
        public string CompletionEndpointId { get; set; } = string.Empty;

        [JsonPropertyName("Order")]
        public string Order { get; set; } = "BottomUp";

        [JsonPropertyName("SummarizationPrompt")]
        public string? SummarizationPrompt { get; set; }

        [JsonPropertyName("MaxSummaryTokens")]
        public int MaxSummaryTokens { get; set; } = 1024;

        [JsonPropertyName("MinCellLength")]
        public int MinCellLength { get; set; } = 0;

        [JsonPropertyName("MaxParallelTasks")]
        public int MaxParallelTasks { get; set; } = 4;

        [JsonPropertyName("MaxRetriesPerSummary")]
        public int MaxRetriesPerSummary { get; set; } = 2;

        [JsonPropertyName("MaxRetries")]
        public int MaxRetries { get; set; } = 10;

        [JsonPropertyName("TimeoutMs")]
        public int TimeoutMs { get; set; } = 30000;
    }
}
