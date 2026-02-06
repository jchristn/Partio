namespace Partio.Core.Chunking
{
    using Partio.Core.Models;
    using SharpToken;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Splits text at sentence boundaries, grouping sentences to fill a token budget.
    /// </summary>
    public static class SentenceChunker
    {
        private static readonly Regex _SentencePattern = new Regex(
            @"(?<=[.!?])\s+",
            RegexOptions.Compiled);

        /// <summary>
        /// Chunk text by sentence boundaries.
        /// </summary>
        /// <param name="text">Input text to chunk.</param>
        /// <param name="config">Chunking configuration.</param>
        /// <param name="encoding">Token encoding.</param>
        /// <returns>List of chunk text strings.</returns>
        public static List<string> Chunk(string text, ChunkingConfiguration config, GptEncoding encoding)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();

            string[] sentences = _SentencePattern.Split(text);
            sentences = sentences.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

            if (sentences.Length == 0) return new List<string> { text };

            int tokenLimit = config.FixedTokenCount;
            int overlapSentences = GetOverlapSentenceCount(config, sentences, encoding, tokenLimit);

            List<string> chunks = new List<string>();
            int sentenceIndex = 0;

            while (sentenceIndex < sentences.Length)
            {
                List<string> currentSentences = new List<string>();
                int currentTokens = 0;

                while (sentenceIndex < sentences.Length)
                {
                    string sentence = sentences[sentenceIndex];
                    int sentenceTokens = encoding.Encode(sentence).Count;

                    if (currentTokens + sentenceTokens > tokenLimit && currentSentences.Count > 0)
                        break;

                    currentSentences.Add(sentence);
                    currentTokens += sentenceTokens;
                    sentenceIndex++;
                }

                chunks.Add(string.Join(" ", currentSentences));

                if (overlapSentences > 0 && sentenceIndex < sentences.Length)
                {
                    sentenceIndex -= Math.Min(overlapSentences, currentSentences.Count - 1);
                    if (sentenceIndex < 0) sentenceIndex = 0;
                }
            }

            return chunks;
        }

        private static int GetOverlapSentenceCount(ChunkingConfiguration config, string[] sentences, GptEncoding encoding, int tokenLimit)
        {
            if (config.OverlapPercentage.HasValue)
                return Math.Max(1, (int)(sentences.Length * config.OverlapPercentage.Value / sentences.Length));
            return config.OverlapCount;
        }
    }
}
