namespace Partio.Core.Models
{
    using Partio.Core.Enums;

    /// <summary>
    /// Response body for a chunk-only request: the produced text chunks (no embeddings).
    /// </summary>
    public class ChunkResponse
    {
        /// <summary>
        /// Unique identifier of the chunked cell.
        /// </summary>
        public Guid GUID { get; set; } = Guid.Empty;

        /// <summary>
        /// Type of the chunked semantic atom.
        /// </summary>
        public AtomTypeEnum Type { get; set; } = AtomTypeEnum.Text;

        /// <summary>
        /// Original input text (when applicable).
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Produced chunks, each carrying its text and echoed labels/tags but no embedding vector.
        /// </summary>
        public List<ChunkResult> Chunks { get; set; } = new List<ChunkResult>();

        /// <summary>
        /// Number of chunks produced.
        /// </summary>
        public int Count { get; set; } = 0;
    }
}
