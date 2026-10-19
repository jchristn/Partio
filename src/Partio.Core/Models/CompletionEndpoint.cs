namespace Partio.Core.Models
{
    using System.Data;
    using Partio.Core.Enums;
    using Partio.Core.ThirdParty;

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
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();
        private bool _HealthCheckEnabled = true;
        private string? _HealthCheckUrl = null;
        private HealthCheckMethodEnum _HealthCheckMethod = HealthCheckMethodEnum.GET;
        private int _HealthCheckIntervalMs = 5000;
        private int _HealthCheckTimeoutMs = 2000;
        private int _MaximumTimeoutMs = 60000;
        private int _MaxConcurrentRequests = 2;
        private int _MaxQueueDepth = 0;
        private int _ContextSize = 0;
        private int _HealthCheckExpectedStatusCode = 200;
        private int _HealthyThreshold = 2;
        private int _UnhealthyThreshold = 2;
        private bool _HealthCheckUseAuth = false;
        private ProviderSettings? _ProviderSettings = null;
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
        /// <remarks>OllamaCloud endpoints always resolve to https://ollama.com. When omitted, hosted providers use their public API root and Bedrock and Vertex AI derive the regional URL from ProviderSettings. Azure OpenAI uses the resource URL. Null or empty selects the provider default; create and update return 400 when the provider has none.</remarks>
        public string Endpoint
        {
            get => _Endpoint;
            set => _Endpoint = value ?? string.Empty;
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
        /// API key for the upstream inference provider (nullable; required for Ollama Cloud, xAI, Azure OpenAI, Anthropic, and Cohere). Masked in API responses; use the reveal API to read it.
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
        /// Maximum upstream provider request timeout in milliseconds.
        /// </summary>
        public int MaximumTimeoutMs
        {
            get => _MaximumTimeoutMs;
            set => _MaximumTimeoutMs = value <= 0 ? 1 : value;
        }

        /// <summary>
        /// Maximum concurrent upstream provider requests allowed for this endpoint.
        /// </summary>
        public int MaxConcurrentRequests
        {
            get => _MaxConcurrentRequests;
            set => _MaxConcurrentRequests = value < 1 ? 1 : value;
        }

        /// <summary>
        /// Maximum number of requests that may wait for a concurrency slot once <see cref="MaxConcurrentRequests"/> upstream inference calls are in flight.
        /// Default is <c>0</c>, meaning requests are rejected immediately (HTTP 429) when the concurrency limit is reached.
        /// Values greater than <c>0</c> allow up to that many requests to queue and wait for a slot; when the queue is full the endpoint returns HTTP 429.
        /// Queued requests wait no longer than <see cref="MaximumTimeoutMs"/>. Clamped server-side to <c>&gt;= 0</c>.
        /// </summary>
        public int MaxQueueDepth
        {
            get => _MaxQueueDepth;
            set => _MaxQueueDepth = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Context window size (maximum tokens) for the model served by this endpoint.
        /// Default is <c>0</c>, meaning unspecified. Clamped server-side to <c>&gt;= 0</c>.
        /// </summary>
        public int ContextSize
        {
            get => _ContextSize;
            set => _ContextSize = value < 0 ? 0 : value;
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
        /// Whether to send the endpoint's ApiKey on health check requests.
        /// Gemini uses x-goog-api-key, Anthropic uses x-api-key, Azure OpenAI uses api-key (or a bearer token), and other providers use a Bearer token. Bedrock, Vertex AI, and VoyageAI are checked with a PolyPrompt connectivity probe instead.
        /// </summary>
        public bool HealthCheckUseAuth
        {
            get => _HealthCheckUseAuth;
            set => _HealthCheckUseAuth = value;
        }

        /// <summary>
        /// Optional provider-specific settings. Only the section matching ApiFormat is kept; Bedrock and
        /// Vertex AI endpoints require their section. Secret fields are masked in API responses.
        /// </summary>
        public ProviderSettings? ProviderSettings
        {
            get => _ProviderSettings;
            set => _ProviderSettings = value;
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
            if (row.Table.Columns.Contains("maximum_timeout_ms") && row["maximum_timeout_ms"] != DBNull.Value)
                ep.MaximumTimeoutMs = Convert.ToInt32(row["maximum_timeout_ms"]);
            if (row.Table.Columns.Contains("max_concurrent_requests") && row["max_concurrent_requests"] != DBNull.Value)
                ep.MaxConcurrentRequests = Convert.ToInt32(row["max_concurrent_requests"]);
            if (row.Table.Columns.Contains("max_queue_depth") && row["max_queue_depth"] != DBNull.Value)
                ep.MaxQueueDepth = Convert.ToInt32(row["max_queue_depth"]);
            if (row.Table.Columns.Contains("context_size") && row["context_size"] != DBNull.Value)
                ep.ContextSize = Convert.ToInt32(row["context_size"]);
            ep.HealthCheckExpectedStatusCode = Convert.ToInt32(row["health_check_expected_status"]);
            ep.HealthyThreshold = Convert.ToInt32(row["healthy_threshold"]);
            ep.UnhealthyThreshold = Convert.ToInt32(row["unhealthy_threshold"]);
            ep.HealthCheckUseAuth = Convert.ToBoolean(row["health_check_use_auth"]);

            if (row.Table.Columns.Contains("labels_json"))
            {
                string? labelsJson = row["labels_json"] == DBNull.Value ? null : row["labels_json"].ToString();
                if (!string.IsNullOrEmpty(labelsJson))
                    ep.Labels = new SerializationHelper.Serializer().DeserializeJson<List<string>>(labelsJson) ?? new List<string>();
            }

            if (row.Table.Columns.Contains("tags_json"))
            {
                string? tagsJson = row["tags_json"] == DBNull.Value ? null : row["tags_json"].ToString();
                if (!string.IsNullOrEmpty(tagsJson))
                    ep.Tags = new SerializationHelper.Serializer().DeserializeJson<Dictionary<string, string>>(tagsJson) ?? new Dictionary<string, string>();
            }

            if (row.Table.Columns.Contains("provider_settings_json"))
            {
                string? providerSettingsJson = row["provider_settings_json"] == DBNull.Value ? null : row["provider_settings_json"].ToString();
                if (!string.IsNullOrEmpty(providerSettingsJson))
                    ep.ProviderSettings = new SerializationHelper.Serializer().DeserializeJson<ProviderSettings>(providerSettingsJson);
            }

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

            ep.ProviderSettings = ProviderDefaults.NormalizeProviderSettings(ep.ApiFormat, ep.ProviderSettings);

            // Resolve fixed hosts, well-known defaults, and region-derived URLs before deriving health check URLs.
            string normalizedEndpoint = ProviderDefaults.NormalizeEndpoint(ep.ApiFormat, ep.Endpoint, ep.ProviderSettings);
            if (!string.IsNullOrEmpty(normalizedEndpoint))
                ep.Endpoint = normalizedEndpoint;

            // Providers checked with a connectivity probe (Bedrock, Vertex AI, VoyageAI) have no health check URL.
            if (string.IsNullOrEmpty(ep.HealthCheckUrl))
                ep.HealthCheckUrl = ProviderDefaults.DefaultHealthCheckUrl(ep.ApiFormat, ep.Endpoint, ep.ProviderSettings);

            bool probe = ProviderDefaults.UsesConnectivityProbe(ep.ApiFormat);

            if (ep.HealthCheckIntervalMs <= 0)
                ep.HealthCheckIntervalMs = probe ? ProviderDefaults.ProbeHealthCheckIntervalMs : ep.ApiFormat == ApiFormatEnum.Ollama ? 5000 : 15000;

            if (ep.HealthCheckTimeoutMs <= 0)
                ep.HealthCheckTimeoutMs = probe ? ProviderDefaults.ProbeHealthCheckTimeoutMs : ep.ApiFormat == ApiFormatEnum.Ollama ? 2000 : 5000;

            if (ep.HealthCheckExpectedStatusCode <= 0)
                ep.HealthCheckExpectedStatusCode = 200;

            if (ep.HealthyThreshold <= 0)
                ep.HealthyThreshold = 2;

            if (ep.UnhealthyThreshold <= 0)
                ep.UnhealthyThreshold = 2;

            // Every provider except local Ollama typically requires auth for its health check.
            if (ProviderDefaults.DefaultHealthCheckUseAuth(ep.ApiFormat) && !string.IsNullOrEmpty(ep.ApiKey))
                ep.HealthCheckUseAuth = true;
        }

        /// <summary>
        /// Return a shallow copy of this endpoint with ApiKey and secret provider settings masked, for API responses.
        /// </summary>
        /// <returns>A redacted copy.</returns>
        public CompletionEndpoint Redacted()
        {
            CompletionEndpoint copy = (CompletionEndpoint)MemberwiseClone();
            copy._ApiKey = EndpointSecrets.Mask(_ApiKey);
            copy._ProviderSettings = EndpointSecrets.Redact(_ProviderSettings);
            return copy;
        }
    }
}
