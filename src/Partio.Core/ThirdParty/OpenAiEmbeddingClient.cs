namespace Partio.Core.ThirdParty
{
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using Partio.Core.Observability;
    using PolyPromptEmbeddingOptions = PolyPrompt.Models.EmbeddingOptions;
    using PolyPromptOpenAiEmbeddingClient = PolyPrompt.Clients.OpenAiEmbeddingClient;
    using PolyPromptEmbeddingClientBase = PolyPrompt.Clients.EmbeddingClientBase;
    using SyslogLogging;

    /// <summary>
    /// Embedding client for OpenAI-compatible APIs backed by PolyPrompt.
    /// </summary>
    public class OpenAiEmbeddingClient : EmbeddingClientBase
    {
        private readonly object _CallDetailsLock = new object();

        /// <summary>
        /// Initialize a new OpenAiEmbeddingClient.
        /// </summary>
        /// <param name="endpoint">OpenAI API endpoint URL.</param>
        /// <param name="apiKey">API key.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        /// <param name="maxQueueDepth">Maximum number of requests allowed to wait for a concurrency slot.</param>
        /// <param name="transport">Optional HTTP message handler for upstream requests. The caller owns it.</param>
        public OpenAiEmbeddingClient(
            string endpoint,
            string? apiKey,
            LoggingModule logging,
            int maximumTimeoutMs,
            string? concurrencyKey = null,
            int maxConcurrentRequests = 2,
            int maxQueueDepth = 0,
            HttpMessageHandler? transport = null)
            : base(endpoint, apiKey, logging, maximumTimeoutMs, concurrencyKey, maxConcurrentRequests, maxQueueDepth, transport)
        {
            _Header = "[OpenAiEmbedding] ";
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
            PolyPromptEmbeddingOptions options = CreateEmbeddingOptions(model);
            using IntegrationScope integ = IntegrationScope.Begin(ServiceName, "embedding", _ConcurrencyKey);
            PolyPrompt.Models.EmbeddingResponse response;
            IDisposable? concurrencyLease = null;
            using (PolyPromptEmbeddingClientBase client = CreateConfiguredClient(_MaximumTimeoutMs))
            {
                try
                {
                    using (CancellationTokenSource acquireTimeoutCts = new CancellationTokenSource(_MaximumTimeoutMs))
                    using (CancellationTokenSource acquireLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, acquireTimeoutCts.Token))
                    {
                        concurrencyLease = await AcquireRequestSlotAsync(acquireLinkedCts.Token).ConfigureAwait(false);
                    }
                    response = await client.EmbedAsync(texts, options, token).ConfigureAwait(false);
                }
                catch (Partio.Core.Exceptions.ProviderConcurrencyLimitException ex)
                {
                    integ.Outcome = "rejected";
                    AppendRejectedCall("EmbeddingRequest", _Endpoint.TrimEnd('/'), "POST", ex.Message);
                    AppendCallDetails(client.CallDetails);
                    throw;
                }
                catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
                {
                    AppendCallDetails(client.CallDetails);
                    throw new Partio.Core.Exceptions.ProviderOperationTimeoutException(
                        "Upstream embedding provider request timed out after " + _MaximumTimeoutMs + "ms.",
                        _MaximumTimeoutMs,
                        ex);
                }
                catch (Exception ex) when (!token.IsCancellationRequested && IsTimeoutLike(ex))
                {
                    AppendCallDetails(client.CallDetails);
                    throw new Partio.Core.Exceptions.ProviderOperationTimeoutException(
                        "Upstream embedding provider request timed out after " + _MaximumTimeoutMs + "ms.",
                        _MaximumTimeoutMs,
                        ex);
                }
                finally
                {
                    concurrencyLease?.Dispose();
                }

                AppendCallDetails(client.CallDetails);

                if (!response.Success)
                {
                    if (IsTimeoutMessage(response.Error))
                    {
                        throw new Partio.Core.Exceptions.ProviderOperationTimeoutException(
                            "Upstream embedding provider request timed out after " + _MaximumTimeoutMs + "ms.",
                            _MaximumTimeoutMs);
                    }

                    throw new Exception(response.Error ?? ProviderName + " embedding request failed.");
                }

                integ.Outcome = "ok";
                return response.Embeddings.Select(e => e.Embedding?.ToList() ?? new List<float>()).ToList();
            }
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

        /// <summary>
        /// Create the PolyPrompt embedding options for a request. Providers with request-level settings
        /// (for example Cohere and VoyageAI input types) return their provider-specific options type.
        /// </summary>
        /// <param name="model">Embedding model.</param>
        /// <returns>The embedding options.</returns>
        protected virtual PolyPromptEmbeddingOptions CreateEmbeddingOptions(string model)
        {
            return new PolyPromptEmbeddingOptions { Model = model };
        }

        /// <summary>
        /// Provider display name used in error messages.
        /// </summary>
        protected virtual string ProviderName => "OpenAI";

        /// <summary>
        /// Create the PolyPrompt client used for a single embedding operation.
        /// </summary>
        /// <returns>A new PolyPrompt embedding client; the caller disposes it.</returns>
        protected virtual PolyPromptEmbeddingClientBase CreateProviderClient()
        {
            return new PolyPromptOpenAiEmbeddingClient(_Endpoint, _ApiKey, _Logging);
        }

        /// <inheritdoc />
        public override async Task<bool> ValidateConnectivityAsync(string model, int timeoutMs, CancellationToken token = default)
        {
            using (PolyPromptEmbeddingClientBase client = CreateConfiguredClient(timeoutMs > 0 ? timeoutMs : _MaximumTimeoutMs))
            {
                if (!string.IsNullOrEmpty(model)) client.Model = model;
                bool reachable = await client.ValidateConnectivityAsync(token).ConfigureAwait(false);
                AppendCallDetails(client.CallDetails);
                return reachable;
            }
        }

        private PolyPromptEmbeddingClientBase CreateConfiguredClient(int timeoutMs)
        {
            PolyPromptEmbeddingClientBase client = CreateProviderClient();
            client.TimeoutMs = timeoutMs;
            return client;
        }

        private void AppendCallDetails(IEnumerable<PolyPrompt.Models.CallDetail> source)
        {
            lock (_CallDetailsLock)
            {
                foreach (PolyPrompt.Models.CallDetail src in source)
                {
                    AddCallDetail(new EmbeddingCallDetail
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

        private void AppendRejectedCall(string? purpose, string url, string method, string error)
        {
            lock (_CallDetailsLock)
            {
                AddCallDetail(new EmbeddingCallDetail
                {
                    Purpose = purpose,
                    Url = url,
                    Method = method,
                    RequestHeaders = new Dictionary<string, string>(),
                    TimestampUtc = DateTime.UtcNow,
                    Success = false,
                    Error = error
                });
            }
        }
    }
}
