namespace Partio.Core.ThirdParty
{
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using PolyPromptEmbeddingClientBase = PolyPrompt.Clients.EmbeddingClientBase;
    using PolyPromptVoyageAiEmbeddingClient = PolyPrompt.Clients.VoyageAiEmbeddingClient;
    using SyslogLogging;

    /// <summary>
    /// Embedding client for the VoyageAI API backed by PolyPrompt.
    /// </summary>
    public class VoyageAiEmbeddingClient : OpenAiEmbeddingClient
    {
        private readonly VoyageAiProviderSettings _Settings;

        /// <summary>
        /// Initialize a new VoyageAiEmbeddingClient.
        /// </summary>
        /// <param name="endpoint">VoyageAI API root (for example https://api.voyageai.com).</param>
        /// <param name="apiKey">VoyageAI API key (required).</param>
        /// <param name="settings">VoyageAI settings (input type and output dimension). Null uses the defaults.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        /// <param name="maxQueueDepth">Maximum number of requests allowed to wait for a concurrency slot.</param>
        /// <param name="transport">Optional HTTP message handler for upstream requests. The caller owns it.</param>
        /// <exception cref="ArgumentException">No API key was supplied.</exception>
        public VoyageAiEmbeddingClient(
            string endpoint,
            string apiKey,
            VoyageAiProviderSettings? settings,
            LoggingModule logging,
            int maximumTimeoutMs,
            string? concurrencyKey = null,
            int maxConcurrentRequests = 2,
            int maxQueueDepth = 0,
            HttpMessageHandler? transport = null)
            : base(ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.VoyageAI, endpoint), RequireApiKey(apiKey), logging, maximumTimeoutMs, concurrencyKey, maxConcurrentRequests, maxQueueDepth, transport)
        {
            _Header = "[VoyageAiEmbedding] ";
            _Settings = settings ?? new VoyageAiProviderSettings();
        }

        /// <inheritdoc />
        protected override PolyPromptEmbeddingClientBase CreateProviderClient()
        {
            return new PolyPromptVoyageAiEmbeddingClient(endpoint: _Endpoint, apiKey: _ApiKey, logging: _Logging, httpClient: _HttpClient);
        }

        /// <inheritdoc />
        protected override string ProviderName => "VoyageAI";

        /// <inheritdoc />
        protected override PolyPrompt.Models.EmbeddingOptions CreateEmbeddingOptions(string model)
        {
            return new PolyPrompt.Options.VoyageAiEmbeddingOptions
            {
                Model = model,
                InputType = _Settings.InputType,
                OutputDimension = _Settings.OutputDimension
            };
        }

        /// <inheritdoc />
        /// <remarks>Returns null so the server's VoyageAI tokenization defaults apply.</remarks>
        public override Task<EmbeddingModelCapabilities?> GetModelCapabilitiesAsync(string model, CancellationToken token = default)
        {
            return Task.FromResult<EmbeddingModelCapabilities?>(null);
        }

        private static string RequireApiKey(string? apiKey)
        {
            ProviderDefaults.ValidateApiKey(ApiFormatEnum.VoyageAI, apiKey);
            return apiKey!;
        }
    }
}
