namespace Partio.Core.ThirdParty
{
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using PolyPromptEmbeddingClientBase = PolyPrompt.Clients.EmbeddingClientBase;
    using PolyPromptVertexAiEmbeddingClient = PolyPrompt.Clients.VertexAiEmbeddingClient;
    using SyslogLogging;

    /// <summary>
    /// Embedding client for Google Vertex AI backed by PolyPrompt. Requests carry an OAuth bearer token from the
    /// endpoint's service account, the server's Application Default Credentials, or a static access token.
    /// </summary>
    public class VertexAiEmbeddingClient : OpenAiEmbeddingClient
    {
        private readonly VertexAiProviderSettings _Settings;

        /// <summary>
        /// Initialize a new VertexAiEmbeddingClient.
        /// </summary>
        /// <param name="endpoint">Vertex AI regional API root (for example https://us-central1-aiplatform.googleapis.com). Empty derives it from the region.</param>
        /// <param name="apiKey">Static access token when CredentialSource is AccessToken; otherwise unused.</param>
        /// <param name="settings">Vertex AI settings (project, region, and credential source, required).</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        /// <param name="maxQueueDepth">Maximum number of requests allowed to wait for a concurrency slot.</param>
        /// <param name="transport">Optional HTTP message handler for upstream requests. The caller owns it.</param>
        /// <exception cref="ArgumentException">No project ID or region was supplied.</exception>
        public VertexAiEmbeddingClient(
            string endpoint,
            string? apiKey,
            VertexAiProviderSettings settings,
            LoggingModule logging,
            int maximumTimeoutMs,
            string? concurrencyKey = null,
            int maxConcurrentRequests = 2,
            int maxQueueDepth = 0,
            HttpMessageHandler? transport = null)
            : base(ResolveEndpoint(endpoint, settings), apiKey, logging, maximumTimeoutMs, concurrencyKey, maxConcurrentRequests, maxQueueDepth, transport)
        {
            _Header = "[VertexAiEmbedding] ";
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(_Settings.ProjectId))
                throw new ArgumentException("Vertex AI endpoints require a project ID.", nameof(settings));
            if (string.IsNullOrWhiteSpace(_Settings.Region))
                throw new ArgumentException("Vertex AI endpoints require a region.", nameof(settings));
        }

        /// <inheritdoc />
        protected override PolyPromptEmbeddingClientBase CreateProviderClient()
        {
            return new PolyPromptVertexAiEmbeddingClient(
                project: _Settings.ProjectId!,
                region: _Settings.Region!,
                credential: ProviderCredentialCache.GetVertexCredential(_Settings, _ApiKey),
                endpoint: _Endpoint,
                logging: _Logging,
                httpClient: _HttpClient);
        }

        /// <inheritdoc />
        protected override string ProviderName => "Vertex AI";

        /// <inheritdoc />
        /// <remarks>Returns null so the server's VertexAI tokenization defaults apply.</remarks>
        public override Task<EmbeddingModelCapabilities?> GetModelCapabilitiesAsync(string model, CancellationToken token = default)
        {
            return Task.FromResult<EmbeddingModelCapabilities?>(null);
        }

        private static string ResolveEndpoint(string endpoint, VertexAiProviderSettings settings)
        {
            return ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.VertexAI, endpoint, new ProviderSettings { VertexAI = settings });
        }
    }
}
