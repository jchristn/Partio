namespace Partio.Core.Models
{
    /// <summary>
    /// Result of a single chunk with its text and computed embeddings.
    /// </summary>
    public class ChunkResult
    {
        private string _Text = string.Empty;
        private string _ChunkedText = string.Empty;
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
        /// Context prefix + chunk text — the string that was actually embedded.
        /// </summary>
        public string ChunkedText
        {
            get => _ChunkedText;
            set => _ChunkedText = value ?? string.Empty;
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
