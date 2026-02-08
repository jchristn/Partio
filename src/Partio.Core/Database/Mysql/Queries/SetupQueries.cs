namespace Partio.Core.Database.Mysql.Queries
{
    /// <summary>
    /// Contains CREATE TABLE IF NOT EXISTS statements for initializing the MySQL database schema.
    /// </summary>
    public static class SetupQueries
    {
        /// <summary>
        /// Creates the tenants table for storing tenant information.
        /// </summary>
        public static readonly string CreateTenantsTable =
            @"CREATE TABLE IF NOT EXISTS tenants (
                id VARCHAR(48) PRIMARY KEY,
                name VARCHAR(256) NOT NULL,
                active TINYINT(1) NOT NULL DEFAULT 1,
                labels_json TEXT NULL,
                tags_json TEXT NULL,
                created_utc TEXT NOT NULL,
                last_update_utc TEXT NOT NULL
            );";

        /// <summary>
        /// Creates the users table for storing user accounts.
        /// References tenants(id) via tenant_id.
        /// </summary>
        public static readonly string CreateUsersTable =
            @"CREATE TABLE IF NOT EXISTS users (
                id VARCHAR(48) PRIMARY KEY,
                tenant_id VARCHAR(48) NOT NULL,
                email VARCHAR(256) NOT NULL,
                password_sha256 VARCHAR(64) NOT NULL,
                first_name VARCHAR(128) NULL,
                last_name VARCHAR(128) NULL,
                is_admin TINYINT(1) NOT NULL DEFAULT 0,
                active TINYINT(1) NOT NULL DEFAULT 1,
                labels_json TEXT NULL,
                tags_json TEXT NULL,
                created_utc TEXT NOT NULL,
                last_update_utc TEXT NOT NULL
            );";

        /// <summary>
        /// Creates the credentials table for storing API bearer tokens.
        /// References tenants(id) via tenant_id and users(id) via user_id.
        /// </summary>
        public static readonly string CreateCredentialsTable =
            @"CREATE TABLE IF NOT EXISTS credentials (
                id VARCHAR(48) PRIMARY KEY,
                tenant_id VARCHAR(48) NOT NULL,
                user_id VARCHAR(48) NOT NULL,
                name VARCHAR(256) NULL,
                bearer_token VARCHAR(64) NOT NULL UNIQUE,
                active TINYINT(1) NOT NULL DEFAULT 1,
                labels_json TEXT NULL,
                tags_json TEXT NULL,
                created_utc TEXT NOT NULL,
                last_update_utc TEXT NOT NULL
            );";

        /// <summary>
        /// Creates the embedding_endpoints table for storing embedding API configurations.
        /// References tenants(id) via tenant_id.
        /// </summary>
        public static readonly string CreateEmbeddingEndpointsTable =
            @"CREATE TABLE IF NOT EXISTS embedding_endpoints (
                id VARCHAR(48) PRIMARY KEY,
                tenant_id VARCHAR(48) NOT NULL,
                model VARCHAR(256) NOT NULL,
                endpoint VARCHAR(512) NOT NULL,
                api_format VARCHAR(32) NOT NULL,
                api_key VARCHAR(512) NULL,
                active TINYINT(1) NOT NULL DEFAULT 1,
                enable_request_history TINYINT(1) NOT NULL DEFAULT 0,
                health_check_enabled TINYINT(1) NOT NULL DEFAULT 0,
                health_check_url VARCHAR(512) NULL,
                health_check_method INT NOT NULL DEFAULT 0,
                health_check_interval_ms INT NOT NULL DEFAULT 5000,
                health_check_timeout_ms INT NOT NULL DEFAULT 2000,
                health_check_expected_status INT NOT NULL DEFAULT 200,
                healthy_threshold INT NOT NULL DEFAULT 2,
                unhealthy_threshold INT NOT NULL DEFAULT 2,
                health_check_use_auth TINYINT(1) NOT NULL DEFAULT 0,
                labels_json TEXT NULL,
                tags_json TEXT NULL,
                created_utc TEXT NOT NULL,
                last_update_utc TEXT NOT NULL
            );";

        /// <summary>
        /// Creates the request_history table for storing HTTP request audit logs.
        /// References tenants(id) via tenant_id, users(id) via user_id, and credentials(id) via credential_id.
        /// </summary>
        public static readonly string CreateRequestHistoryTable =
            @"CREATE TABLE IF NOT EXISTS request_history (
                id VARCHAR(48) PRIMARY KEY,
                tenant_id VARCHAR(48) NULL,
                user_id VARCHAR(48) NULL,
                credential_id VARCHAR(48) NULL,
                requestor_ip VARCHAR(64) NULL,
                http_method VARCHAR(16) NULL,
                http_url VARCHAR(512) NULL,
                request_body_length BIGINT NULL,
                response_body_length BIGINT NULL,
                http_status INTEGER NULL,
                response_time_ms BIGINT NULL,
                object_key VARCHAR(256) NULL,
                created_utc TEXT NOT NULL,
                completed_utc TEXT NULL
            );";

        /// <summary>
        /// All table creation queries in dependency order.
        /// </summary>
        public static readonly string[] AllTables = new string[]
        {
            CreateTenantsTable,
            CreateUsersTable,
            CreateCredentialsTable,
            CreateEmbeddingEndpointsTable,
            CreateRequestHistoryTable
        };
    }
}
