namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request body for chunking a semantic cell into text chunks without embedding them.
    /// Requires no embedding endpoint.
    /// </summary>
    public class ChunkRequest
    {
        [JsonPropertyName("GUID")]
        public Guid GUID { get; set; } = Guid.NewGuid();

        [JsonPropertyName("Type")]
        public string Type { get; set; } = "Text";

        [JsonPropertyName("Text")]
        public string? Text { get; set; }

        [JsonPropertyName("UnorderedList")]
        public List<string>? UnorderedList { get; set; }

        [JsonPropertyName("OrderedList")]
        public List<string>? OrderedList { get; set; }

        [JsonPropertyName("Table")]
        public List<List<string>>? Table { get; set; }

        [JsonPropertyName("Binary")]
        public byte[]? Binary { get; set; }

        [JsonPropertyName("ChunkingConfiguration")]
        public ChunkingConfiguration ChunkingConfiguration { get; set; } = new ChunkingConfiguration();

        [JsonPropertyName("Labels")]
        public List<string>? Labels { get; set; }

        [JsonPropertyName("Tags")]
        public Dictionary<string, string>? Tags { get; set; }
    }
}
