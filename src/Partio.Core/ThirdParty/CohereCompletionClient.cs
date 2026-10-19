namespace Partio.Core.ThirdParty
{
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using PolyPromptCompletionClientBase = PolyPrompt.Clients.CompletionClientBase;
    using PolyPromptCohereCompletionClient = PolyPrompt.Clients.CohereCompletionClient;
    using SyslogLogging;

    /// <summary>
    /// Completion client for the Cohere v2 API backed by PolyPrompt.
    /// </summary>
    public class CohereCompletionClient : OpenAiCompletionClient
    {
        private readonly CohereProviderSettings _Settings;

        /// <summary>
        /// Initialize a new CohereCompletionClient.
        /// </summary>
        /// <param name="endpoint">Cohere API root (for example https://api.cohere.com).</param>
        /// <param name="apiKey">Cohere API key (required).</param>
        /// <param name="settings">Cohere settings. Null uses the defaults.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        /// <param name="maxQueueDepth">Maximum number of requests allowed to wait for a concurrency slot.</param>
        /// <param name="transport">Optional HTTP message handler for upstream requests. The caller owns it.</param>
        /// <exception cref="ArgumentException">No API key was supplied.</exception>
        public CohereCompletionClient(
            string endpoint,
            string apiKey,
            CohereProviderSettings? settings,
            LoggingModule logging,
            int maximumTimeoutMs,
            string? concurrencyKey = null,
            int maxConcurrentRequests = 2,
            int maxQueueDepth = 0,
            HttpMessageHandler? transport = null)
            : base(ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.Cohere, endpoint), RequireApiKey(apiKey), logging, maximumTimeoutMs, concurrencyKey, maxConcurrentRequests, maxQueueDepth, transport)
        {
            _Header = "[CohereCompletion] ";
            _Settings = settings ?? new CohereProviderSettings();
        }

        /// <inheritdoc />
        protected override PolyPromptCompletionClientBase CreateProviderClient()
        {
            return new PolyPromptCohereCompletionClient(endpoint: _Endpoint, apiKey: _ApiKey, logging: _Logging, httpClient: _HttpClient);
        }

        private static string RequireApiKey(string? apiKey)
        {
            ProviderDefaults.ValidateApiKey(ApiFormatEnum.Cohere, apiKey);
            return apiKey!;
        }
    }
}
