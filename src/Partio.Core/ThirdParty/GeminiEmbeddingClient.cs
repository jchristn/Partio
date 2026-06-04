namespace Partio.Core.ThirdParty
{
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using PolyPrompt.Clients;
    using PolyPrompt.Models;
    using SyslogLogging;

    /// <summary>
    /// Embedding client for the Gemini API backed by PolyPrompt.
    /// </summary>
    public class GeminiEmbeddingClient : EmbeddingClientBase
    {
        private readonly object _CallDetailsLock = new object();

        /// <summary>
        /// Initialize a new GeminiEmbeddingClient.
        /// </summary>
        /// <param name="endpoint">Gemini API endpoint URL.</param>
        /// <param name="apiKey">API key.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        public GeminiEmbeddingClient(
            string endpoint,
            string? apiKey,
            LoggingModule logging,
            int maximumTimeoutMs,
            string? concurrencyKey = null,
            int maxConcurrentRequests = 2)
            : base(endpoint, apiKey, logging, maximumTimeoutMs, concurrencyKey, maxConcurrentRequests)
        {
            _Header = "[GeminiEmbedding] ";
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
            EmbeddingOptions options = new EmbeddingOptions { Model = model };
            EmbeddingResponse response;
            IDisposable? concurrencyLease = null;
            using (GeminiClient client = CreateConfiguredClient(_MaximumTimeoutMs))
            {
                try
                {
                    concurrencyLease = AcquireRequestSlot();
                    response = await client.EmbedAsync(texts, options, token).ConfigureAwait(false);
                }
                catch (Partio.Core.Exceptions.ProviderConcurrencyLimitException ex)
                {
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

                    throw new Exception(response.Error ?? "Gemini embedding request failed.");
                }

                return response.Embeddings.Select(e => e.Embedding?.ToList() ?? new List<float>()).ToList();
            }
        }

        /// <inheritdoc />
        public override Task<EmbeddingModelCapabilities?> GetModelCapabilitiesAsync(string model, CancellationToken token = default)
        {
            EmbeddingModelCapabilities capabilities = new EmbeddingModelCapabilities
            {
                SourceHint = TokenizationProfileSourceEnum.ProviderDefault,
                MaxInputTokens = 2048,
                ReservedInputTokens = 0,
                BatchLimitMode = BatchLimitModeEnum.PerInput
            };
            capabilities.ProviderMetadata["CapabilitySource"] = "GeminiModelRegistry";
            capabilities.ProviderMetadata["Model"] = model;
            return Task.FromResult<EmbeddingModelCapabilities?>(capabilities);
        }

        private GeminiClient CreateConfiguredClient(int timeoutMs)
        {
            GeminiClient client = new GeminiClient(_Endpoint, _ApiKey, _Logging);
            client.TimeoutMs = timeoutMs;
            return client;
        }

        private void AppendCallDetails(IEnumerable<PolyPrompt.Models.CompletionCallDetail> source)
        {
            lock (_CallDetailsLock)
            {
                foreach (PolyPrompt.Models.CompletionCallDetail src in source)
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
