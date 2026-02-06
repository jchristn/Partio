namespace Partio.Core.Enums
{
    /// <summary>
    /// Chunking strategy for splitting content.
    /// </summary>
    public enum ChunkStrategyEnum
    {
        /// <summary>Split into fixed token count chunks.</summary>
        FixedTokenCount,
        /// <summary>Split at sentence boundaries.</summary>
        SentenceBased,
        /// <summary>Split at paragraph boundaries.</summary>
        ParagraphBased,
        /// <summary>Treat entire list as a single chunk.</summary>
        WholeList,
        /// <summary>Each list entry becomes its own chunk.</summary>
        ListEntry
    }
}
