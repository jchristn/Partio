namespace Partio.Core.Settings
{
    using Partio.Core.Enums;

    /// <summary>
    /// Default inference (completion) endpoint configuration seeded for new tenants.
    /// </summary>
    public class DefaultInferenceEndpoint
    {
        private string _Name = "Default Inference";
        private string _Model = "llama3";
        private string _Endpoint = "http://localhost:11434";
        private ApiFormatEnum _ApiFormat = ApiFormatEnum.Ollama;
        private string? _ApiKey = null;

        /// <summary>
        /// Display name for the inference endpoint.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = value ?? throw new ArgumentNullException(nameof(Name));
        }

        /// <summary>
        /// Completion model name.
        /// </summary>
        public string Model
        {
            get => _Model;
            set => _Model = value ?? throw new ArgumentNullException(nameof(Model));
        }

        /// <summary>
        /// Inference endpoint URL.
        /// </summary>
        public string Endpoint
        {
            get => _Endpoint;
            set => _Endpoint = value ?? throw new ArgumentNullException(nameof(Endpoint));
        }

        /// <summary>
        /// API format for the inference endpoint.
        /// </summary>
        public ApiFormatEnum ApiFormat
        {
            get => _ApiFormat;
            set => _ApiFormat = value;
        }

        /// <summary>
        /// API key for the inference endpoint (nullable).
        /// </summary>
        public string? ApiKey
        {
            get => _ApiKey;
            set => _ApiKey = value;
        }
    }
}
