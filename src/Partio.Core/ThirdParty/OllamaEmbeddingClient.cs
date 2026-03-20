namespace Partio.Core.ThirdParty
{
    using System.Text.RegularExpressions;
    using Partio.Core.Models;
    using PolyPrompt.Clients;
    using PolyPrompt.Models;
    using SyslogLogging;

    /// <summary>
    /// Embedding client for the Ollama API backed by PolyPrompt.
    /// </summary>
    public class OllamaEmbeddingClient : EmbeddingClientBase
    {
        private readonly OllamaClient _Client;
        private int _RecordedCallCount = 0;

        /// <summary>
        /// Initialize a new OllamaEmbeddingClient.
        /// </summary>
        /// <param name="endpoint">Ollama server endpoint URL.</param>
        /// <param name="apiKey">API key.</param>
        /// <param name="logging">Logging module.</param>
        public OllamaEmbeddingClient(string endpoint, string? apiKey, LoggingModule logging)
            : base(endpoint, apiKey, logging)
        {
            _Header = "[OllamaEmbedding] ";
            _Client = new OllamaClient(endpoint, apiKey, logging);
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
                throw new Exception(response.Error ?? "Ollama embedding request failed.");

            return response.Embeddings.Select(e => e.Embedding?.ToList() ?? new List<float>()).ToList();
        }

        /// <inheritdoc />
        public override async Task<int?> GetModelContextLengthAsync(string model, CancellationToken token = default)
        {
            ModelInformation? info = await _Client.GetModelInformationAsync(model, token).ConfigureAwait(false);
            SyncCallDetails();
            if (info == null) return null;

            if (info.InputTokenLimit.HasValue) return info.InputTokenLimit.Value;

            if (info.Metadata.TryGetValue("parameters", out string? parameters) && !string.IsNullOrEmpty(parameters))
            {
                Match match = Regex.Match(parameters, @"num_ctx\s+(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int numCtx))
                    return numCtx;
            }

            return null;
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
