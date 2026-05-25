namespace Partio.Core.ThirdParty
{
    using System.Diagnostics;
    using Partio.Core.Exceptions;
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
        /// Recorded details of HTTP calls made to upstream embedding endpoints.
        /// </summary>
        public List<EmbeddingCallDetail> CallDetails { get; } = new List<EmbeddingCallDetail>();

        /// <summary>
        /// Initialize a new EmbeddingClientBase.
        /// </summary>
        /// <param name="endpoint">Endpoint URL.</param>
        /// <param name="apiKey">API key (nullable).</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="maximumTimeoutMs">Maximum upstream provider request timeout in milliseconds.</param>
        /// <param name="concurrencyKey">Endpoint-specific concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent upstream provider requests.</param>
        protected EmbeddingClientBase(
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
        /// Retrieve runtime capabilities for a model, including tokenization metadata when available.
        /// </summary>
        /// <param name="model">Model name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Capability data or null.</returns>
        public virtual Task<EmbeddingModelCapabilities?> GetModelCapabilitiesAsync(string model, CancellationToken token = default)
        {
            return Task.FromResult<EmbeddingModelCapabilities?>(null);
        }

        /// <summary>
        /// Retrieve the model's context length (in model-native tokens).
        /// Returns null if the information is unavailable.
        /// </summary>
        public virtual async Task<int?> GetModelContextLengthAsync(string model, CancellationToken token = default)
        {
            EmbeddingModelCapabilities? capabilities = await GetModelCapabilitiesAsync(model, token).ConfigureAwait(false);
            return capabilities?.MaxInputTokens;
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
        /// <param name="purpose">Optional high-level purpose for the call.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An EmbeddingHttpResult containing the response and body.</returns>
        protected async Task<EmbeddingHttpResult> PostAndRecordAsync(
            string url, StringContent content, string requestBodyJson, string? purpose, CancellationToken token)
        {
            EmbeddingCallDetail detail = new EmbeddingCallDetail();
            detail.Purpose = purpose;
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
                using CancellationTokenSource timeoutCts = new CancellationTokenSource(_MaximumTimeoutMs);
                using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

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

                EmbeddingHttpResult result = new EmbeddingHttpResult();
                result.Response = response;
                result.ResponseBody = responseBody;
                return result;
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
                detail.Error = "Upstream embedding provider request timed out after " + _MaximumTimeoutMs + "ms.";
                CallDetails.Add(detail);
                throw new ProviderOperationTimeoutException(detail.Error, _MaximumTimeoutMs, ex);
            }
            catch (Exception ex) when (IsTimeoutLike(ex))
            {
                sw.Stop();
                detail.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                detail.Success = false;
                detail.Error = "Upstream embedding provider request timed out after " + _MaximumTimeoutMs + "ms.";
                CallDetails.Add(detail);
                throw new ProviderOperationTimeoutException(detail.Error, _MaximumTimeoutMs, ex);
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
        /// Acquire a slot from the endpoint concurrency limiter.
        /// </summary>
        protected IDisposable AcquireRequestSlot()
        {
            return ProviderConcurrencyLimiter.Acquire(_ConcurrencyKey, _MaxConcurrentRequests);
        }

        /// <summary>
        /// Record a rejected upstream call when the endpoint concurrency limiter denies the request.
        /// </summary>
        protected void RecordRejectedCall(string? purpose, string url, string method, string error)
        {
            Dictionary<string, string> reqHeaders = new Dictionary<string, string>();
            foreach (KeyValuePair<string, IEnumerable<string>> header in _HttpClient.DefaultRequestHeaders)
            {
                reqHeaders[header.Key] = string.Join(", ", header.Value);
            }

            CallDetails.Add(new EmbeddingCallDetail
            {
                Purpose = purpose,
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
