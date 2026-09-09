namespace Partio.McpServer.Settings
{
    /// <summary>
    /// CORS (Cross-Origin Resource Sharing) settings for the Partio MCP server.
    /// Initialized by default to permissive values so browser-based MCP clients work out of the box.
    /// </summary>
    public class McpCorsSettings
    {
        private bool _Enabled = true;
        private string _AllowedOrigins = "*";
        private string _AllowedMethods = "GET, POST, PUT, DELETE, HEAD, OPTIONS";
        private string _AllowedHeaders = "Content-Type, Authorization, X-Requested-With, Mcp-Session-Id";
        private string _ExposedHeaders = "Mcp-Session-Id";
        private int _MaxAgeSeconds = 86400;
        private bool _AllowCredentials = false;

        /// <summary>
        /// Whether CORS is enabled.
        /// </summary>
        /// <remarks>Default: true.</remarks>
        public bool Enabled
        {
            get => _Enabled;
            set => _Enabled = value;
        }

        /// <summary>
        /// Allowed origins. Use "*" to allow all origins.
        /// </summary>
        /// <remarks>Default: *.</remarks>
        public string AllowedOrigins
        {
            get => _AllowedOrigins;
            set => _AllowedOrigins = value ?? "*";
        }

        /// <summary>
        /// Allowed HTTP methods.
        /// </summary>
        /// <remarks>Default: GET, POST, PUT, DELETE, HEAD, OPTIONS.</remarks>
        public string AllowedMethods
        {
            get => _AllowedMethods;
            set => _AllowedMethods = value ?? "GET, POST, PUT, DELETE, HEAD, OPTIONS";
        }

        /// <summary>
        /// Allowed request headers.
        /// </summary>
        /// <remarks>Default: Content-Type, Authorization, X-Requested-With, Mcp-Session-Id.</remarks>
        public string AllowedHeaders
        {
            get => _AllowedHeaders;
            set => _AllowedHeaders = value ?? "Content-Type, Authorization, X-Requested-With, Mcp-Session-Id";
        }

        /// <summary>
        /// Response headers exposed to the client.
        /// </summary>
        /// <remarks>Default: Mcp-Session-Id.</remarks>
        public string ExposedHeaders
        {
            get => _ExposedHeaders;
            set => _ExposedHeaders = value ?? string.Empty;
        }

        /// <summary>
        /// How long the preflight response can be cached, in seconds.
        /// </summary>
        /// <remarks>Default: 86400 (24 hours).</remarks>
        public int MaxAgeSeconds
        {
            get => _MaxAgeSeconds;
            set => _MaxAgeSeconds = value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MaxAgeSeconds), "MaxAgeSeconds must be non-negative.");
        }

        /// <summary>
        /// Whether to allow credentials (cookies, authorization headers).
        /// Cannot be true when <see cref="AllowedOrigins"/> is "*".
        /// </summary>
        /// <remarks>Default: false.</remarks>
        public bool AllowCredentials
        {
            get => _AllowCredentials;
            set => _AllowCredentials = value;
        }

        /// <summary>
        /// Build the set of CORS response headers implied by these settings, used both on the
        /// preflight (OPTIONS) response and on normal responses.
        /// </summary>
        /// <returns>An ordered dictionary of header name to value.</returns>
        public Dictionary<string, string> BuildHeaders()
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            headers["Access-Control-Allow-Origin"] = _AllowedOrigins;
            headers["Access-Control-Allow-Methods"] = _AllowedMethods;
            headers["Access-Control-Allow-Headers"] = _AllowedHeaders;
            headers["Access-Control-Max-Age"] = _MaxAgeSeconds.ToString();
            if (!string.IsNullOrEmpty(_ExposedHeaders))
                headers["Access-Control-Expose-Headers"] = _ExposedHeaders;
            if (_AllowCredentials)
                headers["Access-Control-Allow-Credentials"] = "true";
            return headers;
        }
    }
}
