namespace Partio.Core.Settings
{
    using Partio.Core.Enums;

    /// <summary>
    /// Database connection settings.
    /// </summary>
    public class DatabaseSettings
    {
        private DatabaseTypeEnum _Type = DatabaseTypeEnum.Sqlite;
        private string _Filename = "./partio.db";
        private string? _Hostname = null;
        private int _Port = 0;
        private string? _DatabaseName = null;
        private string? _Username = null;
        private string? _Password = null;
        private string? _Instance = null;
        private string? _Schema = null;
        private bool _RequireEncryption = false;
        private bool _LogQueries = false;

        /// <summary>
        /// Database type.
        /// </summary>
        /// <remarks>Default: Sqlite.</remarks>
        public DatabaseTypeEnum Type
        {
            get => _Type;
            set => _Type = value;
        }

        /// <summary>
        /// Database filename (SQLite only).
        /// </summary>
        /// <remarks>Default: ./partio.db.</remarks>
        public string Filename
        {
            get => _Filename;
            set => _Filename = value ?? throw new ArgumentNullException(nameof(Filename));
        }

        /// <summary>
        /// Database server hostname.
        /// </summary>
        public string? Hostname
        {
            get => _Hostname;
            set => _Hostname = value;
        }

        /// <summary>
        /// Database server port.
        /// </summary>
        public int Port
        {
            get => _Port;
            set => _Port = value;
        }

        /// <summary>
        /// Database name.
        /// </summary>
        public string? DatabaseName
        {
            get => _DatabaseName;
            set => _DatabaseName = value;
        }

        /// <summary>
        /// Database username.
        /// </summary>
        public string? Username
        {
            get => _Username;
            set => _Username = value;
        }

        /// <summary>
        /// Database password.
        /// </summary>
        public string? Password
        {
            get => _Password;
            set => _Password = value;
        }

        /// <summary>
        /// Database instance (SQL Server).
        /// </summary>
        public string? Instance
        {
            get => _Instance;
            set => _Instance = value;
        }

        /// <summary>
        /// Database schema.
        /// </summary>
        public string? Schema
        {
            get => _Schema;
            set => _Schema = value;
        }

        /// <summary>
        /// Require encryption for the database connection.
        /// </summary>
        /// <remarks>Default: false.</remarks>
        public bool RequireEncryption
        {
            get => _RequireEncryption;
            set => _RequireEncryption = value;
        }

        /// <summary>
        /// Log database queries to the console/log.
        /// </summary>
        /// <remarks>Default: false.</remarks>
        public bool LogQueries
        {
            get => _LogQueries;
            set => _LogQueries = value;
        }
    }
}
