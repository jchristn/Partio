namespace Partio.Core.Models
{
    using Partio.Core.Enums;

    /// <summary>
    /// Request body for chunking a semantic cell into text chunks WITHOUT embedding them. Unlike
    /// <see cref="SemanticCellRequest"/> this requires no embedding endpoint: chunking uses a built-in
    /// tokenizer so token budgets are honored independently of any configured provider.
    /// </summary>
    public class ChunkRequest
    {
        /// <summary>
        /// Unique identifier for this cell (auto-generated if not supplied). Echoed on each produced chunk.
        /// </summary>
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Type of the semantic atom being chunked.
        /// </summary>
        public AtomTypeEnum Type { get; set; } = AtomTypeEnum.Text;

        /// <summary>
        /// Text content (for Text/Code/Hyperlink/Meta atom types).
        /// </summary>
        public string? Text { get; set; } = null;

        /// <summary>
        /// Unordered list content (for List atom type).
        /// </summary>
        public List<string>? UnorderedList { get; set; } = null;

        /// <summary>
        /// Ordered list content (for List atom type).
        /// </summary>
        public List<string>? OrderedList { get; set; } = null;

        /// <summary>
        /// Table content as a list of rows (each row is a list of cell values).
        /// </summary>
        public List<List<string>>? Table { get; set; } = null;

        /// <summary>
        /// Binary content (chunked via its text, when present).
        /// </summary>
        public byte[]? Binary { get; set; } = null;

        /// <summary>
        /// Chunking configuration (strategy, token budget, overlap, context prefix, etc.).
        /// </summary>
        public ChunkingConfiguration ChunkingConfiguration { get; set; } = new ChunkingConfiguration();

        /// <summary>
        /// Labels to echo on each produced chunk.
        /// </summary>
        public List<string>? Labels { get; set; } = null;

        /// <summary>
        /// Tags to echo on each produced chunk.
        /// </summary>
        public Dictionary<string, string>? Tags { get; set; } = null;
    }
}
