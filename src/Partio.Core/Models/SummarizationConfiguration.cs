namespace Partio.Core.Models
{
    using Partio.Core.Enums;

    /// <summary>
    /// Configuration for the summarization pipeline step.
    /// When present on a request, summarization occurs before chunking and embedding.
    /// </summary>
    public class SummarizationConfiguration
    {
        private string _CompletionEndpointId = string.Empty;
        private SummarizationOrderEnum _Order = SummarizationOrderEnum.BottomUp;
        private string _SummarizationPrompt = DefaultPrompt;
        private int _MaxSummaryTokens = 1024;
        private int _MinCellLength = 128;
        private int _MaxParallelTasks = 4;
        private int _MaxRetriesPerSummary = 3;
        private int _MaxRetries = 9;
        private int _TimeoutMs = 30000;

        /// <summary>
        /// Default summarization prompt template.
        /// Supports {tokens}, {content}, and {context} placeholders.
        /// </summary>
        public static readonly string DefaultPrompt =
            "You must follow these rules exactly:\n" +
            "1. If the content cannot be summarized (empty, insufficient, non-text, or meaningless), output exactly: None\n" +
            "2. If the content can be summarized, output ONLY the summary text\n" +
            "3. Never include prefixes like 'Summary:', 'Summary text:', or any introductory phrases\n" +
            "4. Never explain why you cannot summarize, in these cases just output exactly: None\n" +
            "5. Maximum length: {tokens} tokens\n\n" +
            "Content to summarize:\n{content}\n\n" +
            "Context information:\n{context}\n\n" +
            "Output:";

        /// <summary>
        /// Completion endpoint ID (required — must reference a valid, active completion endpoint).
        /// </summary>
        public string CompletionEndpointId
        {
            get => _CompletionEndpointId;
            set => _CompletionEndpointId = value ?? throw new ArgumentNullException(nameof(CompletionEndpointId));
        }

        /// <summary>
        /// Order of summarization traversal.
        /// </summary>
        /// <remarks>Default: BottomUp.</remarks>
        public SummarizationOrderEnum Order
        {
            get => _Order;
            set => _Order = value;
        }

        /// <summary>
        /// Prompt template for summarization. Supports {tokens}, {content}, and {context} placeholders.
        /// </summary>
        public string SummarizationPrompt
        {
            get => _SummarizationPrompt;
            set => _SummarizationPrompt = !string.IsNullOrEmpty(value)
                ? value
                : throw new ArgumentException("SummarizationPrompt must not be null or empty.", nameof(SummarizationPrompt));
        }

        /// <summary>
        /// Maximum number of tokens for generated summaries.
        /// </summary>
        /// <remarks>Default: 1024. Minimum: 128.</remarks>
        public int MaxSummaryTokens
        {
            get => _MaxSummaryTokens;
            set => _MaxSummaryTokens = value >= 128
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MaxSummaryTokens), "Must be >= 128.");
        }

        /// <summary>
        /// Minimum content length (in characters) for a cell to be summarized.
        /// Cells with content shorter than this are skipped.
        /// </summary>
        /// <remarks>Default: 128. Minimum: 0.</remarks>
        public int MinCellLength
        {
            get => _MinCellLength;
            set => _MinCellLength = value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MinCellLength), "Must be >= 0.");
        }

        /// <summary>
        /// Maximum number of parallel summarization tasks.
        /// </summary>
        /// <remarks>Default: 4. Minimum: 1.</remarks>
        public int MaxParallelTasks
        {
            get => _MaxParallelTasks;
            set => _MaxParallelTasks = value >= 1
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MaxParallelTasks), "Must be >= 1.");
        }

        /// <summary>
        /// Maximum retry attempts per individual cell summarization.
        /// </summary>
        /// <remarks>Default: 3. Minimum: 0.</remarks>
        public int MaxRetriesPerSummary
        {
            get => _MaxRetriesPerSummary;
            set => _MaxRetriesPerSummary = value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MaxRetriesPerSummary), "Must be >= 0.");
        }

        /// <summary>
        /// Global failure counter across all cells.
        /// When this limit is reached, the entire summarization step fails.
        /// Acts as a circuit breaker to prevent runaway failures.
        /// </summary>
        /// <remarks>Default: 9. Minimum: 0.</remarks>
        public int MaxRetries
        {
            get => _MaxRetries;
            set => _MaxRetries = value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MaxRetries), "Must be >= 0.");
        }

        /// <summary>
        /// Timeout in milliseconds for each completion API call.
        /// </summary>
        /// <remarks>Default: 30000. Minimum: 100.</remarks>
        public int TimeoutMs
        {
            get => _TimeoutMs;
            set => _TimeoutMs = value >= 100
                ? value
                : throw new ArgumentOutOfRangeException(nameof(TimeoutMs), "Must be >= 100.");
        }
    }
}
