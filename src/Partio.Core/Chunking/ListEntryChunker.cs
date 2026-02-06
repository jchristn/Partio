namespace Partio.Core.Chunking
{
    /// <summary>
    /// Each list item becomes its own chunk.
    /// </summary>
    public static class ListEntryChunker
    {
        /// <summary>
        /// Create one chunk per list item.
        /// </summary>
        /// <param name="items">List items.</param>
        /// <returns>List of chunk text strings, one per item.</returns>
        public static List<string> Chunk(List<string> items)
        {
            if (items == null || items.Count == 0) return new List<string>();
            return items.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        }
    }
}
