namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class ChunkResult
    {
        [JsonPropertyName("Text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("ChunkedText")]
        public string ChunkedText { get; set; } = string.Empty;

        [JsonPropertyName("Embeddings")]
        public List<float>? Embeddings { get; set; }
    }

    public class SemanticCellResponse
    {
        [JsonPropertyName("Cells")]
        public int Cells { get; set; }

        [JsonPropertyName("TotalChunks")]
        public int TotalChunks { get; set; }

        [JsonPropertyName("Chunks")]
        public List<ChunkResult> Chunks { get; set; } = new List<ChunkResult>();

        [JsonPropertyName("Labels")]
        public List<string>? Labels { get; set; }

        [JsonPropertyName("Tags")]
        public Dictionary<string, string>? Tags { get; set; }
    }
}
