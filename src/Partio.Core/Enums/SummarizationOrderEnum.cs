namespace Partio.Core.Enums
{
    /// <summary>
    /// Order in which summarization processes cells in the hierarchy.
    /// </summary>
    public enum SummarizationOrderEnum
    {
        /// <summary>Process from root to leaves, using parent context.</summary>
        TopDown,
        /// <summary>Process from leaves to root, using child context.</summary>
        BottomUp
    }
}
