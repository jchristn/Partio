namespace Partio.Core.Models
{
    using System.Data;

    /// <summary>
    /// Represents an API credential (bearer token) in the Partio system.
    /// </summary>
    public class Credential
    {
        private string _Id = IdGenerator.NewCredentialId();
        private string _TenantId = string.Empty;
        private string _UserId = string.Empty;
        private string? _Name = null;
        private string _BearerToken = IdGenerator.NewBearerToken();
        private bool _Active = true;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();
        private DateTime _CreatedUtc = DateTime.UtcNow;
        private DateTime _LastUpdateUtc = DateTime.UtcNow;

        /// <summary>
        /// K-sortable unique identifier with prefix 'cred_'.
        /// </summary>
        /// <remarks>48 characters.</remarks>
        public string Id
        {
            get => _Id;
            set => _Id = value ?? throw new ArgumentNullException(nameof(Id));
        }

        /// <summary>
        /// Tenant ID this credential belongs to.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = value ?? throw new ArgumentNullException(nameof(TenantId));
        }

        /// <summary>
        /// User ID this credential belongs to.
        /// </summary>
        public string UserId
        {
            get => _UserId;
            set => _UserId = value ?? throw new ArgumentNullException(nameof(UserId));
        }

        /// <summary>
        /// Friendly name for this credential.
        /// </summary>
        public string? Name
        {
            get => _Name;
            set => _Name = value;
        }

        /// <summary>
        /// Bearer token for API authentication (64-char random alphanumeric).
        /// </summary>
        public string BearerToken
        {
            get => _BearerToken;
            set => _BearerToken = value ?? throw new ArgumentNullException(nameof(BearerToken));
        }

        /// <summary>
        /// Whether the credential is active.
        /// </summary>
        /// <remarks>Default: true.</remarks>
        public bool Active
        {
            get => _Active;
            set => _Active = value;
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
        /// UTC timestamp when the credential was created.
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
        /// Create a Credential from a DataRow.
        /// </summary>
        /// <param name="row">DataRow containing credential data.</param>
        /// <returns>A Credential instance.</returns>
        public static Credential FromDataRow(DataRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));

            Credential cred = new Credential();
            cred.Id = row["id"].ToString() ?? string.Empty;
            cred.TenantId = row["tenant_id"].ToString() ?? string.Empty;
            cred.UserId = row["user_id"].ToString() ?? string.Empty;
            cred.Name = row["name"] == DBNull.Value ? null : row["name"].ToString();
            cred.BearerToken = row["bearer_token"].ToString() ?? string.Empty;
            cred.Active = Convert.ToBoolean(row["active"]);
            cred.CreatedUtc = DateTime.Parse(row["created_utc"].ToString() ?? DateTime.UtcNow.ToString("o")).ToUniversalTime();
            cred.LastUpdateUtc = DateTime.Parse(row["last_update_utc"].ToString() ?? DateTime.UtcNow.ToString("o")).ToUniversalTime();

            string? labelsJson = row["labels_json"] == DBNull.Value ? null : row["labels_json"].ToString();
            if (!string.IsNullOrEmpty(labelsJson))
                cred.Labels = new SerializationHelper.Serializer().DeserializeJson<List<string>>(labelsJson) ?? new List<string>();

            string? tagsJson = row["tags_json"] == DBNull.Value ? null : row["tags_json"].ToString();
            if (!string.IsNullOrEmpty(tagsJson))
                cred.Tags = new SerializationHelper.Serializer().DeserializeJson<Dictionary<string, string>>(tagsJson) ?? new Dictionary<string, string>();

            return cred;
        }

        /// <summary>
        /// Create a list of Credential from a DataTable.
        /// </summary>
        /// <param name="table">DataTable containing credential data.</param>
        /// <returns>A list of Credential instances.</returns>
        public static List<Credential> FromDataTable(DataTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            List<Credential> credentials = new List<Credential>();
            foreach (DataRow row in table.Rows)
            {
                credentials.Add(FromDataRow(row));
            }
            return credentials;
        }
    }
}
