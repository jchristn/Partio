namespace Partio.Core.Database.Mysql
{
    using System.Data;
    using MySql.Data.MySqlClient;
    using Partio.Core.Database.Mysql.Implementations;
    using Partio.Core.Database.Mysql.Queries;
    using Partio.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// MySQL implementation of the database driver.
    /// </summary>
    public class MysqlDatabaseDriver : DatabaseDriverBase
    {
        private readonly string _ConnectionString;

        /// <summary>
        /// Connection string for internal use by implementation classes.
        /// </summary>
        internal string ConnectionString => _ConnectionString;

        /// <summary>
        /// Initialize a new instance of MysqlDatabaseDriver.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        public MysqlDatabaseDriver(ServerSettings settings, LoggingModule logging)
            : base(settings, logging)
        {
            _ConnectionString =
                "Server=" + settings.Database.Hostname + ";" +
                "Port=" + settings.Database.Port + ";" +
                "Database=" + settings.Database.DatabaseName + ";" +
                "Uid=" + settings.Database.Username + ";" +
                "Pwd=" + settings.Database.Password + ";";

            _Logging.Info(_Header + "initialized MySQL driver using " + settings.Database.Hostname + ":" + settings.Database.Port);
        }

        /// <summary>
        /// Initialize the database by creating all tables.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public override async Task InitializeAsync(CancellationToken token = default)
        {
            _Logging.Info(_Header + "initializing MySQL database");

            foreach (string query in SetupQueries.AllTables)
            {
                await ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            }

            Tenant = new TenantMethods(this, _Logging);
            User = new UserMethods(this, _Logging);
            Credential = new CredentialMethods(this, _Logging);
            EmbeddingEndpoint = new EmbeddingEndpointMethods(this, _Logging);
            RequestHistory = new RequestHistoryMethods(this, _Logging);

            _Logging.Info(_Header + "MySQL database initialized successfully");
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

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                MySqlTransaction? transaction = null;
                if (isTransaction)
                    transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false) as MySqlTransaction;

                try
                {
                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        if (transaction != null)
                            command.Transaction = transaction;

                        using (MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync(token).ConfigureAwait(false))
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

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                MySqlTransaction? transaction = null;
                if (isTransaction)
                    transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false) as MySqlTransaction;

                try
                {
                    foreach (string query in queries)
                    {
                        if (string.IsNullOrEmpty(query)) continue;

                        if (_Settings.Database.LogQueries)
                            _Logging.Debug(_Header + "query: " + query);

                        using (MySqlCommand command = new MySqlCommand(query, connection))
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
