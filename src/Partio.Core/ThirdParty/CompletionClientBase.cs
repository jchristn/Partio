namespace Partio.Core.ThirdParty
{
    using System.Diagnostics;
    using Partio.Core.Exceptions;
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
        /// Maximum upstream provider request timeout in milliseconds.
        /// </summary>
        protected readonly int _MaximumTimeoutMs;

        /// <summary>
        /// Endpoint key used for concurrency limiting.
        /// </summary>
        protected readonly string _ConcurrencyKey;

        /// <summary>
        /// Maximum concurrent upstream provider requests for the endpoint.
        /// </summary>
        protected readonly int _MaxConcurrentRequests;

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
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        protected CompletionClientBase(
            string endpoint,
            string? apiKey,
            LoggingModule logging,
            int maximumTimeoutMs,
            string? concurrencyKey = null,
            int maxConcurrentRequests = 2)
        {
            _Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _ApiKey = apiKey;
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _MaximumTimeoutMs = maximumTimeoutMs <= 0 ? 1 : maximumTimeoutMs;
            _ConcurrencyKey = !string.IsNullOrWhiteSpace(concurrencyKey) ? concurrencyKey : endpoint;
            _MaxConcurrentRequests = maxConcurrentRequests < 1 ? 1 : maxConcurrentRequests;
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
        /// <param name="systemPrompt">Optional system prompt for instruction separation.</param>
        /// <returns>The completion text, or null on failure.</returns>
        public abstract Task<string?> GenerateCompletionAsync(
            string prompt,
            string model,
            int maxTokens,
            int timeoutMs,
            CancellationToken token = default,
            string? systemPrompt = null);

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
            int effectiveTimeoutMs = ClampTimeoutMs(timeoutMs);
            CompletionCallDetail detail = new CompletionCallDetail();
            detail.Url = url;
            detail.Method = "POST";
            detail.RequestBody = requestBodyJson;
            detail.TimestampUtc = DateTime.UtcNow;

            // Capture request headers
            Dictionary<string, string> reqHeaders = new Dictionary<string, string>();
            foreach (KeyValuePair<string, IEnumerable<string>> header in _HttpClient.DefaultRequestHeaders)
            {
                reqHeaders[header.Key] = string.Join(", ", header.Value);
            }
            if (content.Headers.ContentType != null)
            {
                reqHeaders["Content-Type"] = content.Headers.ContentType.ToString();
            }
            detail.RequestHeaders = reqHeaders;

            Stopwatch sw = Stopwatch.StartNew();
            IDisposable? concurrencyLease = null;

            try
            {
                concurrencyLease = AcquireRequestSlot();
                using (CancellationTokenSource timeoutCts = new CancellationTokenSource(effectiveTimeoutMs))
                using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token))
                {
                    HttpResponseMessage response = await _HttpClient.PostAsync(url, content, linkedCts.Token).ConfigureAwait(false);
                    string responseBody = await response.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);

                    sw.Stop();

                    detail.StatusCode = (int)response.StatusCode;
                    detail.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                    detail.ResponseBody = responseBody;
                    detail.Success = response.IsSuccessStatusCode;

                    // Capture response headers
                    Dictionary<string, string> respHeaders = new Dictionary<string, string>();
                    foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
                    {
                        respHeaders[header.Key] = string.Join(", ", header.Value);
                    }
                    foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
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
            }
            catch (ProviderConcurrencyLimitException ex)
            {
                sw.Stop();
                detail.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                detail.Success = false;
                detail.Error = ex.Message;
                CallDetails.Add(detail);
                throw;
            }
            catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
            {
                sw.Stop();
                detail.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                detail.Success = false;
                detail.Error = "Upstream inference provider request timed out after " + effectiveTimeoutMs + "ms.";
                CallDetails.Add(detail);
                throw new ProviderOperationTimeoutException(detail.Error, effectiveTimeoutMs, ex);
            }
            catch (Exception ex) when (IsTimeoutLike(ex))
            {
                sw.Stop();
                detail.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                detail.Success = false;
                detail.Error = "Upstream inference provider request timed out after " + effectiveTimeoutMs + "ms.";
                CallDetails.Add(detail);
                throw new ProviderOperationTimeoutException(detail.Error, effectiveTimeoutMs, ex);
            }
            catch (Exception ex)
            {
                sw.Stop();
                detail.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                detail.Success = false;
                detail.Error = ex.Message;
                CallDetails.Add(detail);
                throw;
            }
            finally
            {
                concurrencyLease?.Dispose();
            }
        }

        /// <summary>
        /// Clamp a requested timeout to a positive non-zero integer that does not exceed the endpoint maximum.
        /// </summary>
        protected int ClampTimeoutMs(int timeoutMs)
        {
            int positiveTimeoutMs = timeoutMs <= 0 ? 1 : timeoutMs;
            return Math.Min(positiveTimeoutMs, _MaximumTimeoutMs);
        }

        /// <summary>
        /// Acquire a slot from the endpoint concurrency limiter.
        /// </summary>
        protected IDisposable AcquireRequestSlot()
        {
            return ProviderConcurrencyLimiter.Acquire(_ConcurrencyKey, _MaxConcurrentRequests);
        }

        /// <summary>
        /// Record a rejected upstream call when the endpoint concurrency limiter denies the request.
        /// </summary>
        protected void RecordRejectedCall(string url, string method, string error)
        {
            Dictionary<string, string> reqHeaders = new Dictionary<string, string>();
            foreach (KeyValuePair<string, IEnumerable<string>> header in _HttpClient.DefaultRequestHeaders)
            {
                reqHeaders[header.Key] = string.Join(", ", header.Value);
            }

            CallDetails.Add(new CompletionCallDetail
            {
                Url = url,
                Method = method,
                RequestHeaders = reqHeaders,
                TimestampUtc = DateTime.UtcNow,
                Success = false,
                Error = error
            });
        }

        /// <summary>
        /// Determine whether an exception chain indicates a timeout.
        /// </summary>
        protected static bool IsTimeoutLike(Exception ex)
        {
            for (Exception? current = ex; current != null; current = current.InnerException)
            {
                if (current is TimeoutException)
                    return true;

                if (IsTimeoutMessage(current.Message))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Determine whether a message indicates a timeout.
        /// </summary>
        protected static bool IsTimeoutMessage(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            return message.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("task was canceled", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("operation was canceled", StringComparison.OrdinalIgnoreCase) >= 0;
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
