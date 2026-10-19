namespace Partio.Core.ThirdParty
{
    using PolyPromptOllamaCompletionClient = PolyPrompt.Clients.OllamaCompletionClient;
    using PolyPromptOllamaCloudCompletionClient = PolyPrompt.Clients.OllamaCloudCompletionClient;
    using SyslogLogging;

    /// <summary>
    /// Completion client for Ollama Cloud backed by PolyPrompt. Ollama Cloud speaks the native Ollama API at
    /// the fixed host <see cref="ProviderDefaults.OllamaCloudEndpoint"/> and requires a bearer token.
    /// </summary>
    public class OllamaCloudCompletionClient : OllamaCompletionClient
    {
        /// <summary>
        /// Initialize a new OllamaCloudCompletionClient.
        /// </summary>
        /// <param name="apiKey">Ollama Cloud API key (required).</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        /// <param name="maxQueueDepth">Maximum number of requests allowed to wait for a concurrency slot.</param>
        /// <param name="transport">Optional HTTP message handler for upstream requests. The caller owns it.</param>
        /// <exception cref="ArgumentException">No API key was supplied.</exception>
        public OllamaCloudCompletionClient(
            string apiKey,
            LoggingModule logging,
            int maximumTimeoutMs,
            string? concurrencyKey = null,
            int maxConcurrentRequests = 2,
            int maxQueueDepth = 0,
            HttpMessageHandler? transport = null)
            : base(ProviderDefaults.OllamaCloudEndpoint, RequireApiKey(apiKey), logging, maximumTimeoutMs, concurrencyKey, maxConcurrentRequests, maxQueueDepth, transport)
        {
            _Header = "[OllamaCloudCompletion] ";
        }

        /// <inheritdoc />
        protected override PolyPromptOllamaCompletionClient CreateProviderClient()
        {
            // Credentials are attached per request, so the shared Partio HttpClient is safe to reuse.
            return new PolyPromptOllamaCloudCompletionClient(_ApiKey!, _Logging, _HttpClient);
        }

        private static string RequireApiKey(string apiKey)
        {
            ProviderDefaults.ValidateApiKey(Partio.Core.Enums.ApiFormatEnum.OllamaCloud, apiKey);
            return apiKey;
        }
    }
}
