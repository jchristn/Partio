namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Partio.Core.Database.Sqlite;
    using Partio.Core.Settings;
    using SyslogLogging;
    using Touchstone.Core;

    /// <summary>
    /// White-box tests for SQLite schema migration at driver startup, verifying that endpoint
    /// metadata columns are added to pre-existing tables. Backed by temporary SQLite files.
    /// </summary>
    public static class SqliteMigrationTests
    {
        /// <summary>
        /// Build the Touchstone suite of SQLite migration tests.
        /// </summary>
        /// <returns>A suite descriptor exposing every case.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                "SqliteMigration",
                "SQLite schema migration unit tests",
                BuildCases());
        }

        private static List<TestCaseDescriptor> BuildCases()
        {
            List<TestCaseDescriptor> tests = new List<TestCaseDescriptor>();

            tests.Add(TestCaseFactory.Async("SqliteMigration", "SQLite startup adds endpoint metadata columns to existing tables", async () =>
            {
                string dbPath = Path.Combine(Path.GetTempPath(), "partio-metadata-migration-" + Guid.NewGuid().ToString("N") + ".db");

                try
                {
                    await using (SqliteConnection connection = new SqliteConnection("Data Source=" + dbPath))
                    {
                        await connection.OpenAsync();
                        await using (SqliteCommand embedding = connection.CreateCommand())
                        {
                            embedding.CommandText = "CREATE TABLE embedding_endpoints (id VARCHAR(48) PRIMARY KEY);";
                            await embedding.ExecuteNonQueryAsync();
                        }
                        await using (SqliteCommand completion = connection.CreateCommand())
                        {
                            completion.CommandText = "CREATE TABLE completion_endpoints (id VARCHAR(48) PRIMARY KEY);";
                            await completion.ExecuteNonQueryAsync();
                        }
                    }

                    ServerSettings settings = new ServerSettings();
                    settings.Database.Filename = dbPath;

                    LoggingModule logging = new LoggingModule();
                    logging.Settings.EnableConsole = false;

                    SqliteDatabaseDriver driver = new SqliteDatabaseDriver(settings, logging);
                    await driver.InitializeAsync();

                    Check.Contains("labels_json", await GetColumnNamesAsync(dbPath, "embedding_endpoints"));
                    Check.Contains("tags_json", await GetColumnNamesAsync(dbPath, "embedding_endpoints"));
                    Check.Contains("labels_json", await GetColumnNamesAsync(dbPath, "completion_endpoints"));
                    Check.Contains("tags_json", await GetColumnNamesAsync(dbPath, "completion_endpoints"));
                }
                finally
                {
                    SqliteConnection.ClearAllPools();
                    DeleteIfExists(dbPath);
                    DeleteIfExists(dbPath + "-wal");
                    DeleteIfExists(dbPath + "-shm");
                }
            }));

            return tests;
        }

        private static async Task<HashSet<string>> GetColumnNamesAsync(string dbPath, string tableName)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using SqliteConnection connection = new SqliteConnection("Data Source=" + dbPath);
            await connection.OpenAsync();

            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM pragma_table_info($tableName);";
            command.Parameters.AddWithValue("$tableName", tableName);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }

            return names;
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
