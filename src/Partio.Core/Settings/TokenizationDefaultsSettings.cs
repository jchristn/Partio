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
        /// Provider defaults for xAI embedding endpoints.
        /// </summary>
        public EndpointTokenizationSettings xAI { get; set; } = new EndpointTokenizationSettings
        {
            TokenizerKind = TokenizerKindEnum.Cl100kBase,
            TokenizerModel = "cl100k_base",
            MaxInputTokens = 8192,
            ReservedInputTokens = 0,
            BatchLimitMode = BatchLimitModeEnum.PerInput,
            AutoDetect = true
        };

        /// <summary>
        /// Provider defaults for Azure OpenAI embedding endpoints.
        /// </summary>
        public EndpointTokenizationSettings AzureOpenAI { get; set; } = new EndpointTokenizationSettings
        {
            TokenizerKind = TokenizerKindEnum.Cl100kBase,
            TokenizerModel = "cl100k_base",
            MaxInputTokens = 8192,
            ReservedInputTokens = 0,
            BatchLimitMode = BatchLimitModeEnum.PerInput,
            AutoDetect = true
        };

        /// <summary>
        /// Provider defaults for Cohere embedding endpoints (embed-v3 models accept 512 tokens per input).
        /// </summary>
        public EndpointTokenizationSettings Cohere { get; set; } = new EndpointTokenizationSettings
        {
            TokenizerKind = TokenizerKindEnum.Cl100kBase,
            TokenizerModel = "cl100k_base",
            MaxInputTokens = 512,
            ReservedInputTokens = 0,
            BatchLimitMode = BatchLimitModeEnum.PerInput,
            AutoDetect = true
        };

        /// <summary>
        /// Provider defaults for AWS Bedrock embedding endpoints (Titan Text Embeddings v2 accepts 8192 tokens).
        /// </summary>
        public EndpointTokenizationSettings Bedrock { get; set; } = new EndpointTokenizationSettings
        {
            TokenizerKind = TokenizerKindEnum.Cl100kBase,
            TokenizerModel = "cl100k_base",
            MaxInputTokens = 8192,
            ReservedInputTokens = 0,
            BatchLimitMode = BatchLimitModeEnum.PerInput,
            AutoDetect = true
        };

        /// <summary>
        /// Provider defaults for Google Vertex AI embedding endpoints (text-embedding-005 accepts 2048 tokens).
        /// </summary>
        public EndpointTokenizationSettings VertexAI { get; set; } = new EndpointTokenizationSettings
        {
            TokenizerKind = TokenizerKindEnum.Cl100kBase,
            TokenizerModel = "cl100k_base",
            MaxInputTokens = 2048,
            ReservedInputTokens = 0,
            BatchLimitMode = BatchLimitModeEnum.PerInput,
            AutoDetect = true
        };

        /// <summary>
        /// Provider defaults for Hugging Face TEI embedding endpoints (BERT-family models commonly accept 512 tokens).
        /// </summary>
        public EndpointTokenizationSettings TEI { get; set; } = new EndpointTokenizationSettings
        {
            TokenizerKind = TokenizerKindEnum.Cl100kBase,
            TokenizerModel = "cl100k_base",
            MaxInputTokens = 512,
            ReservedInputTokens = 0,
            BatchLimitMode = BatchLimitModeEnum.PerInput,
            AutoDetect = true
        };

        /// <summary>
        /// Provider defaults for VoyageAI embedding endpoints (voyage-3.5 accepts 32000 tokens).
        /// </summary>
        public EndpointTokenizationSettings VoyageAI { get; set; } = new EndpointTokenizationSettings
        {
            TokenizerKind = TokenizerKindEnum.Cl100kBase,
            TokenizerModel = "cl100k_base",
            MaxInputTokens = 32000,
            ReservedInputTokens = 0,
            BatchLimitMode = BatchLimitModeEnum.PerInput,
            AutoDetect = true
        };

        /// <summary>
        /// Optional baseline defaults for Ollama and Ollama Cloud endpoints.
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
