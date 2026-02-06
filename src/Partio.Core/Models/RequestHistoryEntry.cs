namespace Partio.Core.Models
{
    using System.Data;

    /// <summary>
    /// Represents a request history entry for auditing purposes.
    /// </summary>
    public class RequestHistoryEntry
    {
        private string _Id = IdGenerator.NewRequestHistoryId();
        private string? _TenantId = null;
        private string? _UserId = null;
        private string? _CredentialId = null;
        private string? _RequestorIp = null;
        private string? _HttpMethod = null;
        private string? _HttpUrl = null;
        private long? _RequestBodyLength = null;
        private long? _ResponseBodyLength = null;
        private int? _HttpStatus = null;
        private long? _ResponseTimeMs = null;
        private string? _ObjectKey = null;
        private DateTime _CreatedUtc = DateTime.UtcNow;
        private DateTime? _CompletedUtc = null;

        /// <summary>
        /// K-sortable unique identifier with prefix 'req_'.
        /// </summary>
        /// <remarks>48 characters.</remarks>
        public string Id
        {
            get => _Id;
            set => _Id = value ?? throw new ArgumentNullException(nameof(Id));
        }

        /// <summary>
        /// Tenant ID associated with the request.
        /// </summary>
        public string? TenantId
        {
            get => _TenantId;
            set => _TenantId = value;
        }

        /// <summary>
        /// User ID associated with the request.
        /// </summary>
        public string? UserId
        {
            get => _UserId;
            set => _UserId = value;
        }

        /// <summary>
        /// Credential ID used for authentication.
        /// </summary>
        public string? CredentialId
        {
            get => _CredentialId;
            set => _CredentialId = value;
        }

        /// <summary>
        /// IP address of the requestor.
        /// </summary>
        public string? RequestorIp
        {
            get => _RequestorIp;
            set => _RequestorIp = value;
        }

        /// <summary>
        /// HTTP method of the request.
        /// </summary>
        public string? HttpMethod
        {
            get => _HttpMethod;
            set => _HttpMethod = value;
        }

        /// <summary>
        /// HTTP URL of the request.
        /// </summary>
        public string? HttpUrl
        {
            get => _HttpUrl;
            set => _HttpUrl = value;
        }

        /// <summary>
        /// Length of the request body in bytes.
        /// </summary>
        public long? RequestBodyLength
        {
            get => _RequestBodyLength;
            set => _RequestBodyLength = value;
        }

        /// <summary>
        /// Length of the response body in bytes.
        /// </summary>
        public long? ResponseBodyLength
        {
            get => _ResponseBodyLength;
            set => _ResponseBodyLength = value;
        }

        /// <summary>
        /// HTTP status code of the response.
        /// </summary>
        public int? HttpStatus
        {
            get => _HttpStatus;
            set => _HttpStatus = value;
        }

        /// <summary>
        /// Response time in milliseconds.
        /// </summary>
        public long? ResponseTimeMs
        {
            get => _ResponseTimeMs;
            set => _ResponseTimeMs = value;
        }

        /// <summary>
        /// Filesystem BLOB key for the request/response body detail.
        /// </summary>
        public string? ObjectKey
        {
            get => _ObjectKey;
            set => _ObjectKey = value;
        }

        /// <summary>
        /// UTC timestamp when the request was received.
        /// </summary>
        public DateTime CreatedUtc
        {
            get => _CreatedUtc;
            set => _CreatedUtc = value;
        }

        /// <summary>
        /// UTC timestamp when the response was sent.
        /// </summary>
        public DateTime? CompletedUtc
        {
            get => _CompletedUtc;
            set => _CompletedUtc = value;
        }

        /// <summary>
        /// Create a RequestHistoryEntry from a DataRow.
        /// </summary>
        /// <param name="row">DataRow containing request history data.</param>
        /// <returns>A RequestHistoryEntry instance.</returns>
        public static RequestHistoryEntry FromDataRow(DataRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));

            RequestHistoryEntry entry = new RequestHistoryEntry();
            entry.Id = row["id"].ToString() ?? string.Empty;
            entry.TenantId = row["tenant_id"] == DBNull.Value ? null : row["tenant_id"].ToString();
            entry.UserId = row["user_id"] == DBNull.Value ? null : row["user_id"].ToString();
            entry.CredentialId = row["credential_id"] == DBNull.Value ? null : row["credential_id"].ToString();
            entry.RequestorIp = row["requestor_ip"] == DBNull.Value ? null : row["requestor_ip"].ToString();
            entry.HttpMethod = row["http_method"] == DBNull.Value ? null : row["http_method"].ToString();
            entry.HttpUrl = row["http_url"] == DBNull.Value ? null : row["http_url"].ToString();
            entry.RequestBodyLength = row["request_body_length"] == DBNull.Value ? null : Convert.ToInt64(row["request_body_length"]);
            entry.ResponseBodyLength = row["response_body_length"] == DBNull.Value ? null : Convert.ToInt64(row["response_body_length"]);
            entry.HttpStatus = row["http_status"] == DBNull.Value ? null : Convert.ToInt32(row["http_status"]);
            entry.ResponseTimeMs = row["response_time_ms"] == DBNull.Value ? null : Convert.ToInt64(row["response_time_ms"]);
            entry.ObjectKey = row["object_key"] == DBNull.Value ? null : row["object_key"].ToString();
            entry.CreatedUtc = DateTime.Parse(row["created_utc"].ToString() ?? DateTime.UtcNow.ToString("o")).ToUniversalTime();
            entry.CompletedUtc = row["completed_utc"] == DBNull.Value ? null : DateTime.Parse(row["completed_utc"].ToString()!).ToUniversalTime();

            return entry;
        }

        /// <summary>
        /// Create a list of RequestHistoryEntry from a DataTable.
        /// </summary>
        /// <param name="table">DataTable containing request history data.</param>
        /// <returns>A list of RequestHistoryEntry instances.</returns>
        public static List<RequestHistoryEntry> FromDataTable(DataTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            List<RequestHistoryEntry> entries = new List<RequestHistoryEntry>();
            foreach (DataRow row in table.Rows)
            {
                entries.Add(FromDataRow(row));
            }
            return entries;
        }
    }
}
