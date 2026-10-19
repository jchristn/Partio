namespace Partio.Core.ThirdParty
{
    using PolyPromptOllamaEmbeddingClient = PolyPrompt.Clients.OllamaEmbeddingClient;
    using PolyPromptOllamaCloudEmbeddingClient = PolyPrompt.Clients.OllamaCloudEmbeddingClient;
    using SyslogLogging;

    /// <summary>
    /// Embedding client for Ollama Cloud backed by PolyPrompt. Ollama Cloud speaks the native Ollama API at
    /// the fixed host <see cref="ProviderDefaults.OllamaCloudEndpoint"/> and requires a bearer token.
    /// </summary>
    public class OllamaCloudEmbeddingClient : OllamaEmbeddingClient
    {
        /// <summary>
        /// Initialize a new OllamaCloudEmbeddingClient.
        /// </summary>
        /// <param name="apiKey">Ollama Cloud API key (required).</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        /// <param name="maxQueueDepth">Maximum number of requests allowed to wait for a concurrency slot.</param>
        /// <param name="transport">Optional HTTP message handler for upstream requests. The caller owns it.</param>
        /// <exception cref="ArgumentException">No API key was supplied.</exception>
        public OllamaCloudEmbeddingClient(
            string apiKey,
            LoggingModule logging,
            int maximumTimeoutMs,
            string? concurrencyKey = null,
            int maxConcurrentRequests = 2,
            int maxQueueDepth = 0,
            HttpMessageHandler? transport = null)
            : base(ProviderDefaults.OllamaCloudEndpoint, RequireApiKey(apiKey), logging, maximumTimeoutMs, concurrencyKey, maxConcurrentRequests, maxQueueDepth, transport)
        {
            _Header = "[OllamaCloudEmbedding] ";
        }

        /// <inheritdoc />
        protected override PolyPromptOllamaEmbeddingClient CreateProviderClient()
        {
            // Credentials are attached per request, so the shared Partio HttpClient is safe to reuse.
            return new PolyPromptOllamaCloudEmbeddingClient(_ApiKey!, _Logging, _HttpClient);
        }

        private static string RequireApiKey(string apiKey)
        {
            ProviderDefaults.ValidateApiKey(Partio.Core.Enums.ApiFormatEnum.OllamaCloud, apiKey);
            return apiKey;
        }
    }
}
