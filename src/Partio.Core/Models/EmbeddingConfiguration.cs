namespace Partio.Core.Models
{
    /// <summary>
    /// Configuration for embedding generation.
    /// </summary>
    public class EmbeddingConfiguration
    {
        private string _Model = string.Empty;
        private bool _L2Normalization = false;

        /// <summary>
        /// Embedding model name (e.g. "all-minilm", "text-embedding-3-small").
        /// </summary>
        public string Model
        {
            get => _Model;
            set => _Model = !string.IsNullOrEmpty(value)
                ? value
                : throw new ArgumentException("Model must not be null or empty.", nameof(Model));
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
