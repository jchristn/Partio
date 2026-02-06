namespace Partio.Core.Chunking
{
    /// <summary>
    /// Treats the entire list as a single chunk.
    /// </summary>
    public static class WholeListChunker
    {
        /// <summary>
        /// Serialize an entire list into a single chunk.
        /// </summary>
        /// <param name="items">List items.</param>
        /// <param name="ordered">Whether the list is ordered (numbered) or unordered (bulleted).</param>
        /// <returns>List containing a single chunk text string.</returns>
        public static List<string> Chunk(List<string> items, bool ordered)
        {
            if (items == null || items.Count == 0) return new List<string>();

            List<string> lines = new List<string>();
            for (int i = 0; i < items.Count; i++)
            {
                if (ordered)
                    lines.Add($"{i + 1}. {items[i]}");
                else
                    lines.Add($"- {items[i]}");
            }

            return new List<string> { string.Join("\n", lines) };
        }
    }
}
