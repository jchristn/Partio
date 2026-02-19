namespace Partio.Core.Database.Sqlite.Implementations
{
    using System.Data;
    using Partio.Core.Database.Interfaces;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using SyslogLogging;

    /// <summary>
    /// SQLite implementation of completion endpoint database operations.
    /// </summary>
    public class CompletionEndpointMethods : ICompletionEndpointMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Initialize a new instance of CompletionEndpointMethods.
        /// </summary>
        /// <param name="driver">SQLite database driver.</param>
        /// <param name="logging">Logging module.</param>
        public CompletionEndpointMethods(SqliteDatabaseDriver driver, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Create a new completion endpoint.
        /// </summary>
        public async Task<CompletionEndpoint> CreateAsync(CompletionEndpoint endpoint, CancellationToken token = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            SerializationHelper.Serializer serializer = new SerializationHelper.Serializer();
            string labelsJson = serializer.SerializeJson(endpoint.Labels, false);
            string tagsJson = serializer.SerializeJson(endpoint.Tags, false);

            string query =
                "INSERT INTO completion_endpoints (id, tenant_id, name, endpoint, api_format, api_key, model, active, enable_request_history, " +
                "health_check_enabled, health_check_url, health_check_method, health_check_interval_ms, health_check_timeout_ms, " +
                "health_check_expected_status, healthy_threshold, unhealthy_threshold, health_check_use_auth, " +
                "labels_json, tags_json, created_utc, last_update_utc) VALUES (" +
                "'" + _Driver.Sanitize(endpoint.Id) + "', " +
                "'" + _Driver.Sanitize(endpoint.TenantId) + "', " +
                _Driver.FormatNullableString(endpoint.Name) + ", " +
                "'" + _Driver.Sanitize(endpoint.Endpoint) + "', " +
                "'" + _Driver.Sanitize(endpoint.ApiFormat.ToString()) + "', " +
                _Driver.FormatNullableString(endpoint.ApiKey) + ", " +
                "'" + _Driver.Sanitize(endpoint.Model) + "', " +
                _Driver.FormatBoolean(endpoint.Active) + ", " +
                _Driver.FormatBoolean(endpoint.EnableRequestHistory) + ", " +
                _Driver.FormatBoolean(endpoint.HealthCheckEnabled) + ", " +
                _Driver.FormatNullableString(endpoint.HealthCheckUrl) + ", " +
                (int)endpoint.HealthCheckMethod + ", " +
                endpoint.HealthCheckIntervalMs + ", " +
                endpoint.HealthCheckTimeoutMs + ", " +
                endpoint.HealthCheckExpectedStatusCode + ", " +
                endpoint.HealthyThreshold + ", " +
                endpoint.UnhealthyThreshold + ", " +
                _Driver.FormatBoolean(endpoint.HealthCheckUseAuth) + ", " +
                "'" + _Driver.Sanitize(labelsJson) + "', " +
                "'" + _Driver.Sanitize(tagsJson) + "', " +
                "'" + _Driver.FormatDateTime(endpoint.CreatedUtc) + "', " +
                "'" + _Driver.FormatDateTime(endpoint.LastUpdateUtc) + "'" +
                ");";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return endpoint;
        }

        /// <summary>
        /// Read a completion endpoint by ID.
        /// </summary>
        public async Task<CompletionEndpoint?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM completion_endpoints WHERE id = '" + _Driver.Sanitize(id) + "' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count < 1) return null;
            return CompletionEndpoint.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Update an existing completion endpoint.
        /// </summary>
        public async Task<CompletionEndpoint> UpdateAsync(CompletionEndpoint endpoint, CancellationToken token = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            endpoint.LastUpdateUtc = DateTime.UtcNow;

            SerializationHelper.Serializer serializer = new SerializationHelper.Serializer();
            string labelsJson = serializer.SerializeJson(endpoint.Labels, false);
            string tagsJson = serializer.SerializeJson(endpoint.Tags, false);

            string query =
                "UPDATE completion_endpoints SET " +
                "tenant_id = '" + _Driver.Sanitize(endpoint.TenantId) + "', " +
                "name = " + _Driver.FormatNullableString(endpoint.Name) + ", " +
                "endpoint = '" + _Driver.Sanitize(endpoint.Endpoint) + "', " +
                "api_format = '" + _Driver.Sanitize(endpoint.ApiFormat.ToString()) + "', " +
                "api_key = " + _Driver.FormatNullableString(endpoint.ApiKey) + ", " +
                "model = '" + _Driver.Sanitize(endpoint.Model) + "', " +
                "active = " + _Driver.FormatBoolean(endpoint.Active) + ", " +
                "enable_request_history = " + _Driver.FormatBoolean(endpoint.EnableRequestHistory) + ", " +
                "health_check_enabled = " + _Driver.FormatBoolean(endpoint.HealthCheckEnabled) + ", " +
                "health_check_url = " + _Driver.FormatNullableString(endpoint.HealthCheckUrl) + ", " +
                "health_check_method = " + (int)endpoint.HealthCheckMethod + ", " +
                "health_check_interval_ms = " + endpoint.HealthCheckIntervalMs + ", " +
                "health_check_timeout_ms = " + endpoint.HealthCheckTimeoutMs + ", " +
                "health_check_expected_status = " + endpoint.HealthCheckExpectedStatusCode + ", " +
                "healthy_threshold = " + endpoint.HealthyThreshold + ", " +
                "unhealthy_threshold = " + endpoint.UnhealthyThreshold + ", " +
                "health_check_use_auth = " + _Driver.FormatBoolean(endpoint.HealthCheckUseAuth) + ", " +
                "labels_json = '" + _Driver.Sanitize(labelsJson) + "', " +
                "tags_json = '" + _Driver.Sanitize(tagsJson) + "', " +
                "last_update_utc = '" + _Driver.FormatDateTime(endpoint.LastUpdateUtc) + "' " +
                "WHERE id = '" + _Driver.Sanitize(endpoint.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return endpoint;
        }

        /// <summary>
        /// Delete a completion endpoint by ID.
        /// </summary>
        public async Task DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM completion_endpoints WHERE id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a completion endpoint exists by ID.
        /// </summary>
        public async Task<bool> ExistsByIdAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM completion_endpoints WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count < 1) return false;
            return Convert.ToInt64(result.Rows[0]["cnt"]) > 0;
        }

        /// <summary>
        /// Enumerate completion endpoints with pagination, scoped to a tenant.
        /// </summary>
        public async Task<EnumerationResult<CompletionEndpoint>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (request == null) throw new ArgumentNullException(nameof(request));

            List<string> conditions = new List<string>();
            conditions.Add("tenant_id = '" + _Driver.Sanitize(tenantId) + "'");

            if (!string.IsNullOrEmpty(request.NameFilter))
                conditions.Add("name LIKE '%" + _Driver.Sanitize(request.NameFilter) + "%'");

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
                    orderByClause = "ORDER BY name ASC";
                    break;
                case EnumerationOrderEnum.NameDescending:
                    orderByClause = "ORDER BY name DESC";
                    break;
                default:
                    orderByClause = "ORDER BY created_utc DESC";
                    break;
            }

            string countQuery = "SELECT COUNT(*) AS cnt FROM completion_endpoints " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);

            int fetchCount = request.MaxResults + 1;
            string dataQuery = "SELECT * FROM completion_endpoints " + whereClause + " " + orderByClause + " LIMIT " + fetchCount + ";";
            DataTable dataResult = await _Driver.ExecuteQueryAsync(dataQuery, false, token).ConfigureAwait(false);

            List<CompletionEndpoint> endpoints = CompletionEndpoint.FromDataTable(dataResult);

            EnumerationResult<CompletionEndpoint> result = new EnumerationResult<CompletionEndpoint>();
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
        /// Count completion endpoints in a tenant.
        /// </summary>
        public async Task<long> CountAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            string query = "SELECT COUNT(*) AS cnt FROM completion_endpoints WHERE tenant_id = '" + _Driver.Sanitize(tenantId) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return Convert.ToInt64(result.Rows[0]["cnt"]);
        }
    }
}
