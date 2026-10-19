namespace Partio.Core.ThirdParty
{
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using PolyPromptEmbeddingClientBase = PolyPrompt.Clients.EmbeddingClientBase;
    using PolyPromptTeiEmbeddingClient = PolyPrompt.Clients.TeiEmbeddingClient;
    using SyslogLogging;

    /// <summary>
    /// Embedding client for a Hugging Face Text Embeddings Inference (TEI) server backed by PolyPrompt. A TEI
    /// server hosts exactly one model, so the endpoint Model is informational.
    /// </summary>
    public class TeiEmbeddingClient : OpenAiEmbeddingClient
    {
        /// <summary>
        /// Initialize a new TeiEmbeddingClient.
        /// </summary>
        /// <param name="endpoint">TEI server URL (for example http://localhost:8080).</param>
        /// <param name="apiKey">Optional bearer token, needed only when the server was started with --api-key.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        /// <param name="maxQueueDepth">Maximum number of requests allowed to wait for a concurrency slot.</param>
        /// <param name="transport">Optional HTTP message handler for upstream requests. The caller owns it.</param>
        public TeiEmbeddingClient(
            string endpoint,
            string? apiKey,
            LoggingModule logging,
            int maximumTimeoutMs,
            string? concurrencyKey = null,
            int maxConcurrentRequests = 2,
            int maxQueueDepth = 0,
            HttpMessageHandler? transport = null)
            : base(ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.TEI, endpoint), apiKey, logging, maximumTimeoutMs, concurrencyKey, maxConcurrentRequests, maxQueueDepth, transport)
        {
            _Header = "[TeiEmbedding] ";
        }

        /// <inheritdoc />
        protected override PolyPromptEmbeddingClientBase CreateProviderClient()
        {
            return new PolyPromptTeiEmbeddingClient(endpoint: _Endpoint, apiKey: _ApiKey, logging: _Logging, httpClient: _HttpClient);
        }

        /// <inheritdoc />
        protected override string ProviderName => "TEI";

        /// <inheritdoc />
        /// <remarks>Returns null so the server's TEI tokenization defaults apply.</remarks>
        public override Task<EmbeddingModelCapabilities?> GetModelCapabilitiesAsync(string model, CancellationToken token = default)
        {
            return Task.FromResult<EmbeddingModelCapabilities?>(null);
        }
    }
}
