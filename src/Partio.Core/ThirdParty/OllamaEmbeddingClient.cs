namespace Partio.Core.ThirdParty
{
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using PolyPrompt.Clients;
    using PolyPrompt.Models;
    using SyslogLogging;

    /// <summary>
    /// Embedding client for the Ollama API backed by PolyPrompt.
    /// </summary>
    public class OllamaEmbeddingClient : EmbeddingClientBase
    {
        private readonly object _CallDetailsLock = new object();

        /// <summary>
        /// Initialize a new OllamaEmbeddingClient.
        /// </summary>
        /// <param name="endpoint">Ollama server endpoint URL.</param>
        /// <param name="apiKey">API key.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        public OllamaEmbeddingClient(
            string endpoint,
            string? apiKey,
            LoggingModule logging,
            int maximumTimeoutMs,
            string? concurrencyKey = null,
            int maxConcurrentRequests = 2)
            : base(endpoint, apiKey, logging, maximumTimeoutMs, concurrencyKey, maxConcurrentRequests)
        {
            _Header = "[OllamaEmbedding] ";
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
            using (OllamaClient client = CreateConfiguredClient(_MaximumTimeoutMs))
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
                catch (Exception ex) when (IsTimeoutLike(ex))
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

                    throw new Exception(response.Error ?? "Ollama embedding request failed.");
                }

                return response.Embeddings.Select(e => e.Embedding?.ToList() ?? new List<float>()).ToList();
            }
        }

        /// <inheritdoc />
        public override async Task<EmbeddingModelCapabilities?> GetModelCapabilitiesAsync(string model, CancellationToken token = default)
        {
            string url = _Endpoint.TrimEnd('/') + "/api/show";
            string requestBodyJson = "{\"model\":\"" + JsonEncodedText.Encode(model).ToString() + "\",\"verbose\":false}";
            using StringContent content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");
            EmbeddingHttpResult result = await PostAndRecordAsync(url, content, requestBodyJson, "CapabilityProbe", token).ConfigureAwait(false);

            if (!result.Response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(result.ResponseBody))
                return null;

            using JsonDocument doc = JsonDocument.Parse(result.ResponseBody);
            JsonElement root = doc.RootElement;

            EmbeddingModelCapabilities capabilities = new EmbeddingModelCapabilities();
            capabilities.SourceHint = TokenizationProfileSourceEnum.ProviderProbe;
            capabilities.BatchLimitMode = BatchLimitModeEnum.Unknown;
            capabilities.ProviderMetadata["CapabilitySource"] = "OllamaShow";
            capabilities.ProviderMetadata["BatchLimitModeSource"] = "Unverified";

            string? parameters = TryGetString(root, "parameters");
            if (!string.IsNullOrEmpty(parameters))
            {
                Match match = Regex.Match(parameters, @"num_ctx\s+(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int numCtx))
                    capabilities.MaxInputTokens = numCtx;
            }

            if (root.TryGetProperty("model_info", out JsonElement modelInfo))
            {
                string? architecture = TryGetString(modelInfo, "general.architecture");
                if (!string.IsNullOrEmpty(architecture))
                    capabilities.ProviderMetadata["Architecture"] = architecture;

                string? tokenizerFamily = TryGetString(modelInfo, "tokenizer.ggml.model");
                if (!string.IsNullOrEmpty(tokenizerFamily))
                    capabilities.ProviderMetadata["TokenizerFamily"] = tokenizerFamily;

                int? contextLength = TryGetInt(modelInfo, "bert.context_length")
                    ?? TryGetInt(modelInfo, "gemma.context_length")
                    ?? TryGetInt(modelInfo, "llama.context_length")
                    ?? TryGetInt(modelInfo, "qwen2.context_length");
                if (contextLength.HasValue && !capabilities.MaxInputTokens.HasValue)
                    capabilities.MaxInputTokens = contextLength.Value;

                if (!string.IsNullOrEmpty(tokenizerFamily)
                    && tokenizerFamily.Equals("bert", StringComparison.OrdinalIgnoreCase))
                {
                    capabilities.TokenizerKind = TokenizerKindEnum.BertWordPiece;
                    capabilities.TokenizerModel = "bert-base-uncased";
                }
            }

            if (capabilities.MaxInputTokens.HasValue)
                capabilities.ProviderMetadata["MaxInputTokens"] = capabilities.MaxInputTokens.Value.ToString();

            return capabilities;
        }

        /// <inheritdoc />
        public override async Task<int?> GetModelContextLengthAsync(string model, CancellationToken token = default)
        {
            EmbeddingModelCapabilities? capabilities = await GetModelCapabilitiesAsync(model, token).ConfigureAwait(false);
            return capabilities?.MaxInputTokens;
        }

        private OllamaClient CreateConfiguredClient(int timeoutMs)
        {
            OllamaClient client = new OllamaClient(_Endpoint, _ApiKey, _Logging);
            client.TimeoutMs = timeoutMs;
            return client;
        }

        private void AppendCallDetails(IEnumerable<PolyPrompt.Models.CompletionCallDetail> source)
        {
            lock (_CallDetailsLock)
            {
                foreach (PolyPrompt.Models.CompletionCallDetail src in source)
                {
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

        private void AppendRejectedCall(string? purpose, string url, string method, string error)
        {
            lock (_CallDetailsLock)
            {
                CallDetails.Add(new EmbeddingCallDetail
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

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property))
                return null;

            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.GetRawText(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => property.GetRawText()
            };
        }

        private static int? TryGetInt(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property))
                return null;

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int directValue))
                return directValue;

            if (property.ValueKind == JsonValueKind.String
                && int.TryParse(property.GetString(), out int stringValue))
                return stringValue;

            return null;
        }
    }
}
