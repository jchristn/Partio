namespace Partio.Core.Models
{
    using System.Data;

    /// <summary>
    /// Represents a tenant in the Partio system.
    /// </summary>
    public class TenantMetadata
    {
        private string _Id = IdGenerator.NewTenantId();
        private string _Name = string.Empty;
        private bool _Active = true;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();
        private DateTime _CreatedUtc = DateTime.UtcNow;
        private DateTime _LastUpdateUtc = DateTime.UtcNow;

        /// <summary>
        /// K-sortable unique identifier with prefix 'ten_'.
        /// </summary>
        /// <remarks>48 characters.</remarks>
        public string Id
        {
            get => _Id;
            set => _Id = value ?? throw new ArgumentNullException(nameof(Id));
        }

        /// <summary>
        /// Tenant name.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = !string.IsNullOrEmpty(value)
                ? value
                : throw new ArgumentException("Name must not be null or empty.", nameof(Name));
        }

        /// <summary>
        /// Whether the tenant is active.
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
        /// UTC timestamp when the tenant was created.
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
        /// Create a TenantMetadata from a DataRow.
        /// </summary>
        /// <param name="row">DataRow containing tenant data.</param>
        /// <returns>A TenantMetadata instance.</returns>
        public static TenantMetadata FromDataRow(DataRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));

            TenantMetadata tenant = new TenantMetadata();
            tenant.Id = row["id"].ToString() ?? string.Empty;
            tenant.Name = row["name"].ToString() ?? string.Empty;
            tenant.Active = Convert.ToBoolean(row["active"]);
            tenant.CreatedUtc = DateTime.Parse(row["created_utc"].ToString() ?? DateTime.UtcNow.ToString("o")).ToUniversalTime();
            tenant.LastUpdateUtc = DateTime.Parse(row["last_update_utc"].ToString() ?? DateTime.UtcNow.ToString("o")).ToUniversalTime();

            string? labelsJson = row["labels_json"] == DBNull.Value ? null : row["labels_json"].ToString();
            if (!string.IsNullOrEmpty(labelsJson))
                tenant.Labels = new SerializationHelper.Serializer().DeserializeJson<List<string>>(labelsJson) ?? new List<string>();

            string? tagsJson = row["tags_json"] == DBNull.Value ? null : row["tags_json"].ToString();
            if (!string.IsNullOrEmpty(tagsJson))
                tenant.Tags = new SerializationHelper.Serializer().DeserializeJson<Dictionary<string, string>>(tagsJson) ?? new Dictionary<string, string>();

            return tenant;
        }

        /// <summary>
        /// Create a list of TenantMetadata from a DataTable.
        /// </summary>
        /// <param name="table">DataTable containing tenant data.</param>
        /// <returns>A list of TenantMetadata instances.</returns>
        public static List<TenantMetadata> FromDataTable(DataTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            List<TenantMetadata> tenants = new List<TenantMetadata>();
            foreach (DataRow row in table.Rows)
            {
                tenants.Add(FromDataRow(row));
            }
            return tenants;
        }
    }
}
