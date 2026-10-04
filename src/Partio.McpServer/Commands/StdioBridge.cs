namespace Partio.McpServer.Commands
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using Partio.McpServer.Settings;

    /// <summary>
    /// A minimal stdio&lt;-&gt;HTTP JSON-RPC bridge. Reads newline-delimited JSON-RPC messages from stdin,
    /// forwards each to the running MCP server's JSON-RPC endpoint over HTTP, and writes the response to stdout.
    /// This lets harnesses that prefer stdio (or that cannot send an Authorization header) talk to the HTTP server.
    /// The bridge carries the <c>MCP-Session-Id</c> that <c>initialize</c> returns on every later request, because
    /// the server (Voltaic 2.1.4+) runs a sessionless <c>/rpc</c> call on a fresh, uninitialized connection.
    /// </summary>
    public static class StdioBridge
    {
        private const string SessionHeader = "MCP-Session-Id";

        /// <summary>
        /// Run the stdio bridge until stdin is closed.
        /// </summary>
        /// <param name="settings">MCP server settings describing where to forward requests.</param>
        /// <returns>Process exit code.</returns>
        public static async Task<int> RunAsync(McpServerSettings settings)
        {
            string host = settings.McpHost == "*" || string.IsNullOrWhiteSpace(settings.McpHost) ? "127.0.0.1" : settings.McpHost;
            string url = "http://" + host + ":" + settings.McpPort + settings.McpRpcPath;

            string bearer = ResolveBearer(settings);

            using HttpClient http = new HttpClient();
            if (!string.IsNullOrEmpty(bearer))
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

            using TextReader stdin = Console.In;
            using TextWriter stdout = Console.Out;

            string? sessionId = null;
            string? line;
            while ((line = await stdin.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                string responseBody;
                try
                {
                    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Content = new StringContent(line, Encoding.UTF8, "application/json");
                    if (sessionId != null)
                        request.Headers.TryAddWithoutValidation(SessionHeader, sessionId);

                    using HttpResponseMessage response = await http.SendAsync(request).ConfigureAwait(false);
                    responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    // A successful initialize issues the session; an expired or unknown session is 404, after
                    // which the harness re-initializes and the next initialize issues a new one.
                    if (response.Headers.TryGetValues(SessionHeader, out IEnumerable<string>? issued))
                        sessionId = issued.FirstOrDefault() ?? sessionId;
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                        sessionId = null;
                }
                catch (Exception ex)
                {
                    responseBody = "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32000,\"message\":\"stdio bridge transport error: "
                        + ex.Message.Replace("\"", "'") + "\"},\"id\":null}";
                }

                if (!string.IsNullOrEmpty(responseBody))
                {
                    await stdout.WriteLineAsync(responseBody.Replace("\r", string.Empty).Replace("\n", string.Empty)).ConfigureAwait(false);
                    await stdout.FlushAsync().ConfigureAwait(false);
                }
            }

            return 0;
        }

        private static string ResolveBearer(McpServerSettings settings)
        {
            // The stdio bridge injects a bearer on behalf of harnesses that cannot send an Authorization
            // header; it uses the configured Partio API key, which Partio validates like any other token.
            return settings.PartioApiKey;
        }
    }
}
