namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using Partio.Core.Enums;

    /// <summary>
    /// Configuration for a self-hosted Partio test environment: which database backend to run against,
    /// its connection details, and the upstream embedding/inference endpoints to seed. Populated from
    /// CLI arguments (and environment variables) so the same test suites can run against SQLite in-process
    /// or a live external database and real provider endpoints.
    /// </summary>
    public sealed class TestEnvironmentOptions
    {
        /// <summary>Database backend to run the Partio server against. Default: Sqlite.</summary>
        public DatabaseTypeEnum DatabaseType { get; set; } = DatabaseTypeEnum.Sqlite;

        /// <summary>Database server hostname (non-SQLite backends).</summary>
        public string? DatabaseHostname { get; set; }

        /// <summary>Database server port (non-SQLite backends).</summary>
        public int DatabasePort { get; set; }

        /// <summary>Database name / catalog (non-SQLite backends).</summary>
        public string? DatabaseName { get; set; }

        /// <summary>Database username (non-SQLite backends).</summary>
        public string? DatabaseUsername { get; set; }

        /// <summary>Database password (non-SQLite backends).</summary>
        public string? DatabasePassword { get; set; }

        /// <summary>Database instance (SQL Server).</summary>
        public string? DatabaseInstance { get; set; }

        /// <summary>Database schema.</summary>
        public string? DatabaseSchema { get; set; }

        /// <summary>Upstream embedding endpoint URL to seed as the default embedding endpoint.</summary>
        public string? EmbeddingEndpoint { get; set; }

        /// <summary>Embedding model name to seed. Default: nomic-embed-text.</summary>
        public string EmbeddingModel { get; set; } = "nomic-embed-text";

        /// <summary>Upstream inference (completion) endpoint URL to seed as the default inference endpoint.</summary>
        public string? InferenceEndpoint { get; set; }

        /// <summary>Inference model name to seed. Default: gemma3:4b.</summary>
        public string InferenceModel { get; set; } = "gemma3:4b";

        /// <summary>API format of the seeded upstream endpoints. Default: Ollama.</summary>
        public string UpstreamApiFormat { get; set; } = "Ollama";

        /// <summary>Bearer token / API key sent to the seeded upstream endpoints.</summary>
        public string? UpstreamApiKey { get; set; }

        /// <summary>True when the caller supplied real upstream endpoints instead of the in-process stub.</summary>
        public bool UsesRealEndpoints =>
            !string.IsNullOrWhiteSpace(EmbeddingEndpoint) || !string.IsNullOrWhiteSpace(InferenceEndpoint);

        /// <summary>True when running against an external (non-SQLite) database.</summary>
        public bool UsesExternalDatabase => DatabaseType != DatabaseTypeEnum.Sqlite;

        /// <summary>
        /// Parse test-environment options from CLI arguments. Recognized flags (all optional):
        /// <c>--db</c>, <c>--db-host</c>, <c>--db-port</c>, <c>--db-user</c>, <c>--db-pass</c>,
        /// <c>--db-name</c>, <c>--db-instance</c>, <c>--db-schema</c>, <c>--embedding-endpoint</c>,
        /// <c>--embedding-model</c>, <c>--inference-endpoint</c>, <c>--inference-model</c>,
        /// <c>--upstream-format</c>, <c>--upstream-bearer</c>. Both <c>--flag value</c> and
        /// <c>--flag=value</c> forms are accepted.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>The parsed options.</returns>
        public static TestEnvironmentOptions Parse(string[] args)
        {
            TestEnvironmentOptions options = new TestEnvironmentOptions();
            if (args == null) return options;

            Dictionary<string, string> map = BuildMap(args);

            if (map.TryGetValue("--db", out string? db) && Enum.TryParse(db, true, out DatabaseTypeEnum parsed))
                options.DatabaseType = parsed;

            if (map.TryGetValue("--db-host", out string? host)) options.DatabaseHostname = host;
            if (map.TryGetValue("--db-port", out string? port) && int.TryParse(port, out int p)) options.DatabasePort = p;
            if (map.TryGetValue("--db-name", out string? name)) options.DatabaseName = name;
            if (map.TryGetValue("--db-user", out string? user)) options.DatabaseUsername = user;
            if (map.TryGetValue("--db-pass", out string? pass)) options.DatabasePassword = pass;
            if (map.TryGetValue("--db-instance", out string? instance)) options.DatabaseInstance = instance;
            if (map.TryGetValue("--db-schema", out string? schema)) options.DatabaseSchema = schema;

            if (map.TryGetValue("--embedding-endpoint", out string? embed)) options.EmbeddingEndpoint = embed;
            if (map.TryGetValue("--embedding-model", out string? embedModel)) options.EmbeddingModel = embedModel;
            if (map.TryGetValue("--inference-endpoint", out string? infer)) options.InferenceEndpoint = infer;
            if (map.TryGetValue("--inference-model", out string? inferModel)) options.InferenceModel = inferModel;
            if (map.TryGetValue("--upstream-format", out string? format)) options.UpstreamApiFormat = format;
            if (map.TryGetValue("--upstream-bearer", out string? bearer)) options.UpstreamApiKey = bearer;

            return options;
        }

        /// <summary>
        /// True when any test-environment flag is present in the supplied arguments, indicating the caller
        /// wants a configured (rather than default in-process SQLite) environment.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>True when at least one recognized flag is present.</returns>
        public static bool HasAnyOption(string[] args)
        {
            if (args == null) return false;
            foreach (string arg in args)
            {
                if (arg.StartsWith("--db", StringComparison.OrdinalIgnoreCase)
                    || arg.StartsWith("--embedding-", StringComparison.OrdinalIgnoreCase)
                    || arg.StartsWith("--inference-", StringComparison.OrdinalIgnoreCase)
                    || arg.StartsWith("--upstream-", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static Dictionary<string, string> BuildMap(string[] args)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (!arg.StartsWith("--", StringComparison.Ordinal)) continue;

                int eq = arg.IndexOf('=');
                if (eq >= 0)
                {
                    map[arg.Substring(0, eq)] = arg.Substring(eq + 1);
                }
                else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    map[arg] = args[i + 1];
                    i++;
                }
                else
                {
                    map[arg] = "true";
                }
            }
            return map;
        }
    }
}
