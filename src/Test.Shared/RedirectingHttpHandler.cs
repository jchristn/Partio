namespace Test.Shared
{
    using System.Collections.Concurrent;
    using System.Net.Http;

    /// <summary>
    /// Test transport that records each outgoing request and rewrites its scheme/host/port to a local fake
    /// server. Used for providers whose host is fixed (Ollama Cloud) so they can be exercised in-process.
    /// </summary>
    public sealed class RedirectingHttpHandler : DelegatingHandler
    {
        private readonly Uri _Target;

        /// <summary>Original (pre-rewrite) request URIs, in send order.</summary>
        public ConcurrentQueue<Uri> RequestedUris { get; } = new ConcurrentQueue<Uri>();

        /// <summary>Authorization header values, in send order (null when absent).</summary>
        public ConcurrentQueue<string?> Authorizations { get; } = new ConcurrentQueue<string?>();

        /// <summary>
        /// Initialize a new RedirectingHttpHandler.
        /// </summary>
        /// <param name="targetBaseUrl">Base URL of the local fake server that receives every request.</param>
        public RedirectingHttpHandler(string targetBaseUrl)
            : base(new SocketsHttpHandler())
        {
            _Target = new Uri(targetBaseUrl);
        }

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri original = request.RequestUri!;
            RequestedUris.Enqueue(original);
            Authorizations.Enqueue(request.Headers.Authorization?.ToString());

            UriBuilder rewritten = new UriBuilder(original)
            {
                Scheme = _Target.Scheme,
                Host = _Target.Host,
                Port = _Target.Port
            };
            request.RequestUri = rewritten.Uri;
            return base.SendAsync(request, cancellationToken);
        }
    }
}
