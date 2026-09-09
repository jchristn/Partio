namespace Partio.McpServer.Mcp
{
    using Partio.McpServer.Auth;
    using Partio.McpServer.Settings;
    using Partio.Sdk;
    using SyslogLogging;
    using Voltaic.Mcp;

    /// <summary>
    /// Constructs and runs the Partio MCP HTTP server: wires the Voltaic server to the tool catalog,
    /// the bearer authentication handler, and the permissive CORS settings, then starts listening.
    /// </summary>
    public class PartioMcpServer : IDisposable
    {
        private readonly McpServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly PartioClient _Client;
        private readonly string _Header = "[McpServer] ";
        private McpHttpServer? _Server;
        private bool _Disposed;

        /// <summary>
        /// Initialize a new PartioMcpServer.
        /// </summary>
        /// <param name="settings">MCP server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="client">Partio REST SDK client.</param>
        public PartioMcpServer(McpServerSettings settings, LoggingModule logging, PartioClient client)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>
        /// Start the MCP server and listen until the supplied token is cancelled.
        /// </summary>
        /// <param name="token">Cancellation token that stops the server when triggered.</param>
        /// <returns>A task that completes when the server is started.</returns>
        public async Task StartAsync(CancellationToken token)
        {
            _Server = new McpHttpServer(
                _Settings.McpHost,
                _Settings.McpPort,
                _Settings.McpRpcPath,
                _Settings.McpSsePath,
                true,
                _Settings.McpPath);

            _Server.ServerName = "partio-mcp";
            _Server.ServerVersion = typeof(PartioMcpServer).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            _Server.ServerInstructions = "Manage Partio embedding and completion endpoints (including MaxConcurrentRequests and MaxQueueDepth) and run summarize/chunk/embed inference.";

            // CORS: initialized to permissive defaults via McpCorsSettings; Voltaic answers the OPTIONS
            // preflight internally (preflight bypasses authentication) using the headers set here.
            _Server.EnableCors = _Settings.Cors.Enabled;
            _Server.CorsHeaders = _Settings.Cors.BuildHeaders();

            McpAuthenticationHandler auth = new McpAuthenticationHandler(_Settings, _Logging);
            _Server.AuthenticationHandler = auth.AuthenticateAsync;

            PartioMcpToolCatalog catalog = new PartioMcpToolCatalog(_Client, _Settings, _Logging);
            catalog.RegisterAll(_Server);

            await _Server.StartAsync(token).ConfigureAwait(false);

            string scheme = "http";
            _Logging.Info(_Header + "listening on " + scheme + "://" + _Settings.McpHost + ":" + _Settings.McpPort + _Settings.McpPath);
        }

        /// <summary>
        /// Dispose the underlying MCP server.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _Server?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
