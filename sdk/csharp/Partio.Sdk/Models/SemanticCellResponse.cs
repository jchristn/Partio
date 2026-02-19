namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class ChunkResult
    {
        [JsonPropertyName("CellGUID")]
        public Guid CellGUID { get; set; }

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
        [JsonPropertyName("GUID")]
        public Guid GUID { get; set; }

        [JsonPropertyName("ParentGUID")]
        public Guid? ParentGUID { get; set; }

        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        [JsonPropertyName("Text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("Chunks")]
        public List<ChunkResult> Chunks { get; set; } = new List<ChunkResult>();

        [JsonPropertyName("Children")]
        public List<SemanticCellResponse>? Children { get; set; }
    }
}
