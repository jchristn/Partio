namespace Partio.Core.Models
{
    using System.Data;
    using Partio.Core.Enums;

    /// <summary>
    /// Represents a completion/inference model endpoint in the Partio system.
    /// Used for summarization via LLM completion APIs.
    /// </summary>
    public class CompletionEndpoint
    {
        private string _Id = IdGenerator.NewCompletionEndpointId();
        private string _TenantId = string.Empty;
        private string? _Name = null;
        private string _Endpoint = string.Empty;
        private ApiFormatEnum _ApiFormat = ApiFormatEnum.Ollama;
        private string? _ApiKey = null;
        private string _Model = string.Empty;
        private bool _Active = true;
        private bool _EnableRequestHistory = true;
        private List<string>? _Labels = null;
        private Dictionary<string, string>? _Tags = null;
        private bool _HealthCheckEnabled = true;
        private string? _HealthCheckUrl = null;
        private HealthCheckMethodEnum _HealthCheckMethod = HealthCheckMethodEnum.GET;
        private int _HealthCheckIntervalMs = 5000;
        private int _HealthCheckTimeoutMs = 2000;
        private int _HealthCheckExpectedStatusCode = 200;
        private int _HealthyThreshold = 2;
        private int _UnhealthyThreshold = 2;
        private bool _HealthCheckUseAuth = false;
        private DateTime _CreatedUtc = DateTime.UtcNow;
        private DateTime _LastUpdateUtc = DateTime.UtcNow;

        /// <summary>
        /// K-sortable unique identifier with prefix 'cep_'.
        /// </summary>
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
        /// Human-readable name for the endpoint.
        /// </summary>
        public string? Name
        {
            get => _Name;
            set => _Name = value;
        }

        /// <summary>
        /// Base URL for the completion API (e.g., http://ollama:11434).
        /// </summary>
        public string Endpoint
        {
            get => _Endpoint;
            set => _Endpoint = !string.IsNullOrEmpty(value)
                ? value
                : throw new ArgumentException("Endpoint must not be null or empty.", nameof(Endpoint));
        }

        /// <summary>
        /// API format for the completion endpoint.
        /// </summary>
        public ApiFormatEnum ApiFormat
        {
            get => _ApiFormat;
            set => _ApiFormat = value;
        }

        /// <summary>
        /// API key / bearer token (optional, required for OpenAI).
        /// </summary>
        public string? ApiKey
        {
            get => _ApiKey;
            set => _ApiKey = value;
        }

        /// <summary>
        /// Model name (e.g., "llama3", "gpt-4o").
        /// </summary>
        public string Model
        {
            get => _Model;
            set => _Model = !string.IsNullOrEmpty(value)
                ? value
                : throw new ArgumentException("Model must not be null or empty.", nameof(Model));
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
        /// <remarks>Default: true.</remarks>
        public bool EnableRequestHistory
        {
            get => _EnableRequestHistory;
            set => _EnableRequestHistory = value;
        }

        /// <summary>
        /// Labels for categorization.
        /// </summary>
        public List<string>? Labels
        {
            get => _Labels;
            set => _Labels = value;
        }

        /// <summary>
        /// Key-value tags for metadata.
        /// </summary>
        public Dictionary<string, string>? Tags
        {
            get => _Tags;
            set => _Tags = value;
        }

        /// <summary>
        /// Whether background health checks are enabled.
        /// </summary>
        /// <remarks>Default: true.</remarks>
        public bool HealthCheckEnabled
        {
            get => _HealthCheckEnabled;
            set => _HealthCheckEnabled = value;
        }

        /// <summary>
        /// URL to probe for health checks.
        /// </summary>
        public string? HealthCheckUrl
        {
            get => _HealthCheckUrl;
            set => _HealthCheckUrl = value;
        }

        /// <summary>
        /// HTTP method for health checks (GET or HEAD).
        /// </summary>
        public HealthCheckMethodEnum HealthCheckMethod
        {
            get => _HealthCheckMethod;
            set => _HealthCheckMethod = value;
        }

        /// <summary>
        /// Milliseconds between health checks.
        /// </summary>
        public int HealthCheckIntervalMs
        {
            get => _HealthCheckIntervalMs;
            set => _HealthCheckIntervalMs = value;
        }

        /// <summary>
        /// Per-check HTTP timeout in milliseconds.
        /// </summary>
        public int HealthCheckTimeoutMs
        {
            get => _HealthCheckTimeoutMs;
            set => _HealthCheckTimeoutMs = value;
        }

        /// <summary>
        /// HTTP status code that indicates a successful health check.
        /// </summary>
        public int HealthCheckExpectedStatusCode
        {
            get => _HealthCheckExpectedStatusCode;
            set => _HealthCheckExpectedStatusCode = value;
        }

        /// <summary>
        /// Consecutive successes required to transition from unhealthy to healthy.
        /// </summary>
        public int HealthyThreshold
        {
            get => _HealthyThreshold;
            set => _HealthyThreshold = value;
        }

        /// <summary>
        /// Consecutive failures required to transition from healthy to unhealthy.
        /// </summary>
        public int UnhealthyThreshold
        {
            get => _UnhealthyThreshold;
            set => _UnhealthyThreshold = value;
        }

        /// <summary>
        /// Whether to send the endpoint's ApiKey as a Bearer token on health check requests.
        /// </summary>
        public bool HealthCheckUseAuth
        {
            get => _HealthCheckUseAuth;
            set => _HealthCheckUseAuth = value;
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
        /// Create a CompletionEndpoint from a DataRow.
        /// </summary>
        /// <param name="row">DataRow containing endpoint data.</param>
        /// <returns>A CompletionEndpoint instance.</returns>
        public static CompletionEndpoint FromDataRow(DataRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));

            CompletionEndpoint ep = new CompletionEndpoint();
            ep.Id = row["id"].ToString() ?? string.Empty;
            ep.TenantId = row["tenant_id"].ToString() ?? string.Empty;
            ep.Name = row["name"] == DBNull.Value ? null : row["name"].ToString();
            ep.Endpoint = row["endpoint"].ToString() ?? string.Empty;
            ep.ApiFormat = Enum.Parse<ApiFormatEnum>(row["api_format"].ToString() ?? "Ollama");
            ep.ApiKey = row["api_key"] == DBNull.Value ? null : row["api_key"].ToString();
            ep.Model = row["model"].ToString() ?? string.Empty;
            ep.Active = Convert.ToBoolean(row["active"]);
            ep.EnableRequestHistory = Convert.ToBoolean(row["enable_request_history"]);
            ep.CreatedUtc = DateTime.Parse(row["created_utc"].ToString() ?? DateTime.UtcNow.ToString("o")).ToUniversalTime();
            ep.LastUpdateUtc = DateTime.Parse(row["last_update_utc"].ToString() ?? DateTime.UtcNow.ToString("o")).ToUniversalTime();

            ep.HealthCheckEnabled = Convert.ToBoolean(row["health_check_enabled"]);
            ep.HealthCheckUrl = row["health_check_url"] == DBNull.Value ? null : row["health_check_url"].ToString();
            ep.HealthCheckMethod = (HealthCheckMethodEnum)Convert.ToInt32(row["health_check_method"]);
            ep.HealthCheckIntervalMs = Convert.ToInt32(row["health_check_interval_ms"]);
            ep.HealthCheckTimeoutMs = Convert.ToInt32(row["health_check_timeout_ms"]);
            ep.HealthCheckExpectedStatusCode = Convert.ToInt32(row["health_check_expected_status"]);
            ep.HealthyThreshold = Convert.ToInt32(row["healthy_threshold"]);
            ep.UnhealthyThreshold = Convert.ToInt32(row["unhealthy_threshold"]);
            ep.HealthCheckUseAuth = Convert.ToBoolean(row["health_check_use_auth"]);

            string? labelsJson = row["labels_json"] == DBNull.Value ? null : row["labels_json"].ToString();
            if (!string.IsNullOrEmpty(labelsJson))
                ep.Labels = new SerializationHelper.Serializer().DeserializeJson<List<string>>(labelsJson) ?? new List<string>();

            string? tagsJson = row["tags_json"] == DBNull.Value ? null : row["tags_json"].ToString();
            if (!string.IsNullOrEmpty(tagsJson))
                ep.Tags = new SerializationHelper.Serializer().DeserializeJson<Dictionary<string, string>>(tagsJson) ?? new Dictionary<string, string>();

            return ep;
        }

        /// <summary>
        /// Create a list of CompletionEndpoint from a DataTable.
        /// </summary>
        /// <param name="table">DataTable containing endpoint data.</param>
        /// <returns>A list of CompletionEndpoint instances.</returns>
        public static List<CompletionEndpoint> FromDataTable(DataTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            List<CompletionEndpoint> endpoints = new List<CompletionEndpoint>();
            foreach (DataRow row in table.Rows)
            {
                endpoints.Add(FromDataRow(row));
            }
            return endpoints;
        }

        /// <summary>
        /// Apply format-aware health check defaults based on ApiFormat.
        /// Only fills in fields that are at their default/zero values.
        /// </summary>
        /// <param name="ep">Completion endpoint to apply defaults to.</param>
        public static void ApplyHealthCheckDefaults(CompletionEndpoint ep)
        {
            if (ep == null) throw new ArgumentNullException(nameof(ep));

            if (string.IsNullOrEmpty(ep.HealthCheckUrl))
            {
                string baseUrl = ep.Endpoint.TrimEnd('/');
                ep.HealthCheckUrl = ep.ApiFormat == ApiFormatEnum.Ollama
                    ? baseUrl + "/api/tags"
                    : baseUrl + "/v1/models";
            }

            if (ep.HealthCheckIntervalMs <= 0)
                ep.HealthCheckIntervalMs = ep.ApiFormat == ApiFormatEnum.Ollama ? 5000 : 30000;

            if (ep.HealthCheckTimeoutMs <= 0)
                ep.HealthCheckTimeoutMs = ep.ApiFormat == ApiFormatEnum.Ollama ? 2000 : 10000;

            if (ep.HealthCheckExpectedStatusCode <= 0)
                ep.HealthCheckExpectedStatusCode = 200;

            if (ep.HealthyThreshold <= 0)
                ep.HealthyThreshold = 2;

            if (ep.UnhealthyThreshold <= 0)
                ep.UnhealthyThreshold = 2;

            // For OpenAI, default to using auth since most providers require it
            if (ep.ApiFormat == ApiFormatEnum.OpenAI && !string.IsNullOrEmpty(ep.ApiKey))
                ep.HealthCheckUseAuth = true;
        }
    }
}
