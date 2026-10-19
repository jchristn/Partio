namespace Partio.Core.ThirdParty
{
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using PolyPromptCompletionClientBase = PolyPrompt.Clients.CompletionClientBase;
    using PolyPromptAzureOpenAiCompletionClient = PolyPrompt.Clients.AzureOpenAiCompletionClient;
    using PolyPrompt.Auth;
    using SyslogLogging;

    /// <summary>
    /// Completion client for Azure OpenAI backed by PolyPrompt. The endpoint is the Azure resource URL and the
    /// model is the deployment name; requests carry the configured api-version.
    /// </summary>
    public class AzureOpenAiCompletionClient : OpenAiCompletionClient
    {
        private readonly string _Deployment;
        private readonly AzureOpenAiProviderSettings _Settings;

        /// <summary>
        /// Initialize a new AzureOpenAiCompletionClient.
        /// </summary>
        /// <param name="endpoint">Azure resource URL (for example https://my-resource.openai.azure.com).</param>
        /// <param name="deployment">Deployment name (the endpoint Model).</param>
        /// <param name="apiKey">Azure OpenAI API key, or an Entra ID access token when AuthMode is BearerToken (required).</param>
        /// <param name="settings">Azure OpenAI settings (api-version and auth mode). Null uses the defaults.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        /// <param name="maxQueueDepth">Maximum number of requests allowed to wait for a concurrency slot.</param>
        /// <param name="transport">Optional HTTP message handler for upstream requests. The caller owns it.</param>
        /// <exception cref="ArgumentException">No API key or deployment was supplied.</exception>
        public AzureOpenAiCompletionClient(
            string endpoint,
            string deployment,
            string apiKey,
            AzureOpenAiProviderSettings? settings,
            LoggingModule logging,
            int maximumTimeoutMs,
            string? concurrencyKey = null,
            int maxConcurrentRequests = 2,
            int maxQueueDepth = 0,
            HttpMessageHandler? transport = null)
            : base(ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.AzureOpenAI, endpoint), RequireApiKey(apiKey), logging, maximumTimeoutMs, concurrencyKey, maxConcurrentRequests, maxQueueDepth, transport)
        {
            _Header = "[AzureOpenAiCompletion] ";
            _Deployment = !string.IsNullOrWhiteSpace(deployment) ? deployment : throw new ArgumentException("Azure OpenAI endpoints require a deployment name.", nameof(deployment));
            _Settings = settings ?? new AzureOpenAiProviderSettings();
        }

        /// <inheritdoc />
        protected override PolyPromptCompletionClientBase CreateProviderClient()
        {
            string apiVersion = _Settings.ApiVersion ?? ProviderDefaults.AzureOpenAiApiVersion;
            if (_Settings.AuthMode == AzureAuthModeEnum.BearerToken)
                return new PolyPromptAzureOpenAiCompletionClient(_Endpoint, _Deployment, new StaticTokenCredential(_ApiKey!), apiVersion, _Logging, _HttpClient);
            return new PolyPromptAzureOpenAiCompletionClient(_Endpoint, _Deployment, _ApiKey!, apiVersion, _Logging, _HttpClient);
        }

        private static string RequireApiKey(string? apiKey)
        {
            ProviderDefaults.ValidateApiKey(ApiFormatEnum.AzureOpenAI, apiKey);
            return apiKey!;
        }
    }
}
