namespace Partio.Server.Models
{
    using Partio.Core.Models;

    /// <summary>
    /// Result of processing a single semantic cell, including embedding and completion call details.
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

        /// <summary>
        /// Details of HTTP calls made to the upstream completion endpoint for summarization.
        /// </summary>
        public List<CompletionCallDetail> CompletionCalls { get; set; } = new List<CompletionCallDetail>();
    }
}
