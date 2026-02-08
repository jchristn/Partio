namespace Partio.Core.ThirdParty
{
    using System.Diagnostics;
    using Partio.Core.Models;
    using SyslogLogging;

    /// <summary>
    /// Abstract base class for embedding API clients.
    /// </summary>
    public abstract class EmbeddingClientBase : IDisposable
    {
        /// <summary>
        /// Logging module.
        /// </summary>
        protected readonly LoggingModule _Logging;

        /// <summary>
        /// Endpoint URL.
        /// </summary>
        protected readonly string _Endpoint;

        /// <summary>
        /// API key (nullable).
        /// </summary>
        protected readonly string? _ApiKey;

        /// <summary>
        /// HTTP client for API requests.
        /// </summary>
        protected readonly HttpClient _HttpClient;

        /// <summary>
        /// Header prefix for log messages.
        /// </summary>
        protected string _Header = "[EmbeddingClient] ";

        /// <summary>
        /// Recorded details of HTTP calls made to upstream embedding endpoints.
        /// </summary>
        public List<EmbeddingCallDetail> CallDetails { get; } = new List<EmbeddingCallDetail>();

        /// <summary>
        /// Initialize a new EmbeddingClientBase.
        /// </summary>
        /// <param name="endpoint">Endpoint URL.</param>
        /// <param name="apiKey">API key (nullable).</param>
        /// <param name="logging">Logging module.</param>
        protected EmbeddingClientBase(string endpoint, string? apiKey, LoggingModule logging)
        {
            _Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _ApiKey = apiKey;
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _HttpClient = new HttpClient();
        }

        /// <summary>
        /// Generate embeddings for a single text.
        /// </summary>
        /// <param name="text">Input text.</param>
        /// <param name="model">Model name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Embedding vector.</returns>
        public abstract Task<List<float>> EmbedAsync(string text, string model, CancellationToken token = default);

        /// <summary>
        /// Generate embeddings for a batch of texts.
        /// </summary>
        /// <param name="texts">Input texts.</param>
        /// <param name="model">Model name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of embedding vectors.</returns>
        public abstract Task<List<List<float>>> EmbedBatchAsync(List<string> texts, string model, CancellationToken token = default);

        /// <summary>
        /// Retrieve the model's context length (in model-native tokens).
        /// Returns null if the information is unavailable.
        /// </summary>
        /// <param name="model">Model name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Context length or null.</returns>
        public virtual Task<int?> GetModelContextLengthAsync(string model, CancellationToken token = default)
        {
            return Task.FromResult<int?>(null);
        }

        /// <summary>
        /// Apply L2 normalization to an embedding vector.
        /// </summary>
        /// <param name="embeddings">Embedding vector to normalize.</param>
        /// <returns>L2-normalized embedding vector.</returns>
        public List<float> NormalizeL2(List<float> embeddings)
        {
            if (embeddings == null || embeddings.Count == 0) return embeddings ?? new List<float>();

            double magnitude = 0.0;
            foreach (float val in embeddings)
            {
                magnitude += (double)val * val;
            }
            magnitude = Math.Sqrt(magnitude);

            if (magnitude == 0.0) return embeddings;

            List<float> normalized = new List<float>(embeddings.Count);
            foreach (float val in embeddings)
            {
                normalized.Add((float)(val / magnitude));
            }

            return normalized;
        }

        /// <summary>
        /// Send an HTTP POST to an upstream endpoint and record the call details.
        /// </summary>
        /// <param name="url">Full URL to call.</param>
        /// <param name="content">HTTP content to send.</param>
        /// <param name="requestBodyJson">Request body as a JSON string (for recording).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An EmbeddingHttpResult containing the response and body.</returns>
        protected async Task<EmbeddingHttpResult> PostAndRecordAsync(
            string url, StringContent content, string requestBodyJson, CancellationToken token)
        {
            EmbeddingCallDetail detail = new EmbeddingCallDetail();
            detail.Url = url;
            detail.Method = "POST";
            detail.RequestBody = requestBodyJson;
            detail.TimestampUtc = DateTime.UtcNow;

            // Capture request headers
            Dictionary<string, string> reqHeaders = new Dictionary<string, string>();
            foreach (var header in _HttpClient.DefaultRequestHeaders)
            {
                reqHeaders[header.Key] = string.Join(", ", header.Value);
            }
            if (content.Headers.ContentType != null)
            {
                reqHeaders["Content-Type"] = content.Headers.ContentType.ToString();
            }
            detail.RequestHeaders = reqHeaders;

            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                HttpResponseMessage response = await _HttpClient.PostAsync(url, content, token).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

                sw.Stop();

                detail.StatusCode = (int)response.StatusCode;
                detail.ResponseTimeMs = sw.ElapsedMilliseconds;
                detail.ResponseBody = responseBody;
                detail.Success = response.IsSuccessStatusCode;

                // Capture response headers
                Dictionary<string, string> respHeaders = new Dictionary<string, string>();
                foreach (var header in response.Headers)
                {
                    respHeaders[header.Key] = string.Join(", ", header.Value);
                }
                foreach (var header in response.Content.Headers)
                {
                    respHeaders[header.Key] = string.Join(", ", header.Value);
                }
                detail.ResponseHeaders = respHeaders;

                CallDetails.Add(detail);

                EmbeddingHttpResult result = new EmbeddingHttpResult();
                result.Response = response;
                result.ResponseBody = responseBody;
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                detail.ResponseTimeMs = sw.ElapsedMilliseconds;
                detail.Success = false;
                detail.Error = ex.Message;
                CallDetails.Add(detail);
                throw;
            }
        }

        /// <summary>
        /// Dispose of HTTP client resources.
        /// </summary>
        public void Dispose()
        {
            _HttpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
