namespace Partio.Core.Database.Sqlite
{
    using System.Data;
    using Microsoft.Data.Sqlite;
    using Partio.Core.Database.Sqlite.Implementations;
    using Partio.Core.Database.Sqlite.Queries;
    using Partio.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// SQLite implementation of the database driver.
    /// </summary>
    public class SqliteDatabaseDriver : DatabaseDriverBase
    {
        private readonly string _ConnectionString;
        private readonly SemaphoreSlim _Semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Connection string for internal use by implementation classes.
        /// </summary>
        internal string ConnectionString => _ConnectionString;

        /// <summary>
        /// Semaphore for concurrency control, accessible by implementation classes.
        /// </summary>
        internal SemaphoreSlim Semaphore => _Semaphore;

        /// <summary>
        /// Initialize a new instance of SqliteDatabaseDriver.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        public SqliteDatabaseDriver(ServerSettings settings, LoggingModule logging)
            : base(settings, logging)
        {
            _ConnectionString = "Data Source=" + settings.Database.Filename;
            _Logging.Info(_Header + "initialized SQLite driver using file " + settings.Database.Filename);
        }

        /// <summary>
        /// Initialize the database by enabling WAL mode and creating all tables.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public override async Task InitializeAsync(CancellationToken token = default)
        {
            _Logging.Info(_Header + "initializing SQLite database");

            await ExecuteQueryAsync("PRAGMA journal_mode=WAL;", false, token).ConfigureAwait(false);

            foreach (string query in SetupQueries.AllTables)
            {
                await ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            }

            Tenant = new TenantMethods(this, _Logging);
            User = new UserMethods(this, _Logging);
            Credential = new CredentialMethods(this, _Logging);
            EmbeddingEndpoint = new EmbeddingEndpointMethods(this, _Logging);
            RequestHistory = new RequestHistoryMethods(this, _Logging);

            _Logging.Info(_Header + "SQLite database initialized successfully");
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

            await _Semaphore.WaitAsync(token).ConfigureAwait(false);

            try
            {
                using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
                {
                    await connection.OpenAsync(token).ConfigureAwait(false);

                    SqliteTransaction? transaction = null;
                    if (isTransaction)
                        transaction = connection.BeginTransaction();

                    try
                    {
                        using (SqliteCommand command = new SqliteCommand(query, connection))
                        {
                            if (transaction != null)
                                command.Transaction = transaction;

                            using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
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
            }
            finally
            {
                _Semaphore.Release();
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

            await _Semaphore.WaitAsync(token).ConfigureAwait(false);

            try
            {
                using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
                {
                    await connection.OpenAsync(token).ConfigureAwait(false);

                    SqliteTransaction? transaction = null;
                    if (isTransaction)
                        transaction = connection.BeginTransaction();

                    try
                    {
                        foreach (string query in queries)
                        {
                            if (string.IsNullOrEmpty(query)) continue;

                            if (_Settings.Database.LogQueries)
                                _Logging.Debug(_Header + "query: " + query);

                            using (SqliteCommand command = new SqliteCommand(query, connection))
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
            finally
            {
                _Semaphore.Release();
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
