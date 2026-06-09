namespace Partio.Core.Settings
{
    using Partio.Core.Enums;
    using Partio.Core.Models;

    /// <summary>
    /// Default embedding endpoint configuration seeded for new tenants.
    /// </summary>
    public class DefaultEmbeddingEndpoint
    {
        private string? _Name = "nomic-embed-text";
        private string _Model = "nomic-embed-text";
        private string _Endpoint = "http://localhost:11434";
        private ApiFormatEnum _ApiFormat = ApiFormatEnum.Ollama;
        private string? _ApiKey = null;
        private int _MaximumTimeoutMs = 60000;
        private int _MaxConcurrentRequests = 2;
        private EndpointTokenizationSettings? _Tokenization = null;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();

        /// <summary>
        /// Human-readable name for the endpoint.
        /// </summary>
        public string? Name
        {
            get => _Name;
            set => _Name = value;
        }

        /// <summary>
        /// Embedding model name.
        /// </summary>
        public string Model
        {
            get => _Model;
            set => _Model = value ?? throw new ArgumentNullException(nameof(Model));
        }

        /// <summary>
        /// Embedding endpoint URL.
        /// </summary>
        public string Endpoint
        {
            get => _Endpoint;
            set => _Endpoint = value ?? throw new ArgumentNullException(nameof(Endpoint));
        }

        /// <summary>
        /// API format for the embedding endpoint.
        /// </summary>
        public ApiFormatEnum ApiFormat
        {
            get => _ApiFormat;
            set => _ApiFormat = value;
        }

        /// <summary>
        /// API key for the embedding endpoint (nullable).
        /// </summary>
        public string? ApiKey
        {
            get => _ApiKey;
            set => _ApiKey = value;
        }

        /// <summary>
        /// Maximum upstream provider request timeout in milliseconds.
        /// </summary>
        public int MaximumTimeoutMs
        {
            get => _MaximumTimeoutMs;
            set => _MaximumTimeoutMs = value <= 0 ? 1 : value;
        }

        /// <summary>
        /// Maximum concurrent upstream provider requests allowed for the seeded endpoint.
        /// </summary>
        public int MaxConcurrentRequests
        {
            get => _MaxConcurrentRequests;
            set => _MaxConcurrentRequests = value < 1 ? 1 : value;
        }

        /// <summary>
        /// Optional tokenization override settings to seed onto the endpoint.
        /// </summary>
        public EndpointTokenizationSettings? Tokenization
        {
            get => _Tokenization;
            set => _Tokenization = value;
        }

        /// <summary>
        /// Labels to seed onto the default endpoint.
        /// </summary>
        public List<string> Labels
        {
            get => _Labels;
            set => _Labels = value ?? new List<string>();
        }

        /// <summary>
        /// Key-value tags to seed onto the default endpoint.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get => _Tags;
            set => _Tags = value ?? new Dictionary<string, string>();
        }
    }
}
