namespace Partio.Core.Database
{
    using System.Data;
    using Partio.Core.Database.Interfaces;
    using Partio.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// Abstract base class for database drivers.
    /// </summary>
    public abstract class DatabaseDriverBase
    {
        /// <summary>
        /// Logging module.
        /// </summary>
        protected readonly LoggingModule _Logging;

        /// <summary>
        /// Server settings.
        /// </summary>
        protected readonly ServerSettings _Settings;

        /// <summary>
        /// Header prefix for log messages.
        /// </summary>
        protected readonly string _Header = "[DatabaseDriver] ";

        /// <summary>
        /// Tenant database methods.
        /// </summary>
        public ITenantMethods Tenant { get; protected set; } = null!;

        /// <summary>
        /// User database methods.
        /// </summary>
        public IUserMethods User { get; protected set; } = null!;

        /// <summary>
        /// Credential database methods.
        /// </summary>
        public ICredentialMethods Credential { get; protected set; } = null!;

        /// <summary>
        /// Embedding endpoint database methods.
        /// </summary>
        public IEmbeddingEndpointMethods EmbeddingEndpoint { get; protected set; } = null!;

        /// <summary>
        /// Completion endpoint database methods.
        /// </summary>
        public ICompletionEndpointMethods CompletionEndpoint { get; protected set; } = null!;

        /// <summary>
        /// Request history database methods.
        /// </summary>
        public IRequestHistoryMethods RequestHistory { get; protected set; } = null!;

        /// <summary>
        /// Initialize a new instance of DatabaseDriverBase.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        protected DatabaseDriverBase(ServerSettings settings, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Initialize the database (create tables, etc.).
        /// </summary>
        public abstract Task InitializeAsync(CancellationToken token = default);

        /// <summary>
        /// Execute a single SQL query and return results.
        /// </summary>
        /// <param name="query">SQL query string.</param>
        /// <param name="isTransaction">Whether to execute in a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>DataTable with results.</returns>
        public abstract Task<DataTable> ExecuteQueryAsync(string query, bool isTransaction = false, CancellationToken token = default);

        /// <summary>
        /// Execute multiple SQL queries sequentially.
        /// </summary>
        /// <param name="queries">SQL query strings.</param>
        /// <param name="isTransaction">Whether to execute in a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        public abstract Task ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default);

        /// <summary>
        /// Sanitize a string value for SQL (escape single quotes).
        /// </summary>
        /// <param name="value">Input value.</param>
        /// <returns>Sanitized value.</returns>
        protected string Sanitize(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("'", "''");
        }

        /// <summary>
        /// Format a boolean for SQL.
        /// </summary>
        /// <param name="value">Boolean value.</param>
        /// <returns>SQL boolean string (1 or 0).</returns>
        protected string FormatBoolean(bool value)
        {
            return value ? "1" : "0";
        }

        /// <summary>
        /// Format a DateTime for SQL storage.
        /// </summary>
        /// <param name="value">DateTime value.</param>
        /// <returns>ISO 8601 formatted string.</returns>
        protected string FormatDateTime(DateTime value)
        {
            return value.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
        }

        /// <summary>
        /// Format a nullable value for SQL.
        /// </summary>
        /// <param name="value">Nullable value.</param>
        /// <returns>SQL NULL or quoted value.</returns>
        protected string FormatNullable(object? value)
        {
            if (value == null) return "NULL";
            return value.ToString() ?? "NULL";
        }

        /// <summary>
        /// Format a nullable string for SQL.
        /// </summary>
        /// <param name="value">Nullable string.</param>
        /// <returns>SQL NULL or quoted/sanitized value.</returns>
        protected string FormatNullableString(string? value)
        {
            if (value == null) return "NULL";
            return "'" + Sanitize(value) + "'";
        }
    }
}
