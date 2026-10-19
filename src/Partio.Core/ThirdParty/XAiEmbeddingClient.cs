namespace Partio.Core.ThirdParty
{
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using PolyPromptEmbeddingClientBase = PolyPrompt.Clients.EmbeddingClientBase;
    using PolyPromptXAiEmbeddingClient = PolyPrompt.Clients.XAiEmbeddingClient;
    using SyslogLogging;

    /// <summary>
    /// Embedding client for the xAI (Grok) API backed by PolyPrompt.
    /// </summary>
    public class XAiEmbeddingClient : OpenAiEmbeddingClient
    {
        /// <summary>
        /// Initialize a new XAiEmbeddingClient.
        /// </summary>
        /// <param name="endpoint">xAI API root (for example https://api.x.ai).</param>
        /// <param name="apiKey">xAI API key (required).</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        /// <param name="maxQueueDepth">Maximum number of requests allowed to wait for a concurrency slot.</param>
        /// <exception cref="ArgumentException">No API key was supplied.</exception>
        public XAiEmbeddingClient(
            string endpoint,
            string apiKey,
            LoggingModule logging,
            int maximumTimeoutMs,
            string? concurrencyKey = null,
            int maxConcurrentRequests = 2,
            int maxQueueDepth = 0)
            : base(ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.xAI, endpoint), RequireApiKey(apiKey), logging, maximumTimeoutMs, concurrencyKey, maxConcurrentRequests, maxQueueDepth)
        {
            _Header = "[XAiEmbedding] ";
        }

        /// <inheritdoc />
        protected override string ProviderName => "xAI";

        /// <inheritdoc />
        protected override PolyPromptEmbeddingClientBase CreateProviderClient()
        {
            return new PolyPromptXAiEmbeddingClient(_ApiKey!, _Endpoint, _Logging);
        }

        /// <inheritdoc />
        public override async Task<EmbeddingModelCapabilities?> GetModelCapabilitiesAsync(string model, CancellationToken token = default)
        {
            EmbeddingModelCapabilities? capabilities = await base.GetModelCapabilitiesAsync(model, token).ConfigureAwait(false);
            if (capabilities != null)
                capabilities.ProviderMetadata["CapabilitySource"] = "XAiModelRegistry";
            return capabilities;
        }

        private static string RequireApiKey(string apiKey)
        {
            ProviderDefaults.ValidateApiKey(ApiFormatEnum.xAI, apiKey);
            return apiKey;
        }
    }
}
