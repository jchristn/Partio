namespace Test.Shared
{
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;

    /// <summary>
    /// Scriptable in-process HTTP server for provider wire tests. Each request is recorded (method, path and
    /// query, headers, body) and answered by the first registered route whose method and path match; unmatched
    /// requests receive 404. Used for providers without a dedicated fake (Azure OpenAI, Anthropic, Cohere,
    /// Bedrock, Vertex AI, TEI, VoyageAI) and for the Google OAuth token exchange.
    /// </summary>
    public sealed class ScriptedProviderServer : IDisposable
    {
        private readonly HttpListener _Listener = new HttpListener();
        private readonly CancellationTokenSource _Cancellation = new CancellationTokenSource();
        private readonly Task _AcceptLoopTask;
        private readonly List<Route> _Routes = new List<Route>();
        private readonly object _RoutesLock = new object();

        /// <summary>Base URL of the server (for example http://127.0.0.1:54321), without a trailing slash.</summary>
        public string BaseUrl { get; }

        /// <summary>Every request received, in arrival order.</summary>
        public ConcurrentQueue<RecordedRequest> Requests { get; } = new ConcurrentQueue<RecordedRequest>();

        /// <summary>
        /// Initialize and start a new ScriptedProviderServer on a free loopback port.
        /// </summary>
        public ScriptedProviderServer()
        {
            int port = GetAvailablePort();
            BaseUrl = "http://127.0.0.1:" + port;
            _Listener.Prefixes.Add(BaseUrl + "/");
            _Listener.Start();
            _AcceptLoopTask = Task.Run(() => AcceptLoopAsync(_Cancellation.Token));
        }

        /// <summary>
        /// Register a JSON response for requests whose method matches and whose path (without query) satisfies the predicate.
        /// </summary>
        /// <param name="method">HTTP method, for example POST.</param>
        /// <param name="pathMatches">Predicate over the request path (without query string).</param>
        /// <param name="json">Response body.</param>
        /// <param name="statusCode">Response status code.</param>
        /// <returns>This server, for chaining.</returns>
        public ScriptedProviderServer On(string method, Func<string, bool> pathMatches, string json, int statusCode = 200)
        {
            lock (_RoutesLock)
                _Routes.Add(new Route(method, pathMatches, json, statusCode));
            return this;
        }

        /// <summary>
        /// Register a JSON response for an exact path.
        /// </summary>
        /// <param name="method">HTTP method, for example POST.</param>
        /// <param name="path">Exact request path (without query string).</param>
        /// <param name="json">Response body.</param>
        /// <param name="statusCode">Response status code.</param>
        /// <returns>This server, for chaining.</returns>
        public ScriptedProviderServer On(string method, string path, string json, int statusCode = 200)
        {
            return On(method, p => string.Equals(p, path, StringComparison.Ordinal), json, statusCode);
        }

        /// <summary>
        /// Return the recorded requests whose path (without query) satisfies the predicate.
        /// </summary>
        /// <param name="pathMatches">Predicate over the request path.</param>
        /// <returns>Matching requests in arrival order.</returns>
        public List<RecordedRequest> RequestsTo(Func<string, bool> pathMatches)
        {
            return Requests.Where(r => pathMatches(r.Path)).ToList();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _Cancellation.Cancel();
            try { _Listener.Stop(); } catch { }
            try { _AcceptLoopTask.Wait(TimeSpan.FromSeconds(2)); } catch { }
            _Listener.Close();
            _Cancellation.Dispose();
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _Listener.GetContextAsync().ConfigureAwait(false);
                }
                catch
                {
                    return;
                }

                _ = Task.Run(() => HandleAsync(ctx), token);
            }
        }

        private async Task HandleAsync(HttpListenerContext ctx)
        {
            try
            {
                string body;
                using (StreamReader reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                    body = await reader.ReadToEndAsync().ConfigureAwait(false);

                Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string? key in ctx.Request.Headers.AllKeys)
                {
                    if (key != null) headers[key] = ctx.Request.Headers[key] ?? string.Empty;
                }

                string path = ctx.Request.Url?.AbsolutePath ?? "/";
                string query = ctx.Request.Url?.Query ?? string.Empty;
                RecordedRequest recorded = new RecordedRequest(ctx.Request.HttpMethod, Uri.UnescapeDataString(path), query, headers, body);
                Requests.Enqueue(recorded);

                Route? route;
                lock (_RoutesLock)
                    route = _Routes.FirstOrDefault(r => r.Method.Equals(recorded.Method, StringComparison.OrdinalIgnoreCase) && r.PathMatches(recorded.Path));

                int status = route?.StatusCode ?? 404;
                string json = route?.Json ?? "{\"error\":\"no scripted route for " + recorded.Method + " " + recorded.Path + "\"}";
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                ctx.Response.StatusCode = status;
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = bytes.Length;
                await ctx.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
                ctx.Response.Close();
            }
            catch
            {
                try { ctx.Response.Abort(); } catch { }
            }
        }

        private static int GetAvailablePort()
        {
            TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        private sealed record Route(string Method, Func<string, bool> PathMatches, string Json, int StatusCode);
    }

    /// <summary>
    /// A request received by <see cref="ScriptedProviderServer"/>.
    /// </summary>
    /// <param name="Method">HTTP method.</param>
    /// <param name="Path">Unescaped request path without the query string.</param>
    /// <param name="Query">Raw query string including the leading '?', or empty.</param>
    /// <param name="Headers">Request headers (case-insensitive keys).</param>
    /// <param name="Body">Request body.</param>
    public sealed record RecordedRequest(string Method, string Path, string Query, IReadOnlyDictionary<string, string> Headers, string Body)
    {
        /// <summary>Header value, or null when absent.</summary>
        /// <param name="name">Header name.</param>
        /// <returns>The value, or null.</returns>
        public string? Header(string name)
        {
            return Headers.TryGetValue(name, out string? value) ? value : null;
        }
    }
}
