namespace Partio.Core.Settings
{
    /// <summary>
    /// Root settings container for the Partio server.
    /// </summary>
    public class ServerSettings
    {
        private RestSettings _Rest = new RestSettings();
        private DatabaseSettings _Database = new DatabaseSettings();
        private LoggingSettings _Logging = new LoggingSettings();
        private DebugSettings _Debug = new DebugSettings();
        private RequestHistorySettings _RequestHistory = new RequestHistorySettings();
        private List<string> _AdminApiKeys = new List<string> { "partioadmin" };
        private List<DefaultEmbeddingEndpoint> _DefaultEmbeddingEndpoints = new List<DefaultEmbeddingEndpoint>();
        private List<DefaultInferenceEndpoint> _DefaultInferenceEndpoints = new List<DefaultInferenceEndpoint>();

        /// <summary>
        /// REST server settings.
        /// </summary>
        public RestSettings Rest
        {
            get => _Rest;
            set => _Rest = value ?? throw new ArgumentNullException(nameof(Rest));
        }

        /// <summary>
        /// Database settings.
        /// </summary>
        public DatabaseSettings Database
        {
            get => _Database;
            set => _Database = value ?? throw new ArgumentNullException(nameof(Database));
        }

        /// <summary>
        /// Logging settings.
        /// </summary>
        public LoggingSettings Logging
        {
            get => _Logging;
            set => _Logging = value ?? throw new ArgumentNullException(nameof(Logging));
        }

        /// <summary>
        /// Debug settings.
        /// </summary>
        public DebugSettings Debug
        {
            get => _Debug;
            set => _Debug = value ?? throw new ArgumentNullException(nameof(Debug));
        }

        /// <summary>
        /// Request history settings.
        /// </summary>
        public RequestHistorySettings RequestHistory
        {
            get => _RequestHistory;
            set => _RequestHistory = value ?? throw new ArgumentNullException(nameof(RequestHistory));
        }

        /// <summary>
        /// List of admin API keys for global admin access.
        /// </summary>
        /// <remarks>Default: ["partioadmin"].</remarks>
        public List<string> AdminApiKeys
        {
            get => _AdminApiKeys;
            set => _AdminApiKeys = value ?? new List<string>();
        }

        /// <summary>
        /// Default embedding endpoints seeded for new tenants.
        /// </summary>
        public List<DefaultEmbeddingEndpoint> DefaultEmbeddingEndpoints
        {
            get => _DefaultEmbeddingEndpoints;
            set => _DefaultEmbeddingEndpoints = value ?? new List<DefaultEmbeddingEndpoint>();
        }

        /// <summary>
        /// Default inference (completion) endpoints seeded for new tenants.
        /// </summary>
        public List<DefaultInferenceEndpoint> DefaultInferenceEndpoints
        {
            get => _DefaultInferenceEndpoints;
            set => _DefaultInferenceEndpoints = value ?? new List<DefaultInferenceEndpoint>();
        }
    }
}
