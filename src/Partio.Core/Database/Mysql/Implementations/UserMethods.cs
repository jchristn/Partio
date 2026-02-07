namespace Partio.Core.Database.Mysql.Implementations
{
    using System.Data;
    using System.Text;
    using Partio.Core.Database.Interfaces;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using SyslogLogging;

    /// <summary>
    /// MySQL implementation of user database operations.
    /// </summary>
    public class UserMethods : IUserMethods
    {
        private readonly MysqlDatabaseDriver _Driver;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[UserMethods] ";

        /// <summary>
        /// Initialize a new instance of UserMethods.
        /// </summary>
        /// <param name="driver">MySQL database driver.</param>
        /// <param name="logging">Logging module.</param>
        public UserMethods(MysqlDatabaseDriver driver, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Create a new user.
        /// </summary>
        /// <param name="user">User to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created user.</returns>
        public async Task<UserMaster> CreateAsync(UserMaster user, CancellationToken token = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            SerializationHelper.Serializer serializer = new SerializationHelper.Serializer();
            string labelsJson = serializer.SerializeJson(user.Labels, false);
            string tagsJson = serializer.SerializeJson(user.Tags, false);

            string query =
                "INSERT INTO users (id, tenant_id, email, password_sha256, first_name, last_name, is_admin, active, labels_json, tags_json, created_utc, last_update_utc) VALUES (" +
                "'" + _Driver.Sanitize(user.Id) + "', " +
                "'" + _Driver.Sanitize(user.TenantId) + "', " +
                "'" + _Driver.Sanitize(user.Email) + "', " +
                "'" + _Driver.Sanitize(user.PasswordSha256) + "', " +
                _Driver.FormatNullableString(user.FirstName) + ", " +
                _Driver.FormatNullableString(user.LastName) + ", " +
                _Driver.FormatBoolean(user.IsAdmin) + ", " +
                _Driver.FormatBoolean(user.Active) + ", " +
                "'" + _Driver.Sanitize(labelsJson) + "', " +
                "'" + _Driver.Sanitize(tagsJson) + "', " +
                "'" + _Driver.FormatDateTime(user.CreatedUtc) + "', " +
                "'" + _Driver.FormatDateTime(user.LastUpdateUtc) + "'" +
                ");";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return user;
        }

        /// <summary>
        /// Read a user by ID.
        /// </summary>
        /// <param name="id">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The user, or null if not found.</returns>
        public async Task<UserMaster?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM users WHERE id = '" + _Driver.Sanitize(id) + "' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count < 1) return null;
            return UserMaster.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Read a user by email within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="email">User email address.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The user, or null if not found.</returns>
        public async Task<UserMaster?> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(email)) throw new ArgumentNullException(nameof(email));

            string query = "SELECT * FROM users WHERE tenant_id = '" + _Driver.Sanitize(tenantId) + "' AND email = '" + _Driver.Sanitize(email) + "' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count < 1) return null;
            return UserMaster.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Update an existing user.
        /// </summary>
        /// <param name="user">User to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated user.</returns>
        public async Task<UserMaster> UpdateAsync(UserMaster user, CancellationToken token = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            user.LastUpdateUtc = DateTime.UtcNow;

            SerializationHelper.Serializer serializer = new SerializationHelper.Serializer();
            string labelsJson = serializer.SerializeJson(user.Labels, false);
            string tagsJson = serializer.SerializeJson(user.Tags, false);

            string query =
                "UPDATE users SET " +
                "tenant_id = '" + _Driver.Sanitize(user.TenantId) + "', " +
                "email = '" + _Driver.Sanitize(user.Email) + "', " +
                "password_sha256 = '" + _Driver.Sanitize(user.PasswordSha256) + "', " +
                "first_name = " + _Driver.FormatNullableString(user.FirstName) + ", " +
                "last_name = " + _Driver.FormatNullableString(user.LastName) + ", " +
                "is_admin = " + _Driver.FormatBoolean(user.IsAdmin) + ", " +
                "active = " + _Driver.FormatBoolean(user.Active) + ", " +
                "labels_json = '" + _Driver.Sanitize(labelsJson) + "', " +
                "tags_json = '" + _Driver.Sanitize(tagsJson) + "', " +
                "last_update_utc = '" + _Driver.FormatDateTime(user.LastUpdateUtc) + "' " +
                "WHERE id = '" + _Driver.Sanitize(user.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return user;
        }

        /// <summary>
        /// Delete a user by ID.
        /// </summary>
        /// <param name="id">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM users WHERE id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a user exists by ID.
        /// </summary>
        /// <param name="id">User ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the user exists.</returns>
        public async Task<bool> ExistsByIdAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM users WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result.Rows.Count < 1) return false;
            return Convert.ToInt64(result.Rows[0]["cnt"]) > 0;
        }

        /// <summary>
        /// Enumerate users with pagination and filtering, scoped to a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="request">Enumeration request parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated enumeration result.</returns>
        public async Task<EnumerationResult<UserMaster>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (request == null) throw new ArgumentNullException(nameof(request));

            List<string> conditions = new List<string>();
            conditions.Add("tenant_id = '" + _Driver.Sanitize(tenantId) + "'");

            if (!string.IsNullOrEmpty(request.NameFilter))
                conditions.Add("email LIKE '%" + _Driver.Sanitize(request.NameFilter) + "%'");

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
                    orderByClause = "ORDER BY email ASC";
                    break;
                case EnumerationOrderEnum.NameDescending:
                    orderByClause = "ORDER BY email DESC";
                    break;
                default:
                    orderByClause = "ORDER BY created_utc DESC";
                    break;
            }

            string countQuery = "SELECT COUNT(*) AS cnt FROM users " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);

            int fetchCount = request.MaxResults + 1;
            string dataQuery = "SELECT * FROM users " + whereClause + " " + orderByClause + " LIMIT " + fetchCount + ";";
            DataTable dataResult = await _Driver.ExecuteQueryAsync(dataQuery, false, token).ConfigureAwait(false);

            List<UserMaster> users = UserMaster.FromDataTable(dataResult);

            EnumerationResult<UserMaster> result = new EnumerationResult<UserMaster>();
            result.TotalCount = totalCount;

            if (users.Count > request.MaxResults)
            {
                result.HasMore = true;
                users = users.GetRange(0, request.MaxResults);
            }

            result.Data = users;

            if (result.HasMore && users.Count > 0)
                result.ContinuationToken = users[users.Count - 1].Id;

            return result;
        }

        /// <summary>
        /// Count users in a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Total number of users in the tenant.</returns>
        public async Task<long> CountAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            string query = "SELECT COUNT(*) AS cnt FROM users WHERE tenant_id = '" + _Driver.Sanitize(tenantId) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return Convert.ToInt64(result.Rows[0]["cnt"]);
        }
    }
}
