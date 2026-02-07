namespace Partio.Core.Database.Sqlite.Implementations
{
    using System.Data;
    using System.Text;
    using Partio.Core.Database.Interfaces;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using SyslogLogging;

    /// <summary>
    /// SQLite implementation of embedding endpoint database operations.
    /// </summary>
    public class EmbeddingEndpointMethods : IEmbeddingEndpointMethods
    {
        private readonly SqliteDatabaseDriver _Driver;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[EmbeddingEndpointMethods] ";

        /// <summary>
        /// Initialize a new instance of EmbeddingEndpointMethods.
        /// </summary>
        /// <param name="driver">SQLite database driver.</param>
        /// <param name="logging">Logging module.</param>
        public EmbeddingEndpointMethods(SqliteDatabaseDriver driver, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Create a new embedding endpoint.
        /// </summary>
        /// <param name="endpoint">Embedding endpoint to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created embedding endpoint.</returns>
        public async Task<EmbeddingEndpoint> CreateAsync(EmbeddingEndpoint endpoint, CancellationToken token = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            SerializationHelper.Serializer serializer = new SerializationHelper.Serializer();
            string labelsJson = serializer.SerializeJson(endpoint.Labels, false);
            string tagsJson = serializer.SerializeJson(endpoint.Tags, false);

            string query =
                "INSERT INTO embedding_endpoints (id, tenant_id, model, endpoint, api_format, api_key, active, labels_json, tags_json, created_utc, last_update_utc) VALUES (" +
                "'" + _Driver.Sanitize(endpoint.Id) + "', " +
                "'" + _Driver.Sanitize(endpoint.TenantId) + "', " +
                "'" + _Driver.Sanitize(endpoint.Model) + "', " +
                "'" + _Driver.Sanitize(endpoint.Endpoint) + "', " +
                "'" + _Driver.Sanitize(endpoint.ApiFormat.ToString()) + "', " +
                _Driver.FormatNullableString(endpoint.ApiKey) + ", " +
                _Driver.FormatBoolean(endpoint.Active) + ", " +
                "'" + _Driver.Sanitize(labelsJson) + "', " +
                "'" + _Driver.Sanitize(tagsJson) + "', " +
                "'" + _Driver.FormatDateTime(endpoint.CreatedUtc) + "', " +
                "'" + _Driver.FormatDateTime(endpoint.LastUpdateUtc) + "'" +
                ");";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return endpoint;
        }

        /// <summary>
        /// Read an embedding endpoint by ID.
        /// </summary>
        /// <param name="id">Embedding endpoint ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The embedding endpoint, or null if not found.</returns>
        public async Task<EmbeddingEndpoint?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM embedding_endpoints WHERE id = '" + _Driver.Sanitize(id) + "' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count < 1) return null;
            return EmbeddingEndpoint.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Read an embedding endpoint by model name within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="model">Model name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The embedding endpoint, or null if not found.</returns>
        public async Task<EmbeddingEndpoint?> ReadByModelAsync(string tenantId, string model, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(model)) throw new ArgumentNullException(nameof(model));

            string query = "SELECT * FROM embedding_endpoints WHERE tenant_id = '" + _Driver.Sanitize(tenantId) + "' AND model = '" + _Driver.Sanitize(model) + "' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count < 1) return null;
            return EmbeddingEndpoint.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Update an existing embedding endpoint.
        /// </summary>
        /// <param name="endpoint">Embedding endpoint to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated embedding endpoint.</returns>
        public async Task<EmbeddingEndpoint> UpdateAsync(EmbeddingEndpoint endpoint, CancellationToken token = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            endpoint.LastUpdateUtc = DateTime.UtcNow;

            SerializationHelper.Serializer serializer = new SerializationHelper.Serializer();
            string labelsJson = serializer.SerializeJson(endpoint.Labels, false);
            string tagsJson = serializer.SerializeJson(endpoint.Tags, false);

            string query =
                "UPDATE embedding_endpoints SET " +
                "tenant_id = '" + _Driver.Sanitize(endpoint.TenantId) + "', " +
                "model = '" + _Driver.Sanitize(endpoint.Model) + "', " +
                "endpoint = '" + _Driver.Sanitize(endpoint.Endpoint) + "', " +
                "api_format = '" + _Driver.Sanitize(endpoint.ApiFormat.ToString()) + "', " +
                "api_key = " + _Driver.FormatNullableString(endpoint.ApiKey) + ", " +
                "active = " + _Driver.FormatBoolean(endpoint.Active) + ", " +
                "labels_json = '" + _Driver.Sanitize(labelsJson) + "', " +
                "tags_json = '" + _Driver.Sanitize(tagsJson) + "', " +
                "last_update_utc = '" + _Driver.FormatDateTime(endpoint.LastUpdateUtc) + "' " +
                "WHERE id = '" + _Driver.Sanitize(endpoint.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return endpoint;
        }

        /// <summary>
        /// Delete an embedding endpoint by ID.
        /// </summary>
        /// <param name="id">Embedding endpoint ID.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM embedding_endpoints WHERE id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if an embedding endpoint exists by ID.
        /// </summary>
        /// <param name="id">Embedding endpoint ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the embedding endpoint exists.</returns>
        public async Task<bool> ExistsByIdAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM embedding_endpoints WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count < 1) return false;
            return Convert.ToInt64(result.Rows[0]["cnt"]) > 0;
        }

        /// <summary>
        /// Enumerate embedding endpoints with pagination, scoped to a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="request">Enumeration request parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result.</returns>
        public async Task<EnumerationResult<EmbeddingEndpoint>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (request == null) throw new ArgumentNullException(nameof(request));

            List<string> conditions = new List<string>();
            conditions.Add("tenant_id = '" + _Driver.Sanitize(tenantId) + "'");

            if (!string.IsNullOrEmpty(request.NameFilter))
                conditions.Add("model LIKE '%" + _Driver.Sanitize(request.NameFilter) + "%'");

            if (!string.IsNullOrEmpty(request.LabelFilter))
                conditions.Add("labels_json LIKE '%\"" + _Driver.Sanitize(request.LabelFilter) + "\"%'");

            if (!string.IsNullOrEmpty(request.TagKeyFilter))
                conditions.Add("tags_json LIKE '%\"" + _Driver.Sanitize(request.TagKeyFilter) + "\"%'");

            if (!string.IsNullOrEmpty(request.TagValueFilter))
                conditions.Add("tags_json LIKE '%\"" + _Driver.Sanitize(request.TagValueFilter) + "\"%'");

            if (request.ActiveFilter != null)
                conditions.Add("active = " + _Driver.FormatBoolean(request.ActiveFilter.Value));

            if (!string.IsNullOrEmpty(request.ContinuationToken))
                conditions.Add("id > '" + _Driver.Sanitize(request.ContinuationToken) + "'");

            string whereClause = "WHERE " + string.Join(" AND ", conditions);

            string orderByClause;
            switch (request.Order)
            {
                case EnumerationOrderEnum.CreatedAscending:
                    orderByClause = "ORDER BY created_utc ASC";
                    break;
                case EnumerationOrderEnum.CreatedDescending:
                    orderByClause = "ORDER BY created_utc DESC";
                    break;
                case EnumerationOrderEnum.NameAscending:
                    orderByClause = "ORDER BY model ASC";
                    break;
                case EnumerationOrderEnum.NameDescending:
                    orderByClause = "ORDER BY model DESC";
                    break;
                default:
                    orderByClause = "ORDER BY created_utc DESC";
                    break;
            }

            string countQuery = "SELECT COUNT(*) AS cnt FROM embedding_endpoints " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);

            int fetchCount = request.MaxResults + 1;
            string dataQuery = "SELECT * FROM embedding_endpoints " + whereClause + " " + orderByClause + " LIMIT " + fetchCount + ";";
            DataTable dataResult = await _Driver.ExecuteQueryAsync(dataQuery, false, token).ConfigureAwait(false);

            List<EmbeddingEndpoint> endpoints = EmbeddingEndpoint.FromDataTable(dataResult);

            EnumerationResult<EmbeddingEndpoint> result = new EnumerationResult<EmbeddingEndpoint>();
            result.TotalCount = totalCount;

            if (endpoints.Count > request.MaxResults)
            {
                result.HasMore = true;
                endpoints = endpoints.GetRange(0, request.MaxResults);
            }

            result.Data = endpoints;

            if (result.HasMore && endpoints.Count > 0)
                result.ContinuationToken = endpoints[endpoints.Count - 1].Id;

            return result;
        }

        /// <summary>
        /// Count embedding endpoints in a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total number of embedding endpoints in the tenant.</returns>
        public async Task<long> CountAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            string query = "SELECT COUNT(*) AS cnt FROM embedding_endpoints WHERE tenant_id = '" + _Driver.Sanitize(tenantId) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return Convert.ToInt64(result.Rows[0]["cnt"]);
        }
    }
}
