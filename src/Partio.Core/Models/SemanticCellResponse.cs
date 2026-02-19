namespace Partio.Core.Models
{
    using Partio.Core.Enums;

    /// <summary>
    /// Outbound response body for a processed semantic cell.
    /// </summary>
    public class SemanticCellResponse
    {
        private Guid _GUID = Guid.NewGuid();
        private Guid? _ParentGUID = null;
        private AtomTypeEnum _Type = AtomTypeEnum.Text;
        private string _Text = string.Empty;
        private List<ChunkResult> _Chunks = new List<ChunkResult>();
        private List<SemanticCellResponse>? _Children = null;

        /// <summary>
        /// Unique identifier for this cell.
        /// </summary>
        public Guid GUID
        {
            get => _GUID;
            set => _GUID = value;
        }

        /// <summary>
        /// Parent cell GUID (null for root-level cells).
        /// </summary>
        public Guid? ParentGUID
        {
            get => _ParentGUID;
            set => _ParentGUID = value;
        }

        /// <summary>
        /// Cell type (Text, Summary, etc.).
        /// </summary>
        public AtomTypeEnum Type
        {
            get => _Type;
            set => _Type = value;
        }

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

        /// <summary>
        /// Child cell responses forming a hierarchy.
        /// </summary>
        public List<SemanticCellResponse>? Children
        {
            get => _Children;
            set => _Children = value;
        }
    }
}
