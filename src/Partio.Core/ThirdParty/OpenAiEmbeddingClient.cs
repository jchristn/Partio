namespace Partio.Core.ThirdParty
{
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using PolyPromptEmbeddingOptions = PolyPrompt.Models.EmbeddingOptions;
    using PolyPromptOpenAiClient = PolyPrompt.Clients.OpenAiClient;
    using SyslogLogging;

    /// <summary>
    /// Embedding client for OpenAI-compatible APIs backed by PolyPrompt.
    /// </summary>
    public class OpenAiEmbeddingClient : EmbeddingClientBase
    {
        private readonly PolyPromptOpenAiClient _Client;
        private int _RecordedCallCount = 0;

        /// <summary>
        /// Initialize a new OpenAiEmbeddingClient.
        /// </summary>
        /// <param name="endpoint">OpenAI API endpoint URL.</param>
        /// <param name="apiKey">API key.</param>
        /// <param name="logging">Logging module.</param>
        public OpenAiEmbeddingClient(string endpoint, string? apiKey, LoggingModule logging)
            : base(endpoint, apiKey, logging)
        {
            _Header = "[OpenAiEmbedding] ";
            _Client = new PolyPromptOpenAiClient(endpoint, apiKey, logging);
        }

        /// <inheritdoc />
        public override async Task<List<float>> EmbedAsync(string text, string model, CancellationToken token = default)
        {
            List<List<float>> results = await EmbedBatchAsync(new List<string> { text }, model, token).ConfigureAwait(false);
            return results.Count > 0 ? results[0] : new List<float>();
        }

        /// <inheritdoc />
        public override async Task<List<List<float>>> EmbedBatchAsync(List<string> texts, string model, CancellationToken token = default)
        {
            PolyPromptEmbeddingOptions options = new PolyPromptEmbeddingOptions { Model = model };
            PolyPrompt.Models.EmbeddingResponse response = await _Client.EmbedAsync(texts, options, token).ConfigureAwait(false);
            SyncCallDetails();

            if (!response.Success)
                throw new Exception(response.Error ?? "OpenAI embedding request failed.");

            return response.Embeddings.Select(e => e.Embedding?.ToList() ?? new List<float>()).ToList();
        }

        /// <inheritdoc />
        public override Task<EmbeddingModelCapabilities?> GetModelCapabilitiesAsync(string model, CancellationToken token = default)
        {
            EmbeddingModelCapabilities capabilities = new EmbeddingModelCapabilities
            {
                SourceHint = TokenizationProfileSourceEnum.ProviderDefault,
                TokenizerKind = TokenizerKindEnum.Cl100kBase,
                TokenizerModel = "cl100k_base",
                MaxInputTokens = 8192,
                ReservedInputTokens = 0,
                BatchLimitMode = BatchLimitModeEnum.PerInput
            };
            capabilities.ProviderMetadata["CapabilitySource"] = "OpenAIModelRegistry";
            capabilities.ProviderMetadata["Model"] = model;
            return Task.FromResult<EmbeddingModelCapabilities?>(capabilities);
        }

        private void SyncCallDetails()
        {
            for (; _RecordedCallCount < _Client.CallDetails.Count; _RecordedCallCount++)
            {
                PolyPrompt.Models.CompletionCallDetail src = _Client.CallDetails[_RecordedCallCount];
                CallDetails.Add(new EmbeddingCallDetail
                {
                    Purpose = "EmbeddingRequest",
                    Url = src.Url,
                    Method = src.Method,
                    RequestHeaders = src.RequestHeaders,
                    RequestBody = src.RequestBody,
                    StatusCode = src.StatusCode,
                    ResponseHeaders = src.ResponseHeaders,
                    ResponseBody = src.ResponseBody,
                    ResponseTimeMs = src.ResponseTimeMs,
                    Success = src.Success,
                    Error = src.Error,
                    TimestampUtc = src.TimestampUtc
                });
            }
        }
    }
}
