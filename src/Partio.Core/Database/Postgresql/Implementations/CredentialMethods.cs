namespace Partio.Core.Database.Postgresql.Implementations
{
    using System.Data;
    using System.Text;
    using Partio.Core.Database.Interfaces;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using SyslogLogging;

    /// <summary>
    /// PostgreSQL implementation of credential database operations.
    /// </summary>
    public class CredentialMethods : ICredentialMethods
    {
        private readonly PostgresqlDatabaseDriver _Driver;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[CredentialMethods] ";

        /// <summary>
        /// Initialize a new instance of CredentialMethods.
        /// </summary>
        /// <param name="driver">PostgreSQL database driver.</param>
        /// <param name="logging">Logging module.</param>
        public CredentialMethods(PostgresqlDatabaseDriver driver, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Create a new credential.
        /// </summary>
        /// <param name="credential">Credential to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created credential.</returns>
        public async Task<Credential> CreateAsync(Credential credential, CancellationToken token = default)
        {
            if (credential == null) throw new ArgumentNullException(nameof(credential));

            SerializationHelper.Serializer serializer = new SerializationHelper.Serializer();
            string labelsJson = serializer.SerializeJson(credential.Labels, false);
            string tagsJson = serializer.SerializeJson(credential.Tags, false);

            string query =
                "INSERT INTO credentials (id, tenant_id, user_id, name, bearer_token, active, labels_json, tags_json, created_utc, last_update_utc) VALUES (" +
                "'" + _Driver.Sanitize(credential.Id) + "', " +
                "'" + _Driver.Sanitize(credential.TenantId) + "', " +
                "'" + _Driver.Sanitize(credential.UserId) + "', " +
                _Driver.FormatNullableString(credential.Name) + ", " +
                "'" + _Driver.Sanitize(credential.BearerToken) + "', " +
                _Driver.FormatBoolean(credential.Active) + ", " +
                "'" + _Driver.Sanitize(labelsJson) + "', " +
                "'" + _Driver.Sanitize(tagsJson) + "', " +
                "'" + _Driver.FormatDateTime(credential.CreatedUtc) + "', " +
                "'" + _Driver.FormatDateTime(credential.LastUpdateUtc) + "'" +
                ");";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return credential;
        }

        /// <summary>
        /// Read a credential by ID.
        /// </summary>
        /// <param name="id">Credential ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The credential, or null if not found.</returns>
        public async Task<Credential?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM credentials WHERE id = '" + _Driver.Sanitize(id) + "' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count < 1) return null;
            return Credential.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Read a credential by bearer token.
        /// </summary>
        /// <param name="bearerToken">Bearer token value.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The credential, or null if not found.</returns>
        public async Task<Credential?> ReadByBearerTokenAsync(string bearerToken, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(bearerToken)) throw new ArgumentNullException(nameof(bearerToken));

            string query = "SELECT * FROM credentials WHERE bearer_token = '" + _Driver.Sanitize(bearerToken) + "' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count < 1) return null;
            return Credential.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Update an existing credential.
        /// </summary>
        /// <param name="credential">Credential to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated credential.</returns>
        public async Task<Credential> UpdateAsync(Credential credential, CancellationToken token = default)
        {
            if (credential == null) throw new ArgumentNullException(nameof(credential));

            credential.LastUpdateUtc = DateTime.UtcNow;

            SerializationHelper.Serializer serializer = new SerializationHelper.Serializer();
            string labelsJson = serializer.SerializeJson(credential.Labels, false);
            string tagsJson = serializer.SerializeJson(credential.Tags, false);

            string query =
                "UPDATE credentials SET " +
                "tenant_id = '" + _Driver.Sanitize(credential.TenantId) + "', " +
                "user_id = '" + _Driver.Sanitize(credential.UserId) + "', " +
                "name = " + _Driver.FormatNullableString(credential.Name) + ", " +
                "bearer_token = '" + _Driver.Sanitize(credential.BearerToken) + "', " +
                "active = " + _Driver.FormatBoolean(credential.Active) + ", " +
                "labels_json = '" + _Driver.Sanitize(labelsJson) + "', " +
                "tags_json = '" + _Driver.Sanitize(tagsJson) + "', " +
                "last_update_utc = '" + _Driver.FormatDateTime(credential.LastUpdateUtc) + "' " +
                "WHERE id = '" + _Driver.Sanitize(credential.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return credential;
        }

        /// <summary>
        /// Delete a credential by ID.
        /// </summary>
        /// <param name="id">Credential ID.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM credentials WHERE id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a credential exists by ID.
        /// </summary>
        /// <param name="id">Credential ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the credential exists.</returns>
        public async Task<bool> ExistsByIdAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM credentials WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count < 1) return false;
            return Convert.ToInt64(result.Rows[0]["cnt"]) > 0;
        }

        /// <summary>
        /// Enumerate credentials with pagination and filtering, scoped to a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="request">Enumeration request parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result.</returns>
        public async Task<EnumerationResult<Credential>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (request == null) throw new ArgumentNullException(nameof(request));

            List<string> conditions = new List<string>();
            conditions.Add("tenant_id = '" + _Driver.Sanitize(tenantId) + "'");

            if (!string.IsNullOrEmpty(request.NameFilter))
                conditions.Add("name ILIKE '%" + _Driver.Sanitize(request.NameFilter) + "%'");

            if (!string.IsNullOrEmpty(request.LabelFilter))
                conditions.Add("labels_json ILIKE '%\"" + _Driver.Sanitize(request.LabelFilter) + "\"%'");

            if (!string.IsNullOrEmpty(request.TagKeyFilter))
                conditions.Add("tags_json ILIKE '%\"" + _Driver.Sanitize(request.TagKeyFilter) + "\"%'");

            if (!string.IsNullOrEmpty(request.TagValueFilter))
                conditions.Add("tags_json ILIKE '%\"" + _Driver.Sanitize(request.TagValueFilter) + "\"%'");

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

            string countQuery = "SELECT COUNT(*) AS cnt FROM credentials " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);

            int fetchCount = request.MaxResults + 1;
            string dataQuery = "SELECT * FROM credentials " + whereClause + " " + orderByClause + " LIMIT " + fetchCount + ";";
            DataTable dataResult = await _Driver.ExecuteQueryAsync(dataQuery, false, token).ConfigureAwait(false);

            List<Credential> credentials = Credential.FromDataTable(dataResult);

            EnumerationResult<Credential> result = new EnumerationResult<Credential>();
            result.TotalCount = totalCount;

            if (credentials.Count > request.MaxResults)
            {
                result.HasMore = true;
                credentials = credentials.GetRange(0, request.MaxResults);
            }

            result.Data = credentials;

            if (result.HasMore && credentials.Count > 0)
                result.ContinuationToken = credentials[credentials.Count - 1].Id;

            return result;
        }

        /// <summary>
        /// Count credentials in a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total number of credentials in the tenant.</returns>
        public async Task<long> CountAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            string query = "SELECT COUNT(*) AS cnt FROM credentials WHERE tenant_id = '" + _Driver.Sanitize(tenantId) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return Convert.ToInt64(result.Rows[0]["cnt"]);
        }
    }
}
