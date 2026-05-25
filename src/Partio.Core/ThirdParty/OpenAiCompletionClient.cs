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
        private readonly PolyPromptOpenAiClient _Client;
        private int _RecordedCallCount = 0;

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
            _Client = new PolyPromptOpenAiClient(endpoint, apiKey, logging);
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
            _Client.Model = model;
            int effectiveTimeoutMs = ClampTimeoutMs(timeoutMs);
            _Client.TimeoutMs = effectiveTimeoutMs;

            PolyPromptChatCompletionOptions options = new PolyPromptChatCompletionOptions
            {
                MaxTokens = maxTokens,
                SystemPrompt = systemPrompt
            };

            PolyPromptChatResponse response;
            IDisposable? concurrencyLease = null;
            try
            {
                concurrencyLease = AcquireRequestSlot();
                response = await _Client.ChatAsync(prompt, options, token).ConfigureAwait(false);
            }
            catch (Partio.Core.Exceptions.ProviderConcurrencyLimitException ex)
            {
                RecordRejectedCall(_Endpoint.TrimEnd('/'), "POST", ex.Message);
                SyncCallDetails();
                throw;
            }
            catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
            {
                SyncCallDetails();
                throw new Partio.Core.Exceptions.ProviderOperationTimeoutException(
                    "Upstream inference provider request timed out after " + effectiveTimeoutMs + "ms.",
                    effectiveTimeoutMs,
                    ex);
            }
            catch (Exception ex) when (IsTimeoutLike(ex))
            {
                SyncCallDetails();
                throw new Partio.Core.Exceptions.ProviderOperationTimeoutException(
                    "Upstream inference provider request timed out after " + effectiveTimeoutMs + "ms.",
                    effectiveTimeoutMs,
                    ex);
            }
            finally
            {
                concurrencyLease?.Dispose();
            }
            SyncCallDetails();
            if (!response.Success && IsTimeoutMessage(response.Error))
            {
                throw new Partio.Core.Exceptions.ProviderOperationTimeoutException(
                    "Upstream inference provider request timed out after " + effectiveTimeoutMs + "ms.",
                    effectiveTimeoutMs);
            }
            return response.Success ? response.Text?.Trim() : null;
        }

        private void SyncCallDetails()
        {
            for (; _RecordedCallCount < _Client.CallDetails.Count; _RecordedCallCount++)
            {
                PolyPrompt.Models.CompletionCallDetail src = _Client.CallDetails[_RecordedCallCount];
                CallDetails.Add(new Partio.Core.Models.CompletionCallDetail
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
}
