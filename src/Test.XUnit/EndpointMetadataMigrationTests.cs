namespace Test.XUnit
{
    using Microsoft.Data.Sqlite;
    using Partio.Core.Database.Sqlite;
    using Partio.Core.Settings;
    using SyslogLogging;
    using Xunit;

    public class EndpointMetadataMigrationTests
    {
        [Fact]
        public async Task SqliteStartupAddsEndpointMetadataColumnsToExistingTables()
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

                Assert.Contains("labels_json", await GetColumnNamesAsync(dbPath, "embedding_endpoints"));
                Assert.Contains("tags_json", await GetColumnNamesAsync(dbPath, "embedding_endpoints"));
                Assert.Contains("labels_json", await GetColumnNamesAsync(dbPath, "completion_endpoints"));
                Assert.Contains("tags_json", await GetColumnNamesAsync(dbPath, "completion_endpoints"));
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                DeleteIfExists(dbPath);
                DeleteIfExists(dbPath + "-wal");
                DeleteIfExists(dbPath + "-shm");
            }
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
