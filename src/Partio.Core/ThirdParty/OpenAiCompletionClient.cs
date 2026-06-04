namespace Partio.Core.ThirdParty
{
    using PolyPromptChatCompletionOptions = PolyPrompt.Models.ChatCompletionOptions;
    using PolyPromptChatResponse = PolyPrompt.Models.ChatResponse;
    using PolyPromptOpenAiClient = PolyPrompt.Clients.OpenAiClient;
    using SyslogLogging;

    /// <summary>
    /// Completion client for OpenAI-compatible APIs backed by PolyPrompt.
    /// </summary>
    public class OpenAiCompletionClient : CompletionClientBase
    {
        private readonly object _CallDetailsLock = new object();

        /// <summary>
        /// Initialize a new OpenAiCompletionClient.
        /// </summary>
        /// <param name="endpoint">OpenAI API endpoint URL.</param>
        /// <param name="apiKey">API key.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        public OpenAiCompletionClient(
            string endpoint,
            string? apiKey,
            LoggingModule logging,
            int maximumTimeoutMs,
            string? concurrencyKey = null,
            int maxConcurrentRequests = 2)
            : base(endpoint, apiKey, logging, maximumTimeoutMs, concurrencyKey, maxConcurrentRequests)
        {
            _Header = "[OpenAiCompletion] ";
        }

        /// <inheritdoc />
        public override async Task<string?> GenerateCompletionAsync(
            string prompt,
            string model,
            int maxTokens,
            int timeoutMs,
            CancellationToken token = default,
            string? systemPrompt = null)
        {
            int effectiveTimeoutMs = ClampTimeoutMs(timeoutMs);

            PolyPromptChatCompletionOptions options = new PolyPromptChatCompletionOptions
            {
                MaxTokens = maxTokens,
                SystemPrompt = systemPrompt
            };

            PolyPromptChatResponse response;
            IDisposable? concurrencyLease = null;
            using (PolyPromptOpenAiClient client = CreateConfiguredClient(model, effectiveTimeoutMs))
            {
                try
                {
                    concurrencyLease = AcquireRequestSlot();
                    response = await client.ChatAsync(prompt, options, token).ConfigureAwait(false);
                }
                catch (Partio.Core.Exceptions.ProviderConcurrencyLimitException ex)
                {
                    AppendRejectedCall(_Endpoint.TrimEnd('/'), "POST", ex.Message);
                    AppendCallDetails(client.CallDetails);
                    throw;
                }
                catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
                {
                    AppendCallDetails(client.CallDetails);
                    throw new Partio.Core.Exceptions.ProviderOperationTimeoutException(
                        "Upstream inference provider request timed out after " + effectiveTimeoutMs + "ms.",
                        effectiveTimeoutMs,
                        ex);
                }
                catch (Exception ex) when (!token.IsCancellationRequested && IsTimeoutLike(ex))
                {
                    AppendCallDetails(client.CallDetails);
                    throw new Partio.Core.Exceptions.ProviderOperationTimeoutException(
                        "Upstream inference provider request timed out after " + effectiveTimeoutMs + "ms.",
                        effectiveTimeoutMs,
                        ex);
                }
                finally
                {
                    concurrencyLease?.Dispose();
                }

                AppendCallDetails(client.CallDetails);
                if (!response.Success && IsTimeoutMessage(response.Error))
                {
                    throw new Partio.Core.Exceptions.ProviderOperationTimeoutException(
                        "Upstream inference provider request timed out after " + effectiveTimeoutMs + "ms.",
                        effectiveTimeoutMs);
                }
                return response.Success ? response.Text?.Trim() : null;
            }
        }

        private PolyPromptOpenAiClient CreateConfiguredClient(string model, int timeoutMs)
        {
            PolyPromptOpenAiClient client = new PolyPromptOpenAiClient(_Endpoint, _ApiKey, _Logging);
            client.Model = model;
            client.TimeoutMs = timeoutMs;
            return client;
        }

        private void AppendCallDetails(IEnumerable<PolyPrompt.Models.CompletionCallDetail> source)
        {
            lock (_CallDetailsLock)
            {
                foreach (PolyPrompt.Models.CompletionCallDetail src in source)
                {
                    AddCallDetail(new Partio.Core.Models.CompletionCallDetail
                    {
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

        private void AppendRejectedCall(string url, string method, string error)
        {
            lock (_CallDetailsLock)
            {
                AddCallDetail(new Partio.Core.Models.CompletionCallDetail
                {
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
