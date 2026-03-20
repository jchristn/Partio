namespace Partio.Core.ThirdParty
{
    using PolyPrompt.Clients;
    using PolyPrompt.Models;
    using SyslogLogging;

    /// <summary>
    /// Completion client for the Gemini API backed by PolyPrompt.
    /// </summary>
    public class GeminiCompletionClient : CompletionClientBase
    {
        private readonly GeminiClient _Client;
        private int _RecordedCallCount = 0;

        /// <summary>
        /// Initialize a new GeminiCompletionClient.
        /// </summary>
        /// <param name="endpoint">Gemini API endpoint URL.</param>
        /// <param name="apiKey">API key.</param>
        /// <param name="logging">Logging module.</param>
        public GeminiCompletionClient(string endpoint, string? apiKey, LoggingModule logging)
            : base(endpoint, apiKey, logging)
        {
            _Header = "[GeminiCompletion] ";
            _Client = new GeminiClient(endpoint, apiKey, logging);
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

            ChatCompletionOptions options = new ChatCompletionOptions
            {
                MaxTokens = maxTokens,
                SystemPrompt = systemPrompt
            };

            ChatResponse response = await _Client.ChatAsync(prompt, options, token).ConfigureAwait(false);
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
