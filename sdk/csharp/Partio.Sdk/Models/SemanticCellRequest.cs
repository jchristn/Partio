namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Valid Strategy values: FixedTokenCount, SentenceBased, ParagraphBased, RegexBased,
    /// WholeList, ListEntry, Row, RowWithHeaders, RowGroupWithHeaders, KeyValuePairs, WholeTable.
    /// </summary>
    public class ChunkingConfiguration
    {
        [JsonPropertyName("Strategy")]
        public string Strategy { get; set; } = "FixedTokenCount";

        [JsonPropertyName("FixedTokenCount")]
        public int FixedTokenCount { get; set; } = 256;

        [JsonPropertyName("OverlapCount")]
        public int OverlapCount { get; set; } = 0;

        [JsonPropertyName("OverlapPercentage")]
        public double? OverlapPercentage { get; set; }

        [JsonPropertyName("OverlapStrategy")]
        public string OverlapStrategy { get; set; } = "SlidingWindow";

        [JsonPropertyName("ContextPrefix")]
        public string? ContextPrefix { get; set; }

        [JsonPropertyName("RowGroupSize")]
        public int RowGroupSize { get; set; } = 5;

        [JsonPropertyName("RegexPattern")]
        public string? RegexPattern { get; set; }
    }

    public class EmbeddingConfiguration
    {
        [JsonPropertyName("EmbeddingEndpointId")]
        public string? EmbeddingEndpointId { get; set; }

        [JsonPropertyName("Model")]
        public string? Model { get; set; } = null;

        [JsonPropertyName("L2Normalization")]
        public bool L2Normalization { get; set; } = false;
    }

    public class SemanticCellRequest
    {
        [JsonPropertyName("GUID")]
        public Guid GUID { get; set; } = Guid.NewGuid();

        [JsonPropertyName("ParentGUID")]
        public Guid? ParentGUID { get; set; }

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

        [JsonPropertyName("Children")]
        public List<SemanticCellRequest>? Children { get; set; }

        [JsonPropertyName("ChunkingConfiguration")]
        public ChunkingConfiguration ChunkingConfiguration { get; set; } = new ChunkingConfiguration();

        [JsonPropertyName("EmbeddingConfiguration")]
        public EmbeddingConfiguration EmbeddingConfiguration { get; set; } = new EmbeddingConfiguration();

        [JsonPropertyName("SummarizationConfiguration")]
        public SummarizationConfiguration? SummarizationConfiguration { get; set; }

        [JsonPropertyName("Labels")]
        public List<string>? Labels { get; set; }

        [JsonPropertyName("Tags")]
        public Dictionary<string, string>? Tags { get; set; }
    }
}
