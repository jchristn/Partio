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
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        public OpenAiEmbeddingClient(
            string endpoint,
            string? apiKey,
            LoggingModule logging,
            int maximumTimeoutMs,
            string? concurrencyKey = null,
            int maxConcurrentRequests = 2)
            : base(endpoint, apiKey, logging, maximumTimeoutMs, concurrencyKey, maxConcurrentRequests)
        {
            _Header = "[OpenAiEmbedding] ";
            _Client = new PolyPromptOpenAiClient(endpoint, apiKey, logging);
            _Client.TimeoutMs = _MaximumTimeoutMs;
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
            _Client.TimeoutMs = _MaximumTimeoutMs;
            PolyPrompt.Models.EmbeddingResponse response;
            IDisposable? concurrencyLease = null;
            try
            {
                concurrencyLease = AcquireRequestSlot();
                response = await _Client.EmbedAsync(texts, options, token).ConfigureAwait(false);
            }
            catch (Partio.Core.Exceptions.ProviderConcurrencyLimitException ex)
            {
                RecordRejectedCall("EmbeddingRequest", _Endpoint.TrimEnd('/'), "POST", ex.Message);
                SyncCallDetails();
                throw;
            }
            catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
            {
                SyncCallDetails();
                throw new Partio.Core.Exceptions.ProviderOperationTimeoutException(
                    "Upstream embedding provider request timed out after " + _MaximumTimeoutMs + "ms.",
                    _MaximumTimeoutMs,
                    ex);
            }
            catch (Exception ex) when (IsTimeoutLike(ex))
            {
                SyncCallDetails();
                throw new Partio.Core.Exceptions.ProviderOperationTimeoutException(
                    "Upstream embedding provider request timed out after " + _MaximumTimeoutMs + "ms.",
                    _MaximumTimeoutMs,
                    ex);
            }
            finally
            {
                concurrencyLease?.Dispose();
            }
            SyncCallDetails();

            if (!response.Success)
            {
                if (IsTimeoutMessage(response.Error))
                {
                    throw new Partio.Core.Exceptions.ProviderOperationTimeoutException(
                        "Upstream embedding provider request timed out after " + _MaximumTimeoutMs + "ms.",
                        _MaximumTimeoutMs);
                }

                throw new Exception(response.Error ?? "OpenAI embedding request failed.");
            }

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
