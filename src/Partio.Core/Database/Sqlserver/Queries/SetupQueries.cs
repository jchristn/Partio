namespace Partio.Core.Database.Sqlserver.Queries
{
    /// <summary>
    /// Contains CREATE TABLE statements for initializing the SQL Server database schema.
    /// </summary>
    public static class SetupQueries
    {
        /// <summary>
        /// Creates the tenants table for storing tenant information.
        /// </summary>
        public static readonly string CreateTenantsTable =
            @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'tenants')
            BEGIN
                CREATE TABLE tenants (
                    id NVARCHAR(48) PRIMARY KEY,
                    name NVARCHAR(256) NOT NULL,
                    active BIT NOT NULL DEFAULT 1,
                    labels_json NVARCHAR(MAX) NULL,
                    tags_json NVARCHAR(MAX) NULL,
                    created_utc NVARCHAR(64) NOT NULL,
                    last_update_utc NVARCHAR(64) NOT NULL
                );
            END;";

        /// <summary>
        /// Creates the users table for storing user accounts.
        /// References tenants(id) via tenant_id.
        /// </summary>
        public static readonly string CreateUsersTable =
            @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'users')
            BEGIN
                CREATE TABLE users (
                    id NVARCHAR(48) PRIMARY KEY,
                    tenant_id NVARCHAR(48) NOT NULL,       -- references tenants(id)
                    email NVARCHAR(256) NOT NULL,
                    password_sha256 NVARCHAR(64) NOT NULL,
                    first_name NVARCHAR(128) NULL,
                    last_name NVARCHAR(128) NULL,
                    is_admin BIT NOT NULL DEFAULT 0,
                    active BIT NOT NULL DEFAULT 1,
                    labels_json NVARCHAR(MAX) NULL,
                    tags_json NVARCHAR(MAX) NULL,
                    created_utc NVARCHAR(64) NOT NULL,
                    last_update_utc NVARCHAR(64) NOT NULL
                );
            END;";

        /// <summary>
        /// Creates the credentials table for storing API bearer tokens.
        /// References tenants(id) via tenant_id and users(id) via user_id.
        /// </summary>
        public static readonly string CreateCredentialsTable =
            @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'credentials')
            BEGIN
                CREATE TABLE credentials (
                    id NVARCHAR(48) PRIMARY KEY,
                    tenant_id NVARCHAR(48) NOT NULL,       -- references tenants(id)
                    user_id NVARCHAR(48) NOT NULL,         -- references users(id)
                    name NVARCHAR(256) NULL,
                    bearer_token NVARCHAR(64) NOT NULL UNIQUE,
                    active BIT NOT NULL DEFAULT 1,
                    labels_json NVARCHAR(MAX) NULL,
                    tags_json NVARCHAR(MAX) NULL,
                    created_utc NVARCHAR(64) NOT NULL,
                    last_update_utc NVARCHAR(64) NOT NULL
                );
            END;";

        /// <summary>
        /// Creates the embedding_endpoints table for storing embedding API configurations.
        /// References tenants(id) via tenant_id.
        /// </summary>
        public static readonly string CreateEmbeddingEndpointsTable =
            @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'embedding_endpoints')
            BEGIN
                CREATE TABLE embedding_endpoints (
                    id NVARCHAR(48) PRIMARY KEY,
                    tenant_id NVARCHAR(48) NOT NULL,       -- references tenants(id)
                    model NVARCHAR(256) NOT NULL,
                    endpoint NVARCHAR(512) NOT NULL,
                    api_format NVARCHAR(32) NOT NULL,
                    api_key NVARCHAR(512) NULL,
                    active BIT NOT NULL DEFAULT 1,
                    enable_request_history BIT NOT NULL DEFAULT 0,
                    labels_json NVARCHAR(MAX) NULL,
                    tags_json NVARCHAR(MAX) NULL,
                    created_utc NVARCHAR(64) NOT NULL,
                    last_update_utc NVARCHAR(64) NOT NULL
                );
            END;";

        /// <summary>
        /// Creates the request_history table for storing HTTP request audit logs.
        /// References tenants(id) via tenant_id, users(id) via user_id, and credentials(id) via credential_id.
        /// </summary>
        public static readonly string CreateRequestHistoryTable =
            @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'request_history')
            BEGIN
                CREATE TABLE request_history (
                    id NVARCHAR(48) PRIMARY KEY,
                    tenant_id NVARCHAR(48) NULL,           -- references tenants(id)
                    user_id NVARCHAR(48) NULL,             -- references users(id)
                    credential_id NVARCHAR(48) NULL,       -- references credentials(id)
                    requestor_ip NVARCHAR(64) NULL,
                    http_method NVARCHAR(16) NULL,
                    http_url NVARCHAR(512) NULL,
                    request_body_length BIGINT NULL,
                    response_body_length BIGINT NULL,
                    http_status INT NULL,
                    response_time_ms BIGINT NULL,
                    object_key NVARCHAR(256) NULL,
                    created_utc NVARCHAR(64) NOT NULL,
                    completed_utc NVARCHAR(64) NULL
                );
            END;";

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
