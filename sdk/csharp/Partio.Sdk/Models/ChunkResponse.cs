namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Response body for a chunk-only request: the produced text chunks (no embeddings).
    /// </summary>
    public class ChunkResponse
    {
        [JsonPropertyName("GUID")]
        public Guid GUID { get; set; }

        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        [JsonPropertyName("Text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("Chunks")]
        public List<ChunkResult> Chunks { get; set; } = new List<ChunkResult>();

        [JsonPropertyName("Count")]
        public int Count { get; set; }
    }
}
