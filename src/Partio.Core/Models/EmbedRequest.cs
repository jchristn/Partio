namespace Partio.Core.Models
{
    /// <summary>
    /// Request body for embedding one or more input strings through a configured embedding endpoint,
    /// without chunking. Each input is embedded as-is and returned in order.
    /// </summary>
    public class EmbedRequest
    {
        /// <summary>
        /// Target embedding endpoint ID.
        /// </summary>
        public string EndpointId { get; set; } = string.Empty;

        /// <summary>
        /// Input strings to embed, in order.
        /// </summary>
        public List<string> Input { get; set; } = new List<string>();

        /// <summary>
        /// Whether to apply L2 normalization to each returned vector.
        /// </summary>
        public bool L2Normalization { get; set; } = false;
    }
}
