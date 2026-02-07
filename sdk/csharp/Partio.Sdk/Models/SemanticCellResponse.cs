namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class ChunkResult
    {
        [JsonPropertyName("Text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("Labels")]
        public List<string>? Labels { get; set; }

        [JsonPropertyName("Tags")]
        public Dictionary<string, string>? Tags { get; set; }

        [JsonPropertyName("Embeddings")]
        public List<float>? Embeddings { get; set; }
    }

    public class SemanticCellResponse
    {
        [JsonPropertyName("Text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("Chunks")]
        public List<ChunkResult> Chunks { get; set; } = new List<ChunkResult>();
    }
}
