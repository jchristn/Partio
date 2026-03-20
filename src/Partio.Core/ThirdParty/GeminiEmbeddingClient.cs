namespace Partio.Core.ThirdParty
{
    using Partio.Core.Models;
    using PolyPrompt.Clients;
    using PolyPrompt.Models;
    using SyslogLogging;

    /// <summary>
    /// Embedding client for the Gemini API backed by PolyPrompt.
    /// </summary>
    public class GeminiEmbeddingClient : EmbeddingClientBase
    {
        private readonly GeminiClient _Client;
        private int _RecordedCallCount = 0;

        /// <summary>
        /// Initialize a new GeminiEmbeddingClient.
        /// </summary>
        /// <param name="endpoint">Gemini API endpoint URL.</param>
        /// <param name="apiKey">API key.</param>
        /// <param name="logging">Logging module.</param>
        public GeminiEmbeddingClient(string endpoint, string? apiKey, LoggingModule logging)
            : base(endpoint, apiKey, logging)
        {
            _Header = "[GeminiEmbedding] ";
            _Client = new GeminiClient(endpoint, apiKey, logging);
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
            EmbeddingResponse response = await _Client.EmbedAsync(texts, options, token).ConfigureAwait(false);
            SyncCallDetails();

            if (!response.Success)
                throw new Exception(response.Error ?? "Gemini embedding request failed.");

            return response.Embeddings.Select(e => e.Embedding?.ToList() ?? new List<float>()).ToList();
        }

        private void SyncCallDetails()
        {
            for (; _RecordedCallCount < _Client.CallDetails.Count; _RecordedCallCount++)
            {
                PolyPrompt.Models.CompletionCallDetail src = _Client.CallDetails[_RecordedCallCount];
                CallDetails.Add(new EmbeddingCallDetail
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
