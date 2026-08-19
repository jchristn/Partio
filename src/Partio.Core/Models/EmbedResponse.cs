namespace Partio.Core.Models
{
    /// <summary>
    /// Response body for an embed request: one embedding vector per input string, in order.
    /// </summary>
    public class EmbedResponse
    {
        /// <summary>
        /// Whether the embed request succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Result status code.
        /// </summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// Error text when the request fails.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Target embedding endpoint ID.
        /// </summary>
        public string? EndpointId { get; set; }

        /// <summary>
        /// Target model name.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Returned embedding vectors, one per input string in the same order.
        /// </summary>
        public List<List<float>> Embeddings { get; set; } = new List<List<float>>();

        /// <summary>
        /// Number of embedding vectors returned.
        /// </summary>
        public int Count { get; set; } = 0;

        /// <summary>
        /// Number of dimensions in each returned embedding (0 when none).
        /// </summary>
        public int Dimensions { get; set; } = 0;

        /// <summary>
        /// Whether L2 normalization was applied to the returned vectors.
        /// </summary>
        public bool L2Normalization { get; set; } = false;

        /// <summary>
        /// Overall request duration in milliseconds.
        /// </summary>
        public double ResponseTimeMs { get; set; } = 0;

        /// <summary>
        /// Related request-history entry ID when persisted.
        /// </summary>
        public string? RequestHistoryId { get; set; }

        /// <summary>
        /// Upstream embedding calls made by Partio for this request.
        /// </summary>
        public List<EmbeddingCallDetail> EmbeddingCalls { get; set; } = new List<EmbeddingCallDetail>();

        /// <summary>
        /// Resolved tokenization profile for the endpoint/model pair.
        /// </summary>
        public ResolvedTokenizationProfile? TokenizationProfile { get; set; }
    }
}
