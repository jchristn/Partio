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
        public OpenAiCompletionClient(string endpoint, string? apiKey, LoggingModule logging)
            : base(endpoint, apiKey, logging)
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
            _Client.TimeoutMs = timeoutMs;

            PolyPromptChatCompletionOptions options = new PolyPromptChatCompletionOptions
            {
                MaxTokens = maxTokens,
                SystemPrompt = systemPrompt
            };

            PolyPromptChatResponse response = await _Client.ChatAsync(prompt, options, token).ConfigureAwait(false);
            SyncCallDetails();
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
