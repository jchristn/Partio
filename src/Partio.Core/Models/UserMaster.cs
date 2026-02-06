namespace Partio.Core.Models
{
    using System.Data;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Represents a user in the Partio system.
    /// </summary>
    public class UserMaster
    {
        private string _Id = IdGenerator.NewUserId();
        private string _TenantId = string.Empty;
        private string _Email = string.Empty;
        private string _PasswordSha256 = string.Empty;
        private string? _FirstName = null;
        private string? _LastName = null;
        private bool _IsAdmin = false;
        private bool _Active = true;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();
        private DateTime _CreatedUtc = DateTime.UtcNow;
        private DateTime _LastUpdateUtc = DateTime.UtcNow;

        /// <summary>
        /// K-sortable unique identifier with prefix 'usr_'.
        /// </summary>
        /// <remarks>48 characters.</remarks>
        public string Id
        {
            get => _Id;
            set => _Id = value ?? throw new ArgumentNullException(nameof(Id));
        }

        /// <summary>
        /// Tenant ID this user belongs to.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = value ?? throw new ArgumentNullException(nameof(TenantId));
        }

        /// <summary>
        /// User email address.
        /// </summary>
        public string Email
        {
            get => _Email;
            set => _Email = !string.IsNullOrEmpty(value)
                ? value
                : throw new ArgumentException("Email must not be null or empty.", nameof(Email));
        }

        /// <summary>
        /// SHA256 hash of the user's password (64-char hex).
        /// </summary>
        public string PasswordSha256
        {
            get => _PasswordSha256;
            set => _PasswordSha256 = value ?? string.Empty;
        }

        /// <summary>
        /// User's first name.
        /// </summary>
        public string? FirstName
        {
            get => _FirstName;
            set => _FirstName = value;
        }

        /// <summary>
        /// User's last name.
        /// </summary>
        public string? LastName
        {
            get => _LastName;
            set => _LastName = value;
        }

        /// <summary>
        /// Whether this user has admin privileges.
        /// </summary>
        /// <remarks>Default: false.</remarks>
        public bool IsAdmin
        {
            get => _IsAdmin;
            set => _IsAdmin = value;
        }

        /// <summary>
        /// Whether the user is active.
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
        /// UTC timestamp when the user was created.
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
        /// Set the password by computing its SHA256 hash.
        /// </summary>
        /// <param name="plainText">Plain text password.</param>
        public void SetPassword(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) throw new ArgumentException("Password must not be null or empty.", nameof(plainText));
            _PasswordSha256 = ComputePasswordHash(plainText);
        }

        /// <summary>
        /// Verify a plain text password against the stored hash.
        /// </summary>
        /// <param name="plainText">Plain text password to verify.</param>
        /// <returns>True if the password matches.</returns>
        public bool VerifyPassword(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return false;
            return string.Equals(ComputePasswordHash(plainText), _PasswordSha256, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Compute the SHA256 hash of a password string.
        /// </summary>
        /// <param name="plainText">Plain text password.</param>
        /// <returns>64-character lowercase hex hash.</returns>
        public static string ComputePasswordHash(string plainText)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainText));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        /// <summary>
        /// Create a redacted copy of the user (password masked).
        /// </summary>
        /// <param name="user">User to redact.</param>
        /// <returns>A copy of the user with PasswordSha256 set to "********".</returns>
        public static UserMaster Redact(UserMaster user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            UserMaster redacted = new UserMaster();
            redacted.Id = user.Id;
            redacted.TenantId = user.TenantId;
            redacted.Email = user.Email;
            redacted.PasswordSha256 = "********";
            redacted.FirstName = user.FirstName;
            redacted.LastName = user.LastName;
            redacted.IsAdmin = user.IsAdmin;
            redacted.Active = user.Active;
            redacted.Labels = new List<string>(user.Labels);
            redacted.Tags = new Dictionary<string, string>(user.Tags);
            redacted.CreatedUtc = user.CreatedUtc;
            redacted.LastUpdateUtc = user.LastUpdateUtc;
            return redacted;
        }

        /// <summary>
        /// Create a UserMaster from a DataRow.
        /// </summary>
        /// <param name="row">DataRow containing user data.</param>
        /// <returns>A UserMaster instance.</returns>
        public static UserMaster FromDataRow(DataRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));

            UserMaster user = new UserMaster();
            user.Id = row["id"].ToString() ?? string.Empty;
            user.TenantId = row["tenant_id"].ToString() ?? string.Empty;
            user.Email = row["email"].ToString() ?? string.Empty;
            user.PasswordSha256 = row["password_sha256"].ToString() ?? string.Empty;
            user.FirstName = row["first_name"] == DBNull.Value ? null : row["first_name"].ToString();
            user.LastName = row["last_name"] == DBNull.Value ? null : row["last_name"].ToString();
            user.IsAdmin = Convert.ToBoolean(row["is_admin"]);
            user.Active = Convert.ToBoolean(row["active"]);
            user.CreatedUtc = DateTime.Parse(row["created_utc"].ToString() ?? DateTime.UtcNow.ToString("o")).ToUniversalTime();
            user.LastUpdateUtc = DateTime.Parse(row["last_update_utc"].ToString() ?? DateTime.UtcNow.ToString("o")).ToUniversalTime();

            string? labelsJson = row["labels_json"] == DBNull.Value ? null : row["labels_json"].ToString();
            if (!string.IsNullOrEmpty(labelsJson))
                user.Labels = new SerializationHelper.Serializer().DeserializeJson<List<string>>(labelsJson) ?? new List<string>();

            string? tagsJson = row["tags_json"] == DBNull.Value ? null : row["tags_json"].ToString();
            if (!string.IsNullOrEmpty(tagsJson))
                user.Tags = new SerializationHelper.Serializer().DeserializeJson<Dictionary<string, string>>(tagsJson) ?? new Dictionary<string, string>();

            return user;
        }

        /// <summary>
        /// Create a list of UserMaster from a DataTable.
        /// </summary>
        /// <param name="table">DataTable containing user data.</param>
        /// <returns>A list of UserMaster instances.</returns>
        public static List<UserMaster> FromDataTable(DataTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            List<UserMaster> users = new List<UserMaster>();
            foreach (DataRow row in table.Rows)
            {
                users.Add(FromDataRow(row));
            }
            return users;
        }
    }
}
