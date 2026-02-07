namespace Partio.Core.Models
{
    using System.Data;
    using Partio.Core.Enums;

    /// <summary>
    /// Represents an embedding model endpoint in the Partio system.
    /// </summary>
    public class EmbeddingEndpoint
    {
        private string _Id = IdGenerator.NewEmbeddingEndpointId();
        private string _TenantId = string.Empty;
        private string _Model = string.Empty;
        private string _Endpoint = string.Empty;
        private ApiFormatEnum _ApiFormat = ApiFormatEnum.Ollama;
        private string? _ApiKey = null;
        private bool _Active = true;
        private bool _EnableRequestHistory = false;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();
        private DateTime _CreatedUtc = DateTime.UtcNow;
        private DateTime _LastUpdateUtc = DateTime.UtcNow;

        /// <summary>
        /// K-sortable unique identifier with prefix 'ep_'.
        /// </summary>
        /// <remarks>48 characters.</remarks>
        public string Id
        {
            get => _Id;
            set => _Id = value ?? throw new ArgumentNullException(nameof(Id));
        }

        /// <summary>
        /// Tenant ID this endpoint belongs to.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = value ?? throw new ArgumentNullException(nameof(TenantId));
        }

        /// <summary>
        /// Embedding model name (e.g. "all-minilm", "text-embedding-3-small").
        /// </summary>
        public string Model
        {
            get => _Model;
            set => _Model = !string.IsNullOrEmpty(value)
                ? value
                : throw new ArgumentException("Model must not be null or empty.", nameof(Model));
        }

        /// <summary>
        /// Embedding endpoint URL.
        /// </summary>
        public string Endpoint
        {
            get => _Endpoint;
            set => _Endpoint = !string.IsNullOrEmpty(value)
                ? value
                : throw new ArgumentException("Endpoint must not be null or empty.", nameof(Endpoint));
        }

        /// <summary>
        /// API format for the embedding endpoint.
        /// </summary>
        public ApiFormatEnum ApiFormat
        {
            get => _ApiFormat;
            set => _ApiFormat = value;
        }

        /// <summary>
        /// API key for the embedding endpoint (nullable).
        /// </summary>
        public string? ApiKey
        {
            get => _ApiKey;
            set => _ApiKey = value;
        }

        /// <summary>
        /// Whether the endpoint is active.
        /// </summary>
        /// <remarks>Default: true.</remarks>
        public bool Active
        {
            get => _Active;
            set => _Active = value;
        }

        /// <summary>
        /// Whether request history is enabled for this endpoint.
        /// </summary>
        /// <remarks>Default: false.</remarks>
        public bool EnableRequestHistory
        {
            get => _EnableRequestHistory;
            set => _EnableRequestHistory = value;
        }

        /// <summary>
        /// Labels for categorization.
        /// </summary>
        public List<string> Labels
        {
            get => _Labels;
            set => _Labels = value ?? new List<string>();
        }

        /// <summary>
        /// Key-value tags for metadata.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get => _Tags;
            set => _Tags = value ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// UTC timestamp when the endpoint was created.
        /// </summary>
        public DateTime CreatedUtc
        {
            get => _CreatedUtc;
            set => _CreatedUtc = value;
        }

        /// <summary>
        /// UTC timestamp of the last update.
        /// </summary>
        public DateTime LastUpdateUtc
        {
            get => _LastUpdateUtc;
            set => _LastUpdateUtc = value;
        }

        /// <summary>
        /// Create an EmbeddingEndpoint from a DataRow.
        /// </summary>
        /// <param name="row">DataRow containing endpoint data.</param>
        /// <returns>An EmbeddingEndpoint instance.</returns>
        public static EmbeddingEndpoint FromDataRow(DataRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));

            EmbeddingEndpoint ep = new EmbeddingEndpoint();
            ep.Id = row["id"].ToString() ?? string.Empty;
            ep.TenantId = row["tenant_id"].ToString() ?? string.Empty;
            ep.Model = row["model"].ToString() ?? string.Empty;
            ep.Endpoint = row["endpoint"].ToString() ?? string.Empty;
            ep.ApiFormat = Enum.Parse<ApiFormatEnum>(row["api_format"].ToString() ?? "Ollama");
            ep.ApiKey = row["api_key"] == DBNull.Value ? null : row["api_key"].ToString();
            ep.Active = Convert.ToBoolean(row["active"]);
            ep.EnableRequestHistory = Convert.ToBoolean(row["enable_request_history"]);
            ep.CreatedUtc = DateTime.Parse(row["created_utc"].ToString() ?? DateTime.UtcNow.ToString("o")).ToUniversalTime();
            ep.LastUpdateUtc = DateTime.Parse(row["last_update_utc"].ToString() ?? DateTime.UtcNow.ToString("o")).ToUniversalTime();

            string? labelsJson = row["labels_json"] == DBNull.Value ? null : row["labels_json"].ToString();
            if (!string.IsNullOrEmpty(labelsJson))
                ep.Labels = new SerializationHelper.Serializer().DeserializeJson<List<string>>(labelsJson) ?? new List<string>();

            string? tagsJson = row["tags_json"] == DBNull.Value ? null : row["tags_json"].ToString();
            if (!string.IsNullOrEmpty(tagsJson))
                ep.Tags = new SerializationHelper.Serializer().DeserializeJson<Dictionary<string, string>>(tagsJson) ?? new Dictionary<string, string>();

            return ep;
        }

        /// <summary>
        /// Create a list of EmbeddingEndpoint from a DataTable.
        /// </summary>
        /// <param name="table">DataTable containing endpoint data.</param>
        /// <returns>A list of EmbeddingEndpoint instances.</returns>
        public static List<EmbeddingEndpoint> FromDataTable(DataTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            List<EmbeddingEndpoint> endpoints = new List<EmbeddingEndpoint>();
            foreach (DataRow row in table.Rows)
            {
                endpoints.Add(FromDataRow(row));
            }
            return endpoints;
        }
    }
}
