namespace Partio.Core.Settings
{
    /// <summary>
    /// CORS (Cross-Origin Resource Sharing) settings.
    /// </summary>
    public class CorsSettings
    {
        private bool _Enabled = true;
        private string _AllowedOrigins = "*";
        private string _AllowedMethods = "GET, POST, PUT, DELETE, HEAD, OPTIONS";
        private string _AllowedHeaders = "Content-Type, Authorization, X-Requested-With";
        private string _ExposedHeaders = "";
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
        /// <remarks>Default: Content-Type, Authorization, X-Requested-With.</remarks>
        public string AllowedHeaders
        {
            get => _AllowedHeaders;
            set => _AllowedHeaders = value ?? "Content-Type, Authorization, X-Requested-With";
        }

        /// <summary>
        /// Response headers exposed to the client.
        /// </summary>
        /// <remarks>Default: empty.</remarks>
        public string ExposedHeaders
        {
            get => _ExposedHeaders;
            set => _ExposedHeaders = value ?? "";
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
        /// Cannot be true when AllowedOrigins is "*".
        /// </summary>
        /// <remarks>Default: false.</remarks>
        public bool AllowCredentials
        {
            get => _AllowCredentials;
            set => _AllowCredentials = value;
        }
    }
}
