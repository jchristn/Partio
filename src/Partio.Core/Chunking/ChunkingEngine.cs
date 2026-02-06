namespace Partio.Core.Chunking
{
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using SharpToken;
    using SyslogLogging;

    /// <summary>
    /// Factory/dispatcher for chunking operations across different strategies and atom types.
    /// </summary>
    public class ChunkingEngine
    {
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[ChunkingEngine] ";
        private readonly GptEncoding _Encoding;

        /// <summary>
        /// Initialize a new ChunkingEngine.
        /// </summary>
        /// <param name="logging">Logging module.</param>
        public ChunkingEngine(LoggingModule logging)
        {
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Encoding = GptEncoding.GetEncoding("cl100k_base");
        }

        /// <summary>
        /// Chunk a semantic cell request into text chunks (before embedding).
        /// </summary>
        /// <param name="request">Semantic cell request.</param>
        /// <returns>List of chunk results with text but no embeddings yet.</returns>
        public List<ChunkResult> Chunk(SemanticCellRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            List<string> rawChunks = GetRawChunks(request);
            string? contextPrefix = request.ChunkingConfiguration.ContextPrefix;

            List<ChunkResult> results = new List<ChunkResult>();

            foreach (string chunk in rawChunks)
            {
                string chunkedText = string.IsNullOrEmpty(contextPrefix) ? chunk : contextPrefix + chunk;

                ChunkResult result = new ChunkResult();
                result.Text = chunk;
                result.ChunkedText = chunkedText;
                results.Add(result);
            }

            return results;
        }

        private List<string> GetRawChunks(SemanticCellRequest request)
        {
            ChunkingConfiguration config = request.ChunkingConfiguration;

            switch (request.Type)
            {
                case AtomTypeEnum.Text:
                case AtomTypeEnum.Code:
                case AtomTypeEnum.Hyperlink:
                case AtomTypeEnum.Meta:
                    return ChunkText(request.Text ?? string.Empty, config);

                case AtomTypeEnum.List:
                    return ChunkList(request, config);

                case AtomTypeEnum.Table:
                    return ChunkTable(request, config);

                case AtomTypeEnum.Binary:
                case AtomTypeEnum.Image:
                    if (!string.IsNullOrEmpty(request.Text))
                        return ChunkText(request.Text, config);
                    return new List<string>();

                case AtomTypeEnum.Unknown:
                default:
                    if (!string.IsNullOrEmpty(request.Text))
                        return ChunkText(request.Text, config);
                    return new List<string>();
            }
        }

        private List<string> ChunkText(string text, ChunkingConfiguration config)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();

            switch (config.Strategy)
            {
                case ChunkStrategyEnum.FixedTokenCount:
                    return FixedTokenChunker.Chunk(text, config, _Encoding);

                case ChunkStrategyEnum.SentenceBased:
                    return SentenceChunker.Chunk(text, config, _Encoding);

                case ChunkStrategyEnum.ParagraphBased:
                    return ParagraphChunker.Chunk(text, config, _Encoding);

                case ChunkStrategyEnum.WholeList:
                    return new List<string> { text };

                case ChunkStrategyEnum.ListEntry:
                    return text.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

                default:
                    return FixedTokenChunker.Chunk(text, config, _Encoding);
            }
        }

        private List<string> ChunkList(SemanticCellRequest request, ChunkingConfiguration config)
        {
            List<string>? items = request.OrderedList ?? request.UnorderedList;
            if (items == null || items.Count == 0) return new List<string>();

            bool ordered = request.OrderedList != null;

            switch (config.Strategy)
            {
                case ChunkStrategyEnum.WholeList:
                    return WholeListChunker.Chunk(items, ordered);

                case ChunkStrategyEnum.ListEntry:
                    return ListEntryChunker.Chunk(items);

                default:
                    string serialized = SerializeList(items, ordered);
                    return ChunkText(serialized, config);
            }
        }

        private List<string> ChunkTable(SemanticCellRequest request, ChunkingConfiguration config)
        {
            if (request.Table == null || request.Table.Count == 0) return new List<string>();

            string serialized = SerializeTable(request.Table);
            return ChunkText(serialized, config);
        }

        private string SerializeList(List<string> items, bool ordered)
        {
            List<string> lines = new List<string>();
            for (int i = 0; i < items.Count; i++)
            {
                if (ordered)
                    lines.Add($"{i + 1}. {items[i]}");
                else
                    lines.Add($"- {items[i]}");
            }
            return string.Join("\n", lines);
        }

        private string SerializeTable(List<List<string>> table)
        {
            List<string> lines = new List<string>();
            foreach (List<string> row in table)
            {
                lines.Add(string.Join(" | ", row));
            }
            return string.Join("\n", lines);
        }
    }
}
