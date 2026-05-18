namespace Partio.Core.Models
{
    /// <summary>
    /// Records per-chunk diagnostics used for request-history troubleshooting.
    /// </summary>
    public class ChunkProcessingDiagnostic
    {
        /// <summary>
        /// GUID of the semantic cell that produced the chunk.
        /// </summary>
        public Guid CellGuid { get; set; } = Guid.Empty;

        /// <summary>
        /// Zero-based chunk index within the cell.
        /// </summary>
        public int ChunkIndex { get; set; } = 0;

        /// <summary>
        /// Number of characters in the chunk text before any context prefix is applied.
        /// </summary>
        public int ChunkCharacterCount { get; set; } = 0;

        /// <summary>
        /// Token count for the chunk text before any context prefix is applied.
        /// </summary>
        public int ChunkTokenCount { get; set; } = 0;

        /// <summary>
        /// Number of characters actually sent upstream after the context prefix is applied.
        /// </summary>
        public int EmbeddingCharacterCount { get; set; } = 0;

        /// <summary>
        /// Token count actually sent upstream after the context prefix is applied.
        /// </summary>
        public int EmbeddingTokenCount { get; set; } = 0;

        /// <summary>
        /// True when the embeddable text exceeds the active per-input budget.
        /// </summary>
        public bool ExceedsEffectiveInputBudget { get; set; } = false;

        /// <summary>
        /// Short preview used in request-history diagnostics.
        /// </summary>
        public string? Preview { get; set; }
    }
}
