namespace Partio.Core.Models
{
    using Partio.Core.Enums;

    /// <summary>
    /// Provider/model capability data discovered at runtime.
    /// </summary>
    public class EmbeddingModelCapabilities
    {
        /// <summary>
        /// Optional source hint describing how these capabilities were obtained.
        /// </summary>
        public TokenizationProfileSourceEnum? SourceHint { get; set; }

        /// <summary>
        /// Tokenizer family, if known.
        /// </summary>
        public TokenizerKindEnum? TokenizerKind { get; set; }

        /// <summary>
        /// Tokenizer model or vocabulary identifier, if known.
        /// </summary>
        public string? TokenizerModel { get; set; }

        /// <summary>
        /// Maximum supported input tokens, if known.
        /// </summary>
        public int? MaxInputTokens { get; set; }

        /// <summary>
        /// Tokens reserved before chunking begins, if known.
        /// </summary>
        public int? ReservedInputTokens { get; set; }

        /// <summary>
        /// Effective token budget available to content after provider overhead is applied, if known.
        /// </summary>
        public int? EffectiveInputBudget { get; set; }

        /// <summary>
        /// Batch limit behavior, if known.
        /// </summary>
        public BatchLimitModeEnum? BatchLimitMode { get; set; }

        /// <summary>
        /// Provider-specific metadata captured from the probe path.
        /// </summary>
        public Dictionary<string, string> ProviderMetadata { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
