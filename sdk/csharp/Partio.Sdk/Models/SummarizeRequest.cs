namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request body for summarizing text through a completion endpoint, without chunking or embedding.
    /// </summary>
    public class SummarizeRequest
    {
        [JsonPropertyName("Text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("SummarizationConfiguration")]
        public SummarizationConfiguration SummarizationConfiguration { get; set; } = new SummarizationConfiguration();
    }
}
