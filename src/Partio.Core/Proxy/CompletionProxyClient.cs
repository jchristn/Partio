namespace Partio.Core.Proxy
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Partio.Core.Exceptions;
    using Partio.Core.Models;
    using Partio.Core.ThirdParty;
    using SyslogLogging;

    /// <summary>
    /// Transparent HTTP passthrough client for the completion proxy. Forwards a caller's native provider
    /// request (OpenAI, Ollama, Gemini, or vLLM) to the endpoint's configured upstream and returns the
    /// upstream response for verbatim relay. Partio performs no body translation here: it injects the
    /// upstream API key, enforces per-endpoint concurrency and timeout, and streams the response back.
    /// </summary>
    public sealed class CompletionProxyClient
    {
        // Shared handler/client: automatic decompression is disabled so compressed upstream bodies are
        // relayed verbatim (with their Content-Encoding), and redirects are not followed so the caller
        // observes exactly what the upstream returned. Per-request timeouts are enforced with a linked
        // CancellationTokenSource rather than HttpClient.Timeout.
        private static readonly HttpClient _Http = CreateHttpClient();

        private readonly LoggingModule _Logging;

        // Hop-by-hop and framing headers that must not be copied from the inbound request to the upstream
        // request; HttpClient recomputes framing and manages the connection itself.
        private static readonly HashSet<string> _StripRequestHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Host", "Content-Length", "Connection", "Keep-Alive", "Proxy-Connection",
            "Transfer-Encoding", "TE", "Trailer", "Upgrade", "Expect", "Authorization"
        };

        /// <summary>
        /// Initialize a new <see cref="CompletionProxyClient"/>.
        /// </summary>
        /// <param name="logging">Logging module.</param>
        public CompletionProxyClient(LoggingModule logging)
        {
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        private static HttpClient CreateHttpClient()
        {
            SocketsHttpHandler handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.None,
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            };
            HttpClient client = new HttpClient(handler);
            client.Timeout = Timeout.InfiniteTimeSpan;
            return client;
        }

        /// <summary>
        /// Forward a proxied request to the endpoint's upstream provider and return the upstream response
        /// for relay. Acquires a per-endpoint concurrency lease (bounded by the effective timeout) before
        /// dispatching, and holds it — together with the HTTP response — inside the returned
        /// <see cref="ProxyForwardResult"/> until the caller disposes it.
        /// </summary>
        /// <param name="endpoint">Resolved completion endpoint (provides base URL, dialect, key, limits).</param>
        /// <param name="method">Inbound HTTP method (for example POST or GET).</param>
        /// <param name="subpath">Normalized native provider sub-path (already allow-list checked).</param>
        /// <param name="queryString">Original query string (with or without leading '?'), or null.</param>
        /// <param name="body">Raw inbound request body, or null for a bodyless request.</param>
        /// <param name="contentType">Inbound <c>Content-Type</c>, propagated to the upstream body.</param>
        /// <param name="inboundHeaders">Inbound request headers to selectively propagate.</param>
        /// <param name="timeoutMs">Upstream timeout in milliseconds; when &lt;= 0 the endpoint default is used.</param>
        /// <param name="token">Caller cancellation token.</param>
        /// <returns>The upstream response wrapped for verbatim relay.</returns>
        /// <exception cref="ProviderConcurrencyLimitException">The endpoint concurrency limit and queue are full.</exception>
        /// <exception cref="ProviderOperationTimeoutException">The upstream call (or concurrency wait) timed out.</exception>
        /// <exception cref="HttpRequestException">The upstream provider was unreachable or refused the connection.</exception>
        public async Task<ProxyForwardResult> ForwardAsync(
            CompletionEndpoint endpoint,
            string method,
            string subpath,
            string? queryString,
            byte[]? body,
            string? contentType,
            IReadOnlyDictionary<string, string> inboundHeaders,
            int timeoutMs,
            CancellationToken token)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            string url = ProxyPathPolicy.BuildUpstreamUrl(endpoint.Endpoint, subpath, queryString);
            int effectiveTimeoutMs = timeoutMs > 0 ? timeoutMs : endpoint.MaximumTimeoutMs;
            if (effectiveTimeoutMs <= 0) effectiveTimeoutMs = 60000;

            // 1. Acquire a concurrency slot, bounded by the effective timeout. A full queue throws
            //    ProviderConcurrencyLimitException (429); a wait that exceeds the timeout is surfaced as a
            //    gateway timeout (504), matching the buffered completion path.
            IDisposable lease;
            using (CancellationTokenSource acquireTimeoutCts = new CancellationTokenSource(effectiveTimeoutMs))
            using (CancellationTokenSource acquireLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, acquireTimeoutCts.Token))
            {
                try
                {
                    lease = await ProviderConcurrencyLimiter.AcquireAsync(
                        endpoint.Id, endpoint.MaxConcurrentRequests, endpoint.MaxQueueDepth, acquireLinkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    throw new ProviderOperationTimeoutException(
                        "Upstream inference provider request timed out after " + effectiveTimeoutMs + "ms while waiting for a concurrency slot.",
                        effectiveTimeoutMs);
                }
            }

            HttpResponseMessage? response = null;
            CancellationTokenSource? timeoutCts = null;
            CancellationTokenSource? linkedCts = null;
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(method), url);

                if (body != null && body.Length > 0 && MethodAllowsBody(method))
                {
                    ByteArrayContent content = new ByteArrayContent(body);
                    if (!string.IsNullOrEmpty(contentType))
                        content.Headers.TryAddWithoutValidation("Content-Type", contentType);
                    request.Content = content;
                }

                foreach (KeyValuePair<string, string> header in inboundHeaders)
                {
                    if (_StripRequestHeaders.Contains(header.Key)) continue;
                    if (header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase)) continue; // set on content above
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                // Inject the upstream API key (replacing any inbound Authorization), so the caller never
                // needs the provider credential and cannot override it.
                ProxyAuthHeader? auth = ProxyPathPolicy.ResolveAuthHeader(endpoint.ApiFormat, endpoint.ApiKey);
                if (auth != null)
                {
                    request.Headers.Remove(auth.Value.Name);
                    request.Headers.TryAddWithoutValidation(auth.Value.Name, auth.Value.Value);
                }

                timeoutCts = new CancellationTokenSource(effectiveTimeoutMs);
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

                response = await _Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token).ConfigureAwait(false);

                Stream bodyStream = await response.Content.ReadAsStreamAsync(linkedCts.Token).ConfigureAwait(false);
                string? respContentType = response.Content.Headers.ContentType?.ToString();
                long? contentLength = response.Content.Headers.ContentLength;
                bool isEventStream = respContentType != null
                    && respContentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase);

                ProxyForwardResult result = new ProxyForwardResult(response, lease, linkedCts, timeoutCts)
                {
                    StatusCode = (int)response.StatusCode,
                    ContentType = respContentType,
                    ContentLength = contentLength,
                    IsEventStream = isEventStream,
                    Headers = CollectResponseHeaders(response),
                    Body = bodyStream,
                    UpstreamUrl = url,
                    Method = method
                };

                // Ownership of response/lease/CTS has transferred to the result.
                return result;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                response?.Dispose();
                linkedCts?.Dispose();
                timeoutCts?.Dispose();
                lease.Dispose();
                throw new ProviderOperationTimeoutException(
                    "Upstream inference provider request timed out after " + effectiveTimeoutMs + "ms.",
                    effectiveTimeoutMs);
            }
            catch
            {
                response?.Dispose();
                linkedCts?.Dispose();
                timeoutCts?.Dispose();
                lease.Dispose();
                throw;
            }
        }

        private static Dictionary<string, string> CollectResponseHeaders(HttpResponseMessage response)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
                headers[header.Key] = string.Join(", ", header.Value);

            if (response.Content != null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
                    headers[header.Key] = string.Join(", ", header.Value);
            }

            return headers;
        }

        private static bool MethodAllowsBody(string method)
        {
            return !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase);
        }
    }
}
