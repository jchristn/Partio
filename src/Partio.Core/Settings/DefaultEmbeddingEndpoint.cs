namespace Partio.Core.Settings
{
    using Partio.Core.Enums;

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
    }
}
