namespace Partio.Core.ThirdParty
{
    using System.Diagnostics;
    using Partio.Core.Models;
    using SyslogLogging;

    /// <summary>
    /// Abstract base class for completion/generation API clients.
    /// </summary>
    public abstract class CompletionClientBase : IDisposable
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
        protected string _Header = "[CompletionClient] ";

        /// <summary>
        /// Recorded details of HTTP calls made to upstream completion endpoints.
        /// </summary>
        public List<CompletionCallDetail> CallDetails { get; } = new List<CompletionCallDetail>();

        /// <summary>
        /// Initialize a new CompletionClientBase.
        /// </summary>
        /// <param name="endpoint">Endpoint URL.</param>
        /// <param name="apiKey">API key (nullable).</param>
        /// <param name="logging">Logging module.</param>
        protected CompletionClientBase(string endpoint, string? apiKey, LoggingModule logging)
        {
            _Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _ApiKey = apiKey;
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _HttpClient = new HttpClient();
        }

        /// <summary>
        /// Generate a completion for the given prompt.
        /// </summary>
        /// <param name="prompt">Input prompt.</param>
        /// <param name="model">Model name.</param>
        /// <param name="maxTokens">Maximum tokens to generate.</param>
        /// <param name="timeoutMs">Timeout in milliseconds.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The completion text, or null on failure.</returns>
        public abstract Task<string?> GenerateCompletionAsync(
            string prompt,
            string model,
            int maxTokens,
            int timeoutMs,
            CancellationToken token = default);

        /// <summary>
        /// Send an HTTP POST to an upstream endpoint and record the call details.
        /// </summary>
        /// <param name="url">Full URL to call.</param>
        /// <param name="content">HTTP content to send.</param>
        /// <param name="requestBodyJson">Request body as a JSON string (for recording).</param>
        /// <param name="timeoutMs">Timeout in milliseconds.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A CompletionHttpResult containing the response and body.</returns>
        protected async Task<CompletionHttpResult> PostAndRecordAsync(
            string url, StringContent content, string requestBodyJson, int timeoutMs, CancellationToken token)
        {
            CompletionCallDetail detail = new CompletionCallDetail();
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
                using CancellationTokenSource timeoutCts = new CancellationTokenSource(timeoutMs);
                using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

                HttpResponseMessage response = await _HttpClient.PostAsync(url, content, linkedCts.Token).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);

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

                CompletionHttpResult result = new CompletionHttpResult();
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
