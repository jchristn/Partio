namespace Partio.McpServer.Settings
{
    /// <summary>
    /// Configuration for the Partio MCP server: how it binds, where Partio is, and how it authenticates.
    /// Loaded from a JSON file and overridable by environment variables.
    /// </summary>
    public class McpServerSettings
    {
        private string _McpHost = "localhost";
        private int _McpPort = 8500;
        private string _McpRpcPath = "/rpc";
        private string _McpSsePath = "/events";
        private string _McpPath = "/mcp";
        private string _PartioEndpoint = "http://127.0.0.1:8400";
        private string _PartioApiKey = "partioadmin";
        private List<string> _AdminApiKeys = new List<string>();
        private bool _RequireAuthentication = true;
        private int _LogLevel = 1;
        private int _MaxResults = 100;
        private McpCorsSettings _Cors = new McpCorsSettings();

        /// <summary>
        /// Hostname the MCP server binds to. Use "*" for all interfaces (requires elevation).
        /// </summary>
        /// <remarks>Default: localhost.</remarks>
        public string McpHost
        {
            get => _McpHost;
            set => _McpHost = string.IsNullOrWhiteSpace(value) ? "localhost" : value;
        }

        /// <summary>
        /// TCP port the MCP server listens on.
        /// </summary>
        /// <remarks>Default: 8500.</remarks>
        public int McpPort
        {
            get => _McpPort;
            set => _McpPort = value is > 0 and < 65536 ? value : 8500;
        }

        /// <summary>
        /// URL path for JSON-RPC requests.
        /// </summary>
        /// <remarks>Default: /rpc.</remarks>
        public string McpRpcPath
        {
            get => _McpRpcPath;
            set => _McpRpcPath = string.IsNullOrWhiteSpace(value) ? "/rpc" : value;
        }

        /// <summary>
        /// URL path for Server-Sent Events connections.
        /// </summary>
        /// <remarks>Default: /events.</remarks>
        public string McpSsePath
        {
            get => _McpSsePath;
            set => _McpSsePath = string.IsNullOrWhiteSpace(value) ? "/events" : value;
        }

        /// <summary>
        /// URL path for the MCP Streamable HTTP endpoint.
        /// </summary>
        /// <remarks>Default: /mcp.</remarks>
        public string McpPath
        {
            get => _McpPath;
            set => _McpPath = string.IsNullOrWhiteSpace(value) ? "/mcp" : value;
        }

        /// <summary>
        /// Base URL of the Partio REST server the MCP tools call.
        /// </summary>
        /// <remarks>Default: http://127.0.0.1:8400.</remarks>
        public string PartioEndpoint
        {
            get => _PartioEndpoint;
            set => _PartioEndpoint = string.IsNullOrWhiteSpace(value) ? "http://127.0.0.1:8400" : value;
        }

        /// <summary>
        /// Partio admin API key or bearer token used for outbound calls to Partio.
        /// </summary>
        /// <remarks>Default: partioadmin.</remarks>
        public string PartioApiKey
        {
            get => _PartioApiKey;
            set => _PartioApiKey = value ?? string.Empty;
        }

        /// <summary>
        /// Bearer tokens accepted on inbound MCP requests. When empty, the outbound
        /// <see cref="PartioApiKey"/> is accepted (single-key gateway).
        /// </summary>
        public List<string> AdminApiKeys
        {
            get => _AdminApiKeys;
            set => _AdminApiKeys = value ?? new List<string>();
        }

        /// <summary>
        /// Whether inbound MCP requests must present a valid bearer token.
        /// </summary>
        /// <remarks>Default: true. Partio endpoints are tenant-scoped and carry provider keys, so a bearer is required by default.</remarks>
        public bool RequireAuthentication
        {
            get => _RequireAuthentication;
            set => _RequireAuthentication = value;
        }

        /// <summary>
        /// Minimum syslog severity to emit (0 = Debug .. 5 = Emergency).
        /// </summary>
        /// <remarks>Default: 1 (Info).</remarks>
        public int LogLevel
        {
            get => _LogLevel;
            set => _LogLevel = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Maximum number of results any enumerate tool may return; larger requests are clamped to this value.
        /// </summary>
        /// <remarks>Default: 100.</remarks>
        public int MaxResults
        {
            get => _MaxResults;
            set => _MaxResults = value < 1 ? 1 : value;
        }

        /// <summary>
        /// CORS settings applied to the MCP webserver. Permissive by default.
        /// </summary>
        public McpCorsSettings Cors
        {
            get => _Cors;
            set => _Cors = value ?? new McpCorsSettings();
        }

        /// <summary>
        /// Return the set of bearer tokens accepted on inbound requests, falling back to the
        /// outbound Partio API key when no explicit admin keys are configured.
        /// </summary>
        /// <returns>The list of accepted bearer tokens.</returns>
        public List<string> ResolveAcceptedTokens()
        {
            if (_AdminApiKeys.Count > 0)
                return _AdminApiKeys;

            List<string> fallback = new List<string>();
            if (!string.IsNullOrEmpty(_PartioApiKey))
                fallback.Add(_PartioApiKey);
            return fallback;
        }
    }
}
