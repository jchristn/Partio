namespace Partio.Core.Models
{
    /// <summary>
    /// Configuration for embedding generation.
    /// </summary>
    public class EmbeddingConfiguration
    {
        private string _EmbeddingEndpointId = string.Empty;
        private string? _Model = null;
        private bool _L2Normalization = false;

        /// <summary>
        /// Embedding endpoint ID (required — previously came from URL path).
        /// </summary>
        public string EmbeddingEndpointId
        {
            get => _EmbeddingEndpointId;
            set => _EmbeddingEndpointId = value ?? throw new ArgumentNullException(nameof(EmbeddingEndpointId));
        }

        /// <summary>
        /// Embedding model name (e.g. "all-minilm", "text-embedding-3-small").
        /// Optional override for the model configured on the endpoint.
        /// </summary>
        public string? Model
        {
            get => _Model;
            set => _Model = value;
        }

        /// <summary>
        /// Whether to apply L2 normalization to embeddings.
        /// </summary>
        /// <remarks>Default: false.</remarks>
        public bool L2Normalization
        {
            get => _L2Normalization;
            set => _L2Normalization = value;
        }
    }
}
