namespace Partio.Core.Models
{
    /// <summary>
    /// Outbound response body for a processed semantic cell.
    /// </summary>
    public class SemanticCellResponse
    {
        private string _Text = string.Empty;
        private List<ChunkResult> _Chunks = new List<ChunkResult>();

        /// <summary>
        /// Original full input text.
        /// </summary>
        public string Text
        {
            get => _Text;
            set => _Text = value ?? string.Empty;
        }

        /// <summary>
        /// List of chunk results with text and embeddings.
        /// </summary>
        public List<ChunkResult> Chunks
        {
            get => _Chunks;
            set => _Chunks = value ?? new List<ChunkResult>();
        }
    }
}
