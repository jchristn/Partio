namespace Partio.Core.Models
{
    /// <summary>
    /// Request body for exercising a configured embedding endpoint through Partio.
    /// </summary>
    public class EndpointExplorerEmbeddingRequest
    {
        /// <summary>
        /// Target embedding endpoint ID.
        /// </summary>
        public string EndpointId { get; set; } = string.Empty;

        /// <summary>
        /// Input text to embed.
        /// </summary>
        public string Input { get; set; } = string.Empty;

        /// <summary>
        /// Whether to apply L2 normalization to the returned vector.
        /// </summary>
        public bool L2Normalization { get; set; } = false;
    }
}
