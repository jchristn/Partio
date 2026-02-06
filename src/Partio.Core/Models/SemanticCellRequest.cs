namespace Partio.Core.Models
{
    using Partio.Core.Enums;

    /// <summary>
    /// Inbound request body for processing a semantic cell.
    /// </summary>
    public class SemanticCellRequest
    {
        private AtomTypeEnum _Type = AtomTypeEnum.Text;
        private string? _Text = null;
        private List<string>? _UnorderedList = null;
        private List<string>? _OrderedList = null;
        private List<List<string>>? _Table = null;
        private byte[]? _Binary = null;
        private ChunkingConfiguration _ChunkingConfiguration = new ChunkingConfiguration();
        private EmbeddingConfiguration _EmbeddingConfiguration = new EmbeddingConfiguration();
        private List<string>? _Labels = null;
        private Dictionary<string, string>? _Tags = null;

        /// <summary>
        /// Type of the semantic atom.
        /// </summary>
        public AtomTypeEnum Type
        {
            get => _Type;
            set => _Type = value;
        }

        /// <summary>
        /// Text content (for Text atom type).
        /// </summary>
        public string? Text
        {
            get => _Text;
            set => _Text = value;
        }

        /// <summary>
        /// Unordered list content.
        /// </summary>
        public List<string>? UnorderedList
        {
            get => _UnorderedList;
            set => _UnorderedList = value;
        }

        /// <summary>
        /// Ordered list content.
        /// </summary>
        public List<string>? OrderedList
        {
            get => _OrderedList;
            set => _OrderedList = value;
        }

        /// <summary>
        /// Table content as a list of rows (each row is a list of cell values).
        /// </summary>
        public List<List<string>>? Table
        {
            get => _Table;
            set => _Table = value;
        }

        /// <summary>
        /// Binary content.
        /// </summary>
        public byte[]? Binary
        {
            get => _Binary;
            set => _Binary = value;
        }

        /// <summary>
        /// Chunking configuration for this request.
        /// </summary>
        public ChunkingConfiguration ChunkingConfiguration
        {
            get => _ChunkingConfiguration;
            set => _ChunkingConfiguration = value ?? throw new ArgumentNullException(nameof(ChunkingConfiguration));
        }

        /// <summary>
        /// Embedding configuration for this request.
        /// </summary>
        public EmbeddingConfiguration EmbeddingConfiguration
        {
            get => _EmbeddingConfiguration;
            set => _EmbeddingConfiguration = value ?? throw new ArgumentNullException(nameof(EmbeddingConfiguration));
        }

        /// <summary>
        /// Labels to echo in the response.
        /// </summary>
        public List<string>? Labels
        {
            get => _Labels;
            set => _Labels = value;
        }

        /// <summary>
        /// Tags to echo in the response.
        /// </summary>
        public Dictionary<string, string>? Tags
        {
            get => _Tags;
            set => _Tags = value;
        }
    }
}
