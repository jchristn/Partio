namespace Partio.Core.ThirdParty
{
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using PolyPromptCompletionClientBase = PolyPrompt.Clients.CompletionClientBase;
    using PolyPromptAnthropicCompletionClient = PolyPrompt.Clients.AnthropicCompletionClient;
    using SyslogLogging;

    /// <summary>
    /// Completion client for the Anthropic Messages API backed by PolyPrompt.
    /// </summary>
    public class AnthropicCompletionClient : OpenAiCompletionClient
    {
        private readonly AnthropicProviderSettings _Settings;

        /// <summary>
        /// Initialize a new AnthropicCompletionClient.
        /// </summary>
        /// <param name="endpoint">Anthropic API root (for example https://api.anthropic.com).</param>
        /// <param name="apiKey">Anthropic API key (required).</param>
        /// <param name="settings">Anthropic settings (anthropic-version and workspace). Null uses the defaults.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        /// <param name="maxQueueDepth">Maximum number of requests allowed to wait for a concurrency slot.</param>
        /// <param name="transport">Optional HTTP message handler for upstream requests. The caller owns it.</param>
        /// <exception cref="ArgumentException">No API key was supplied.</exception>
        public AnthropicCompletionClient(
            string endpoint,
            string apiKey,
            AnthropicProviderSettings? settings,
            LoggingModule logging,
            int maximumTimeoutMs,
            string? concurrencyKey = null,
            int maxConcurrentRequests = 2,
            int maxQueueDepth = 0,
            HttpMessageHandler? transport = null)
            : base(ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.Anthropic, endpoint), RequireApiKey(apiKey), logging, maximumTimeoutMs, concurrencyKey, maxConcurrentRequests, maxQueueDepth, transport)
        {
            _Header = "[AnthropicCompletion] ";
            _Settings = settings ?? new AnthropicProviderSettings();
        }

        /// <inheritdoc />
        protected override PolyPromptCompletionClientBase CreateProviderClient()
        {
            PolyPromptAnthropicCompletionClient client = new PolyPromptAnthropicCompletionClient(endpoint: _Endpoint, apiKey: _ApiKey, logging: _Logging, httpClient: _HttpClient);
            if (!string.IsNullOrEmpty(_Settings.AnthropicVersion)) client.AnthropicVersion = _Settings.AnthropicVersion;
            if (!string.IsNullOrEmpty(_Settings.WorkspaceId)) client.WorkspaceId = _Settings.WorkspaceId;
            return client;
        }

        private static string RequireApiKey(string? apiKey)
        {
            ProviderDefaults.ValidateApiKey(ApiFormatEnum.Anthropic, apiKey);
            return apiKey!;
        }
    }
}
