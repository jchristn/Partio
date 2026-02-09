namespace Partio.Core.Chunking
{
    using Partio.Core.Models;
    using SharpToken;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Splits text at boundaries defined by a user-supplied regular expression,
    /// then groups segments to fill a token budget.
    /// </summary>
    public static class RegexChunker
    {
        /// <summary>
        /// Chunk text by regex-defined boundaries.
        /// </summary>
        public static List<string> Chunk(
            string text,
            ChunkingConfiguration config,
            GptEncoding encoding)
        {
            // 1. Validate
            if (string.IsNullOrEmpty(text)) return new List<string>();
            if (string.IsNullOrEmpty(config.RegexPattern))
                throw new ArgumentException("RegexPattern is required when using RegexBased strategy.");

            // 2. Compile with Multiline (so ^ and $ match line boundaries) and a timeout
            Regex regex = new Regex(
                config.RegexPattern,
                RegexOptions.Compiled | RegexOptions.Multiline,
                TimeSpan.FromSeconds(5));

            // 3. Split and discard empty segments
            string[] segments = regex.Split(text);
            List<string> filtered = segments
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (filtered.Count == 0) return new List<string> { text };

            // 4. Each regex-defined segment becomes its own chunk
            return filtered;
        }
    }
}
