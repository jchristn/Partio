namespace Partio.Core.ThirdParty
{
    using Partio.Core.Enums;
    using PolyPromptCompletionClientBase = PolyPrompt.Clients.CompletionClientBase;
    using PolyPromptXAiCompletionClient = PolyPrompt.Clients.XAiCompletionClient;
    using SyslogLogging;

    /// <summary>
    /// Completion client for the xAI (Grok) API backed by PolyPrompt. Completions use xAI's native
    /// Responses API (<c>/v1/responses</c>).
    /// </summary>
    public class XAiCompletionClient : OpenAiCompletionClient
    {
        /// <summary>
        /// Initialize a new XAiCompletionClient.
        /// </summary>
        /// <param name="endpoint">xAI API root (for example https://api.x.ai).</param>
        /// <param name="apiKey">xAI API key (required).</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        /// <param name="maxQueueDepth">Maximum number of requests allowed to wait for a concurrency slot.</param>
        /// <exception cref="ArgumentException">No API key was supplied.</exception>
        public XAiCompletionClient(
            string endpoint,
            string apiKey,
            LoggingModule logging,
            int maximumTimeoutMs,
            string? concurrencyKey = null,
            int maxConcurrentRequests = 2,
            int maxQueueDepth = 0)
            : base(ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.xAI, endpoint), RequireApiKey(apiKey), logging, maximumTimeoutMs, concurrencyKey, maxConcurrentRequests, maxQueueDepth)
        {
            _Header = "[XAiCompletion] ";
        }

        /// <inheritdoc />
        protected override PolyPromptCompletionClientBase CreateProviderClient()
        {
            return new PolyPromptXAiCompletionClient(_ApiKey!, _Endpoint, _Logging);
        }

        private static string RequireApiKey(string apiKey)
        {
            ProviderDefaults.ValidateApiKey(ApiFormatEnum.xAI, apiKey);
            return apiKey;
        }
    }
}
