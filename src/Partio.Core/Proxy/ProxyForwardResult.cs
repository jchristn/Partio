namespace Partio.Core.Proxy
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The result of forwarding a proxied request to an upstream provider. Exposes the upstream status,
    /// headers, content type, and a readable body stream so the caller can relay the response verbatim.
    /// The upstream response body is not buffered here — the caller reads <see cref="Body"/> (buffering
    /// or chunk-relaying as appropriate) and then disposes this instance, which releases the concurrency
    /// lease and the underlying HTTP response together.
    /// </summary>
    public sealed class ProxyForwardResult : IAsyncDisposable
    {
        private readonly HttpResponseMessage _Response;
        private readonly IDisposable _ConcurrencyLease;
        private readonly IDisposable? _LinkedCts;
        private readonly IDisposable? _TimeoutCts;
        private bool _Disposed;

        /// <summary>Upstream HTTP status code, relayed to the caller verbatim.</summary>
        public int StatusCode { get; init; }

        /// <summary>Upstream <c>Content-Type</c> header value, if present.</summary>
        public string? ContentType { get; init; }

        /// <summary>Upstream <c>Content-Length</c>, if the provider declared one; null for streamed responses.</summary>
        public long? ContentLength { get; init; }

        /// <summary>Whether the upstream response is a server-sent-event stream (<c>text/event-stream</c>).</summary>
        public bool IsEventStream { get; init; }

        /// <summary>Upstream response headers to consider for passthrough to the caller.</summary>
        public Dictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Readable upstream response body stream. Not buffered; read once.</summary>
        public Stream Body { get; init; } = Stream.Null;

        /// <summary>The absolute upstream URL the request was forwarded to (for history/diagnostics).</summary>
        public string UpstreamUrl { get; init; } = string.Empty;

        /// <summary>The HTTP method used for the upstream request (for history/diagnostics).</summary>
        public string Method { get; init; } = string.Empty;

        /// <summary>
        /// Initialize a new <see cref="ProxyForwardResult"/> that owns the upstream response, the concurrency
        /// lease, and the cancellation sources bounding the upstream call. All are released on disposal.
        /// </summary>
        /// <param name="response">The upstream HTTP response message.</param>
        /// <param name="concurrencyLease">The per-endpoint concurrency lease held for the duration of the relay.</param>
        /// <param name="linkedCts">The linked cancellation source bounding the upstream read, or null.</param>
        /// <param name="timeoutCts">The timeout cancellation source bounding the upstream read, or null.</param>
        public ProxyForwardResult(
            HttpResponseMessage response,
            IDisposable concurrencyLease,
            IDisposable? linkedCts,
            IDisposable? timeoutCts)
        {
            _Response = response ?? throw new ArgumentNullException(nameof(response));
            _ConcurrencyLease = concurrencyLease ?? throw new ArgumentNullException(nameof(concurrencyLease));
            _LinkedCts = linkedCts;
            _TimeoutCts = timeoutCts;
        }

        /// <summary>
        /// Release the upstream response, the concurrency lease, and the cancellation sources. Safe to call
        /// multiple times.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            if (_Disposed) return ValueTask.CompletedTask;
            _Disposed = true;

            try { _Response.Dispose(); } catch { /* best effort */ }
            try { _ConcurrencyLease.Dispose(); } catch { /* best effort */ }
            try { _LinkedCts?.Dispose(); } catch { /* best effort */ }
            try { _TimeoutCts?.Dispose(); } catch { /* best effort */ }

            return ValueTask.CompletedTask;
        }
    }
}
