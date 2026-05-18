namespace Partio.Core.Models
{
    using Partio.Core.Enums;

    /// <summary>
    /// Optional endpoint or settings-level tokenization configuration.
    /// </summary>
    public class EndpointTokenizationSettings
    {
        private int? _MaxInputTokens = null;
        private int? _ReservedInputTokens = null;
        private int? _EffectiveInputBudget = null;

        /// <summary>
        /// Tokenizer family to use for counting and chunk slicing.
        /// </summary>
        public TokenizerKindEnum? TokenizerKind { get; set; }

        /// <summary>
        /// Tokenizer model or vocabulary identifier.
        /// </summary>
        public string? TokenizerModel { get; set; }

        /// <summary>
        /// Maximum number of input tokens accepted by the upstream embedding model.
        /// </summary>
        public int? MaxInputTokens
        {
            get => _MaxInputTokens;
            set => _MaxInputTokens = !value.HasValue || value.Value >= 1
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MaxInputTokens), "MaxInputTokens must be at least 1 when supplied.");
        }

        /// <summary>
        /// Tokens reserved before chunking begins.
        /// </summary>
        public int? ReservedInputTokens
        {
            get => _ReservedInputTokens;
            set => _ReservedInputTokens = !value.HasValue || value.Value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(ReservedInputTokens), "ReservedInputTokens must be at least 0 when supplied.");
        }

        /// <summary>
        /// Effective token budget available for content after provider-specific overhead is applied.
        /// </summary>
        public int? EffectiveInputBudget
        {
            get => _EffectiveInputBudget;
            set => _EffectiveInputBudget = !value.HasValue || value.Value >= 1
                ? value
                : throw new ArgumentOutOfRangeException(nameof(EffectiveInputBudget), "EffectiveInputBudget must be at least 1 when supplied.");
        }

        /// <summary>
        /// Indicates how the upstream endpoint applies token limits to batched inputs.
        /// </summary>
        public BatchLimitModeEnum? BatchLimitMode { get; set; }

        /// <summary>
        /// Whether Partio should continue resolving missing fields dynamically.
        /// </summary>
        public bool AutoDetect { get; set; } = true;
    }
}
