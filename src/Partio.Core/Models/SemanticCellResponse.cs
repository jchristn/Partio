namespace Partio.Core.Models
{
    /// <summary>
    /// Outbound response body for a processed semantic cell.
    /// </summary>
    public class SemanticCellResponse
    {
        private int _Cells = 0;
        private int _TotalChunks = 0;
        private List<ChunkResult> _Chunks = new List<ChunkResult>();
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();

        /// <summary>
        /// Number of input cells processed.
        /// </summary>
        public int Cells
        {
            get => _Cells;
            set => _Cells = value;
        }

        /// <summary>
        /// Total number of chunks produced.
        /// </summary>
        public int TotalChunks
        {
            get => _TotalChunks;
            set => _TotalChunks = value;
        }

        /// <summary>
        /// List of chunk results with text and embeddings.
        /// </summary>
        public List<ChunkResult> Chunks
        {
            get => _Chunks;
            set => _Chunks = value ?? new List<ChunkResult>();
        }

        /// <summary>
        /// Labels echoed from the request.
        /// </summary>
        public List<string> Labels
        {
            get => _Labels;
            set => _Labels = value ?? new List<string>();
        }

        /// <summary>
        /// Tags echoed from the request.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get => _Tags;
            set => _Tags = value ?? new Dictionary<string, string>();
        }
    }
}
