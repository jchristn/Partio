namespace Partio.Core.Settings
{
    using Partio.Core.Enums;
    using Partio.Core.Models;

    /// <summary>
    /// Server-wide tokenization defaults and fallback behavior.
    /// </summary>
    public class TokenizationDefaultsSettings
    {
        private int _CapabilityCacheTtlSeconds = 300;

        /// <summary>
        /// Required final fallback profile when endpoint-specific tokenization cannot be resolved.
        /// </summary>
        public EndpointTokenizationSettings GlobalFallback { get; set; } = new EndpointTokenizationSettings
        {
            TokenizerKind = TokenizerKindEnum.Cl100kBase,
            TokenizerModel = "cl100k_base",
            MaxInputTokens = 8192,
            ReservedInputTokens = 0,
            BatchLimitMode = BatchLimitModeEnum.PerInput,
            AutoDetect = true
        };

        /// <summary>
        /// Provider defaults for OpenAI-compatible embedding endpoints.
        /// </summary>
        public EndpointTokenizationSettings OpenAI { get; set; } = new EndpointTokenizationSettings
        {
            TokenizerKind = TokenizerKindEnum.Cl100kBase,
            TokenizerModel = "cl100k_base",
            MaxInputTokens = 8192,
            ReservedInputTokens = 0,
            BatchLimitMode = BatchLimitModeEnum.PerInput,
            AutoDetect = true
        };

        /// <summary>
        /// Provider defaults for vLLM endpoints when no endpoint-specific override exists.
        /// </summary>
        public EndpointTokenizationSettings vLLM { get; set; } = new EndpointTokenizationSettings
        {
            TokenizerKind = TokenizerKindEnum.Cl100kBase,
            TokenizerModel = "cl100k_base",
            MaxInputTokens = 8192,
            ReservedInputTokens = 0,
            BatchLimitMode = BatchLimitModeEnum.PerInput,
            AutoDetect = true
        };

        /// <summary>
        /// Provider defaults for Gemini embedding endpoints.
        /// </summary>
        public EndpointTokenizationSettings Gemini { get; set; } = new EndpointTokenizationSettings
        {
            TokenizerKind = TokenizerKindEnum.Cl100kBase,
            TokenizerModel = "cl100k_base",
            MaxInputTokens = 2048,
            ReservedInputTokens = 0,
            BatchLimitMode = BatchLimitModeEnum.PerInput,
            AutoDetect = true
        };

        /// <summary>
        /// Optional baseline defaults for Ollama endpoints.
        /// </summary>
        public EndpointTokenizationSettings? Ollama { get; set; } = null;

        /// <summary>
        /// Lifetime of in-memory capability cache entries.
        /// </summary>
        public int CapabilityCacheTtlSeconds
        {
            get => _CapabilityCacheTtlSeconds;
            set => _CapabilityCacheTtlSeconds = value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(CapabilityCacheTtlSeconds), "CapabilityCacheTtlSeconds must be at least 0.");
        }
    }
}
