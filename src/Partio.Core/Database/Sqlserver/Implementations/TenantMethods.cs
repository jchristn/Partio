namespace Partio.Core.Database.Sqlserver.Implementations
{
    using System.Data;
    using System.Text;
    using Partio.Core.Database.Interfaces;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using SyslogLogging;

    /// <summary>
    /// SQL Server implementation of tenant database operations.
    /// </summary>
    public class TenantMethods : ITenantMethods
    {
        private readonly SqlServerDatabaseDriver _Driver;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[TenantMethods] ";

        /// <summary>
        /// Initialize a new instance of TenantMethods.
        /// </summary>
        /// <param name="driver">SQL Server database driver.</param>
        /// <param name="logging">Logging module.</param>
        public TenantMethods(SqlServerDatabaseDriver driver, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Create a new tenant.
        /// </summary>
        /// <param name="tenant">Tenant metadata to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created tenant.</returns>
        public async Task<TenantMetadata> CreateAsync(TenantMetadata tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));

            SerializationHelper.Serializer serializer = new SerializationHelper.Serializer();
            string labelsJson = serializer.SerializeJson(tenant.Labels, false);
            string tagsJson = serializer.SerializeJson(tenant.Tags, false);

            string query =
                "INSERT INTO tenants (id, name, active, labels_json, tags_json, created_utc, last_update_utc) VALUES (" +
                "'" + _Driver.Sanitize(tenant.Id) + "', " +
                "'" + _Driver.Sanitize(tenant.Name) + "', " +
                _Driver.FormatBoolean(tenant.Active) + ", " +
                "'" + _Driver.Sanitize(labelsJson) + "', " +
                "'" + _Driver.Sanitize(tagsJson) + "', " +
                "'" + _Driver.FormatDateTime(tenant.CreatedUtc) + "', " +
                "'" + _Driver.FormatDateTime(tenant.LastUpdateUtc) + "'" +
                ");";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return tenant;
        }

        /// <summary>
        /// Read a tenant by ID.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The tenant, or null if not found.</returns>
        public async Task<TenantMetadata?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT TOP 1 * FROM tenants WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count < 1) return null;
            return TenantMetadata.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Update an existing tenant.
        /// </summary>
        /// <param name="tenant">Tenant metadata to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated tenant.</returns>
        public async Task<TenantMetadata> UpdateAsync(TenantMetadata tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));

            tenant.LastUpdateUtc = DateTime.UtcNow;

            SerializationHelper.Serializer serializer = new SerializationHelper.Serializer();
            string labelsJson = serializer.SerializeJson(tenant.Labels, false);
            string tagsJson = serializer.SerializeJson(tenant.Tags, false);

            string query =
                "UPDATE tenants SET " +
                "name = '" + _Driver.Sanitize(tenant.Name) + "', " +
                "active = " + _Driver.FormatBoolean(tenant.Active) + ", " +
                "labels_json = '" + _Driver.Sanitize(labelsJson) + "', " +
                "tags_json = '" + _Driver.Sanitize(tagsJson) + "', " +
                "last_update_utc = '" + _Driver.FormatDateTime(tenant.LastUpdateUtc) + "' " +
                "WHERE id = '" + _Driver.Sanitize(tenant.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return tenant;
        }

        /// <summary>
        /// Delete a tenant by ID.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM tenants WHERE id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a tenant exists by ID.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the tenant exists.</returns>
        public async Task<bool> ExistsByIdAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM tenants WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count < 1) return false;
            return Convert.ToInt64(result.Rows[0]["cnt"]) > 0;
        }

        /// <summary>
        /// Enumerate tenants with pagination and filtering.
        /// </summary>
        /// <param name="request">Enumeration request parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result.</returns>
        public async Task<EnumerationResult<TenantMetadata>> EnumerateAsync(EnumerationRequest request, CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            StringBuilder whereClause = new StringBuilder();
            List<string> conditions = new List<string>();

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

            if (conditions.Count > 0)
                whereClause.Append("WHERE " + string.Join(" AND ", conditions));

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

            string countQuery = "SELECT COUNT(*) AS cnt FROM tenants " + whereClause.ToString() + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);

            int fetchCount = request.MaxResults + 1;
            string dataQuery = "SELECT TOP " + fetchCount + " * FROM tenants " + whereClause.ToString() + " " + orderByClause + ";";
            DataTable dataResult = await _Driver.ExecuteQueryAsync(dataQuery, false, token).ConfigureAwait(false);

            List<TenantMetadata> tenants = TenantMetadata.FromDataTable(dataResult);

            EnumerationResult<TenantMetadata> result = new EnumerationResult<TenantMetadata>();
            result.TotalCount = totalCount;

            if (tenants.Count > request.MaxResults)
            {
                result.HasMore = true;
                tenants = tenants.GetRange(0, request.MaxResults);
            }

            result.Data = tenants;

            if (result.HasMore && tenants.Count > 0)
                result.ContinuationToken = tenants[tenants.Count - 1].Id;

            return result;
        }

        /// <summary>
        /// Count total tenants.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total number of tenants.</returns>
        public async Task<long> CountAsync(CancellationToken token = default)
        {
            string query = "SELECT COUNT(*) AS cnt FROM tenants;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return Convert.ToInt64(result.Rows[0]["cnt"]);
        }
    }
}
