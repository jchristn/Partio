namespace Partio.Core.Models
{
    /// <summary>
    /// Result of a single chunk with its text and computed embeddings.
    /// </summary>
    public class ChunkResult
    {
        private string _Text = string.Empty;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();
        private List<float> _Embeddings = new List<float>();

        /// <summary>
        /// Original text of this chunk.
        /// </summary>
        public string Text
        {
            get => _Text;
            set => _Text = value ?? string.Empty;
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

        /// <summary>
        /// Computed embedding vector.
        /// </summary>
        public List<float> Embeddings
        {
            get => _Embeddings;
            set => _Embeddings = value ?? new List<float>();
        }
    }
}
