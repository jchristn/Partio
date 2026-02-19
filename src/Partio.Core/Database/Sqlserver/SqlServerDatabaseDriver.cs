namespace Partio.Core.Database.Sqlserver
{
    using System.Data;
    using Microsoft.Data.SqlClient;
    using Partio.Core.Database.Sqlserver.Implementations;
    using Partio.Core.Database.Sqlserver.Queries;
    using Partio.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// SQL Server implementation of the database driver.
    /// </summary>
    public class SqlServerDatabaseDriver : DatabaseDriverBase
    {
        private readonly string _ConnectionString;

        /// <summary>
        /// Connection string for internal use by implementation classes.
        /// </summary>
        internal string ConnectionString => _ConnectionString;

        /// <summary>
        /// Initialize a new instance of SqlServerDatabaseDriver.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        public SqlServerDatabaseDriver(ServerSettings settings, LoggingModule logging)
            : base(settings, logging)
        {
            string server = settings.Database.Hostname ?? "localhost";
            if (!string.IsNullOrEmpty(settings.Database.Instance))
                server += "\\" + settings.Database.Instance;
            if (settings.Database.Port > 0)
                server += "," + settings.Database.Port;

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder.DataSource = server;

            if (!string.IsNullOrEmpty(settings.Database.DatabaseName))
                builder.InitialCatalog = settings.Database.DatabaseName;

            if (!string.IsNullOrEmpty(settings.Database.Username))
                builder.UserID = settings.Database.Username;

            if (!string.IsNullOrEmpty(settings.Database.Password))
                builder.Password = settings.Database.Password;

            builder.Encrypt = settings.Database.RequireEncryption;
            builder.TrustServerCertificate = !settings.Database.RequireEncryption;

            _ConnectionString = builder.ConnectionString;
            _Logging.Info(_Header + "initialized SQL Server driver using server " + server);
        }

        /// <summary>
        /// Initialize the database by creating all tables.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public override async Task InitializeAsync(CancellationToken token = default)
        {
            _Logging.Info(_Header + "initializing SQL Server database");

            foreach (string query in SetupQueries.AllTables)
            {
                await ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            }

            Tenant = new TenantMethods(this, _Logging);
            User = new UserMethods(this, _Logging);
            Credential = new CredentialMethods(this, _Logging);
            EmbeddingEndpoint = new EmbeddingEndpointMethods(this, _Logging);
            CompletionEndpoint = new CompletionEndpointMethods(this, _Logging);
            RequestHistory = new RequestHistoryMethods(this, _Logging);

            _Logging.Info(_Header + "SQL Server database initialized successfully");
        }

        /// <summary>
        /// Execute a single SQL query and return results as a DataTable.
        /// </summary>
        /// <param name="query">SQL query string.</param>
        /// <param name="isTransaction">Whether to execute in a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>DataTable with results.</returns>
        public override async Task<DataTable> ExecuteQueryAsync(string query, bool isTransaction = false, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));

            if (_Settings.Database.LogQueries)
                _Logging.Debug(_Header + "query: " + query);

            DataTable result = new DataTable();

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                SqlTransaction? transaction = null;
                if (isTransaction)
                    transaction = connection.BeginTransaction();

                try
                {
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        if (transaction != null)
                            command.Transaction = transaction;

                        using (SqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                        {
                            result.Load(reader);
                        }
                    }

                    if (transaction != null)
                        await transaction.CommitAsync(token).ConfigureAwait(false);
                }
                catch
                {
                    if (transaction != null)
                        await transaction.RollbackAsync(token).ConfigureAwait(false);
                    throw;
                }
                finally
                {
                    if (transaction != null)
                        transaction.Dispose();
                }
            }

            return result;
        }

        /// <summary>
        /// Execute multiple SQL queries sequentially.
        /// </summary>
        /// <param name="queries">SQL query strings.</param>
        /// <param name="isTransaction">Whether to execute in a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        public override async Task ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default)
        {
            if (queries == null) throw new ArgumentNullException(nameof(queries));

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                SqlTransaction? transaction = null;
                if (isTransaction)
                    transaction = connection.BeginTransaction();

                try
                {
                    foreach (string query in queries)
                    {
                        if (string.IsNullOrEmpty(query)) continue;

                        if (_Settings.Database.LogQueries)
                            _Logging.Debug(_Header + "query: " + query);

                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            if (transaction != null)
                                command.Transaction = transaction;

                            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        }
                    }

                    if (transaction != null)
                        await transaction.CommitAsync(token).ConfigureAwait(false);
                }
                catch
                {
                    if (transaction != null)
                        await transaction.RollbackAsync(token).ConfigureAwait(false);
                    throw;
                }
                finally
                {
                    if (transaction != null)
                        transaction.Dispose();
                }
            }
        }

        /// <summary>
        /// Sanitize a string value for SQL (escape single quotes).
        /// </summary>
        /// <param name="value">Input value.</param>
        /// <returns>Sanitized value.</returns>
        internal new string Sanitize(string? value)
        {
            return base.Sanitize(value);
        }

        /// <summary>
        /// Format a boolean for SQL.
        /// </summary>
        /// <param name="value">Boolean value.</param>
        /// <returns>SQL boolean string (1 or 0).</returns>
        internal new string FormatBoolean(bool value)
        {
            return base.FormatBoolean(value);
        }

        /// <summary>
        /// Format a DateTime for SQL storage.
        /// </summary>
        /// <param name="value">DateTime value.</param>
        /// <returns>ISO 8601 formatted string.</returns>
        internal new string FormatDateTime(DateTime value)
        {
            return base.FormatDateTime(value);
        }

        /// <summary>
        /// Format a nullable string for SQL.
        /// </summary>
        /// <param name="value">Nullable string.</param>
        /// <returns>SQL NULL or quoted/sanitized value.</returns>
        internal new string FormatNullableString(string? value)
        {
            return base.FormatNullableString(value);
        }
    }
}
