namespace Partio.McpServer.Commands
{
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using Partio.McpServer.Settings;

    /// <summary>
    /// A minimal stdio&lt;-&gt;HTTP JSON-RPC bridge. Reads newline-delimited JSON-RPC messages from stdin,
    /// forwards each to the running MCP server's JSON-RPC endpoint over HTTP, and writes the response to stdout.
    /// This lets harnesses that prefer stdio (or that cannot send an Authorization header) talk to the HTTP server.
    /// </summary>
    public static class StdioBridge
    {
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

            string? line;
            while ((line = await stdin.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                string responseBody;
                try
                {
                    using StringContent content = new StringContent(line, Encoding.UTF8, "application/json");
                    using HttpResponseMessage response = await http.PostAsync(url, content).ConfigureAwait(false);
                    responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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
            List<string> accepted = settings.ResolveAcceptedTokens();
            return accepted.Count > 0 ? accepted[0] : settings.PartioApiKey;
        }
    }
}
