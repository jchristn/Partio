namespace Partio.Core.ThirdParty
{
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using PolyPromptCompletionClientBase = PolyPrompt.Clients.CompletionClientBase;
    using PolyPromptBedrockCompletionClient = PolyPrompt.Clients.BedrockCompletionClient;
    using SyslogLogging;

    /// <summary>
    /// Completion client for AWS Bedrock backed by PolyPrompt. Every request is signed with AWS Signature V4
    /// using the endpoint's Bedrock credentials.
    /// </summary>
    public class BedrockCompletionClient : OpenAiCompletionClient
    {
        private readonly BedrockProviderSettings _Settings;

        /// <summary>
        /// Initialize a new BedrockCompletionClient.
        /// </summary>
        /// <param name="endpoint">Bedrock runtime URL (for example https://bedrock-runtime.us-east-1.amazonaws.com). Empty derives it from the region.</param>
        /// <param name="settings">Bedrock settings (region and credentials, required).</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        /// <param name="maxQueueDepth">Maximum number of requests allowed to wait for a concurrency slot.</param>
        /// <param name="transport">Optional HTTP message handler for upstream requests. The caller owns it.</param>
        /// <exception cref="ArgumentException">No region was supplied.</exception>
        public BedrockCompletionClient(
            string endpoint,
            BedrockProviderSettings settings,
            LoggingModule logging,
            int maximumTimeoutMs,
            string? concurrencyKey = null,
            int maxConcurrentRequests = 2,
            int maxQueueDepth = 0,
            HttpMessageHandler? transport = null)
            : base(ResolveEndpoint(endpoint, settings), null, logging, maximumTimeoutMs, concurrencyKey, maxConcurrentRequests, maxQueueDepth, transport)
        {
            _Header = "[BedrockCompletion] ";
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(_Settings.Region))
                throw new ArgumentException("Bedrock endpoints require a region.", nameof(settings));
        }

        /// <inheritdoc />
        protected override PolyPromptCompletionClientBase CreateProviderClient()
        {
            return new PolyPromptBedrockCompletionClient(
                credentialProvider: ProviderCredentialCache.GetAwsCredential(_Settings),
                region: _Settings.Region!,
                endpoint: _Endpoint,
                logging: _Logging,
                httpClient: _HttpClient);
        }

        private static string ResolveEndpoint(string endpoint, BedrockProviderSettings settings)
        {
            return ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.Bedrock, endpoint, new ProviderSettings { Bedrock = settings });
        }
    }
}
