namespace Partio.Core.Database.Sqlite.Implementations
{
    using System.Data;
    using System.Text;
    using Partio.Core.Database.Interfaces;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using SyslogLogging;

    /// <summary>
    /// SQLite implementation of request history database operations.
    /// </summary>
    public class RequestHistoryMethods : IRequestHistoryMethods
    {
        private readonly SqliteDatabaseDriver _Driver;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[RequestHistoryMethods] ";

        /// <summary>
        /// Initialize a new instance of RequestHistoryMethods.
        /// </summary>
        /// <param name="driver">SQLite database driver.</param>
        /// <param name="logging">Logging module.</param>
        public RequestHistoryMethods(SqliteDatabaseDriver driver, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Create a new request history entry.
        /// </summary>
        /// <param name="entry">Request history entry to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created request history entry.</returns>
        public async Task<RequestHistoryEntry> CreateAsync(RequestHistoryEntry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string query =
                "INSERT INTO request_history (id, tenant_id, user_id, credential_id, requestor_ip, http_method, http_url, request_body_length, response_body_length, http_status, response_time_ms, object_key, created_utc, completed_utc) VALUES (" +
                "'" + _Driver.Sanitize(entry.Id) + "', " +
                _Driver.FormatNullableString(entry.TenantId) + ", " +
                _Driver.FormatNullableString(entry.UserId) + ", " +
                _Driver.FormatNullableString(entry.CredentialId) + ", " +
                _Driver.FormatNullableString(entry.RequestorIp) + ", " +
                _Driver.FormatNullableString(entry.HttpMethod) + ", " +
                _Driver.FormatNullableString(entry.HttpUrl) + ", " +
                (entry.RequestBodyLength.HasValue ? entry.RequestBodyLength.Value.ToString() : "NULL") + ", " +
                (entry.ResponseBodyLength.HasValue ? entry.ResponseBodyLength.Value.ToString() : "NULL") + ", " +
                (entry.HttpStatus.HasValue ? entry.HttpStatus.Value.ToString() : "NULL") + ", " +
                (entry.ResponseTimeMs.HasValue ? entry.ResponseTimeMs.Value.ToString() : "NULL") + ", " +
                _Driver.FormatNullableString(entry.ObjectKey) + ", " +
                "'" + _Driver.FormatDateTime(entry.CreatedUtc) + "', " +
                (entry.CompletedUtc.HasValue ? "'" + _Driver.FormatDateTime(entry.CompletedUtc.Value) + "'" : "NULL") +
                ");";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return entry;
        }

        /// <summary>
        /// Update a request history entry.
        /// </summary>
        /// <param name="entry">Request history entry to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated request history entry.</returns>
        public async Task<RequestHistoryEntry> UpdateAsync(RequestHistoryEntry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string query =
                "UPDATE request_history SET " +
                "tenant_id = " + _Driver.FormatNullableString(entry.TenantId) + ", " +
                "user_id = " + _Driver.FormatNullableString(entry.UserId) + ", " +
                "credential_id = " + _Driver.FormatNullableString(entry.CredentialId) + ", " +
                "requestor_ip = " + _Driver.FormatNullableString(entry.RequestorIp) + ", " +
                "http_method = " + _Driver.FormatNullableString(entry.HttpMethod) + ", " +
                "http_url = " + _Driver.FormatNullableString(entry.HttpUrl) + ", " +
                "request_body_length = " + (entry.RequestBodyLength.HasValue ? entry.RequestBodyLength.Value.ToString() : "NULL") + ", " +
                "response_body_length = " + (entry.ResponseBodyLength.HasValue ? entry.ResponseBodyLength.Value.ToString() : "NULL") + ", " +
                "http_status = " + (entry.HttpStatus.HasValue ? entry.HttpStatus.Value.ToString() : "NULL") + ", " +
                "response_time_ms = " + (entry.ResponseTimeMs.HasValue ? entry.ResponseTimeMs.Value.ToString() : "NULL") + ", " +
                "object_key = " + _Driver.FormatNullableString(entry.ObjectKey) + ", " +
                "completed_utc = " + (entry.CompletedUtc.HasValue ? "'" + _Driver.FormatDateTime(entry.CompletedUtc.Value) + "'" : "NULL") + " " +
                "WHERE id = '" + _Driver.Sanitize(entry.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return entry;
        }

        /// <summary>
        /// Read a request history entry by ID.
        /// </summary>
        /// <param name="id">Request history entry ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The request history entry, or null if not found.</returns>
        public async Task<RequestHistoryEntry?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM request_history WHERE id = '" + _Driver.Sanitize(id) + "' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count < 1) return null;
            return RequestHistoryEntry.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Enumerate request history with pagination, scoped to a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="request">Enumeration request parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result.</returns>
        public async Task<EnumerationResult<RequestHistoryEntry>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (request == null) throw new ArgumentNullException(nameof(request));

            List<string> conditions = new List<string>();
            conditions.Add("tenant_id = '" + _Driver.Sanitize(tenantId) + "'");

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
                default:
                    orderByClause = "ORDER BY created_utc DESC";
                    break;
            }

            string countQuery = "SELECT COUNT(*) AS cnt FROM request_history " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);

            int fetchCount = request.MaxResults + 1;
            string dataQuery = "SELECT * FROM request_history " + whereClause + " " + orderByClause + " LIMIT " + fetchCount + ";";
            DataTable dataResult = await _Driver.ExecuteQueryAsync(dataQuery, false, token).ConfigureAwait(false);

            List<RequestHistoryEntry> entries = RequestHistoryEntry.FromDataTable(dataResult);

            EnumerationResult<RequestHistoryEntry> result = new EnumerationResult<RequestHistoryEntry>();
            result.TotalCount = totalCount;

            if (entries.Count > request.MaxResults)
            {
                result.HasMore = true;
                entries = entries.GetRange(0, request.MaxResults);
            }

            result.Data = entries;

            if (result.HasMore && entries.Count > 0)
                result.ContinuationToken = entries[entries.Count - 1].Id;

            return result;
        }

        /// <summary>
        /// Delete a request history entry by ID.
        /// </summary>
        /// <param name="id">Request history entry ID.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM request_history WHERE id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete expired request history entries before the specified cutoff.
        /// </summary>
        /// <param name="cutoff">UTC cutoff timestamp; entries created before this are deleted.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteExpiredAsync(DateTime cutoff, CancellationToken token = default)
        {
            string query = "DELETE FROM request_history WHERE created_utc < '" + _Driver.FormatDateTime(cutoff) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get object keys of expired request history entries for filesystem cleanup.
        /// </summary>
        /// <param name="cutoff">UTC cutoff timestamp; entries created before this are considered expired.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of non-null object keys from expired entries.</returns>
        public async Task<List<string>> GetExpiredObjectKeysAsync(DateTime cutoff, CancellationToken token = default)
        {
            string query = "SELECT object_key FROM request_history WHERE created_utc < '" + _Driver.FormatDateTime(cutoff) + "' AND object_key IS NOT NULL;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<string> objectKeys = new List<string>();
            foreach (DataRow row in result.Rows)
            {
                string? key = row["object_key"].ToString();
                if (!string.IsNullOrEmpty(key))
                    objectKeys.Add(key);
            }

            return objectKeys;
        }

        /// <summary>
        /// Count request history entries in a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total number of request history entries in the tenant.</returns>
        public async Task<long> CountAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            string query = "SELECT COUNT(*) AS cnt FROM request_history WHERE tenant_id = '" + _Driver.Sanitize(tenantId) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return Convert.ToInt64(result.Rows[0]["cnt"]);
        }
    }
}
