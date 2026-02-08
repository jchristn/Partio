namespace Partio.Server.Models
{
    using Partio.Core.Models;

    /// <summary>
    /// Result of processing a single semantic cell, including embedding call details.
    /// </summary>
    public class ProcessCellResult
    {
        /// <summary>
        /// The semantic cell response.
        /// </summary>
        public SemanticCellResponse Response { get; set; } = null!;

        /// <summary>
        /// Details of HTTP calls made to the upstream embedding endpoint.
        /// </summary>
        public List<EmbeddingCallDetail> EmbeddingCalls { get; set; } = new List<EmbeddingCallDetail>();
    }
}
