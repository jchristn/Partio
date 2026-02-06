# Partio — Implementation Plan

> **Partio** is a multi-tenant RESTful (and future MQ) platform that accepts semantic cells (text, lists, tables, images, etc.) with a chunking and embedding configuration, and returns chunked text with computed embeddings.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Solution & Project Structure](#2-solution--project-structure)
3. [Phase 1 — Partio.Core Foundation](#3-phase-1--partiocore-foundation)
4. [Phase 2 — Database Layer](#4-phase-2--database-layer)
5. [Phase 3 — Chunking Engine](#5-phase-3--chunking-engine)
6. [Phase 4 — Embedding Clients](#6-phase-4--embedding-clients)
7. [Phase 5 — Partio.Server](#7-phase-5--partioserver)
8. [Phase 6 — Dashboard](#8-phase-6--dashboard)
9. [Phase 7 — SDKs](#9-phase-7--sdks)
10. [Phase 8 — Test.Automated](#10-phase-8--testautomated)
11. [Phase 9 — Docker & Build](#11-phase-9--docker--build)
12. [Phase 10 — Documentation](#12-phase-10--documentation)
13. [Appendix A — Data Models](#appendix-a--data-models)
14. [Appendix B — API Routes](#appendix-b--api-routes)
15. [Appendix C — Settings File (partio.json)](#appendix-c--settings-file-partiojson)
16. [Appendix D — Database Schema](#appendix-d--database-schema)

---

## 1. Architecture Overview

```
┌─────────────┐     ┌─────────────────────────────────────────────┐
│  Dashboard   │     │                Partio.Server                │
│  (React/     │────▶│  SwiftStack REST ─▶ Auth ─▶ Route Handlers │
│   Vite)      │     │       │                                     │
└─────────────┘     │       ├── ChunkingEngine                    │
                     │       ├── EmbeddingClients (Ollama/OpenAI)  │
┌─────────────┐     │       ├── RequestHistoryService              │
│  SDKs        │────▶│       └── RequestHistoryCleanupService      │
│  C#/Py/JS    │     │                                             │
└─────────────┘     │  Partio.Core                                │
                     │       ├── Models (Tenant, User, Credential, │
                     │       │          EmbeddingEndpoint, etc.)    │
                     │       ├── Database (SQLite/PG/MySQL/MSSQL)  │
                     │       ├── Settings                          │
                     │       ├── Chunking                          │
                     │       └── ThirdParty (Ollama, OpenAI)       │
                     └─────────────────────────────────────────────┘
```

**Key decisions:**
- **SwiftStack** NuGet package (v0.4.5+) for REST server framework
- **SharpToken** NuGet package for BPE token counting (tiktoken-compatible)
- **SyslogLogging** (LoggingModule) for structured logging
- **PrettyId** for k-sortable unique IDs with semantic prefixes
- **SerializationHelper** for JSON serialization (PascalCase, no JsonPropertyName/JsonNamingPolicy)
- Four database backends: SQLite, PostgreSQL, MySQL, SQL Server
- Multi-tenant with bearer token authentication
- Embedding model endpoints: system-wide defaults in `partio.json`, seeded into DB per-tenant on creation
- Tenant creation auto-creates a default user, credential, and set of embedding endpoints

---

## 2. Solution & Project Structure

```
C:\Code\Partio\
├── src\
│   ├── Partio.sln
│   ├── Partio.Core\
│   │   ├── Partio.Core.csproj                 (net8.0;net10.0, class library)
│   │   ├── Constants.cs
│   │   ├── IdGenerator.cs
│   │   ├── Settings\
│   │   │   ├── ServerSettings.cs              (root settings)
│   │   │   ├── RestSettings.cs
│   │   │   ├── LoggingSettings.cs
│   │   │   ├── SyslogServer.cs
│   │   │   ├── DatabaseSettings.cs
│   │   │   ├── DebugSettings.cs
│   │   │   ├── RequestHistorySettings.cs
│   │   │   └── DefaultEmbeddingEndpoint.cs
│   │   ├── Enums\
│   │   │   ├── DatabaseTypeEnum.cs
│   │   │   ├── ApiFormatEnum.cs               (Ollama, OpenAI)
│   │   │   ├── ChunkStrategyEnum.cs
│   │   │   ├── OverlapStrategyEnum.cs
│   │   │   ├── AtomTypeEnum.cs                (mirrors DocumentAtom)
│   │   │   └── EnumerationOrderEnum.cs
│   │   ├── Models\
│   │   │   ├── TenantMetadata.cs
│   │   │   ├── UserMaster.cs
│   │   │   ├── Credential.cs
│   │   │   ├── EmbeddingEndpoint.cs
│   │   │   ├── RequestHistoryEntry.cs
│   │   │   ├── EnumerationRequest.cs
│   │   │   ├── EnumerationResult.cs
│   │   │   ├── ChunkingConfiguration.cs
│   │   │   ├── EmbeddingConfiguration.cs
│   │   │   ├── SemanticCellRequest.cs
│   │   │   ├── SemanticCellResponse.cs
│   │   │   ├── ChunkResult.cs
│   │   │   └── ApiErrorResponse.cs
│   │   ├── Chunking\
│   │   │   ├── ChunkingEngine.cs
│   │   │   ├── FixedTokenChunker.cs
│   │   │   ├── SentenceChunker.cs
│   │   │   ├── ParagraphChunker.cs
│   │   │   ├── WholeListChunker.cs
│   │   │   └── ListEntryChunker.cs
│   │   ├── ThirdParty\
│   │   │   ├── EmbeddingClientBase.cs
│   │   │   ├── OllamaEmbeddingClient.cs
│   │   │   └── OpenAiEmbeddingClient.cs
│   │   ├── Database\
│   │   │   ├── DatabaseDriverBase.cs          (abstract base)
│   │   │   ├── Interfaces\
│   │   │   │   ├── ITenantMethods.cs
│   │   │   │   ├── IUserMethods.cs
│   │   │   │   ├── ICredentialMethods.cs
│   │   │   │   ├── IEmbeddingEndpointMethods.cs
│   │   │   │   └── IRequestHistoryMethods.cs
│   │   │   ├── Sqlite\
│   │   │   │   ├── SqliteDatabaseDriver.cs
│   │   │   │   ├── Implementations\           (TenantMethods.cs, etc.)
│   │   │   │   └── Queries\                   (SetupQueries.cs, etc.)
│   │   │   ├── Postgresql\
│   │   │   │   ├── PostgresqlDatabaseDriver.cs
│   │   │   │   ├── Implementations\
│   │   │   │   └── Queries\
│   │   │   ├── Mysql\
│   │   │   │   ├── MysqlDatabaseDriver.cs
│   │   │   │   ├── Implementations\
│   │   │   │   └── Queries\
│   │   │   └── Sqlserver\
│   │   │       ├── SqlServerDatabaseDriver.cs
│   │   │       ├── Implementations\
│   │   │       └── Queries\
│   │   └── Serialization\
│   │       └── PartioSerializer.cs
│   ├── Partio.Server\
│   │   ├── Partio.Server.csproj               (net10.0, exe)
│   │   ├── PartioServer.cs                    (main entry point)
│   │   ├── Dockerfile
│   │   ├── Services\
│   │   │   ├── AuthenticationService.cs
│   │   │   ├── RequestHistoryService.cs
│   │   │   └── RequestHistoryCleanupService.cs
│   │   └── www\                               (optional static assets)
│   └── Test.Automated\
│       ├── Test.Automated.csproj              (net10.0, exe)
│       └── Program.cs
├── dashboard\
│   ├── src\
│   │   ├── App.jsx
│   │   ├── main.jsx
│   │   ├── index.css
│   │   ├── components\
│   │   │   ├── Login.jsx / Login.css
│   │   │   ├── Sidebar.jsx / Sidebar.css
│   │   │   ├── Topbar.jsx / Topbar.css
│   │   │   ├── Workspace.jsx / Workspace.css
│   │   │   ├── TenantsView.jsx / TenantsView.css
│   │   │   ├── UsersView.jsx / UsersView.css
│   │   │   ├── CredentialsView.jsx / CredentialsView.css
│   │   │   ├── EmbeddingEndpointsView.jsx / EmbeddingEndpointsView.css
│   │   │   ├── RequestHistoryView.jsx / RequestHistoryView.css
│   │   │   ├── ChunkEmbedView.jsx / ChunkEmbedView.css  (semantic cell test UI)
│   │   │   ├── Modal.jsx / Modal.css
│   │   │   ├── ActionMenu.jsx / ActionMenu.css
│   │   │   ├── CopyableId.jsx / CopyableId.css
│   │   │   ├── Pagination.jsx / Pagination.css
│   │   │   ├── KeyValueEditor.jsx / KeyValueEditor.css
│   │   │   ├── TagInput.jsx / TagInput.css
│   │   │   └── JsonEditor.jsx / JsonEditor.css
│   │   ├── context\
│   │   │   └── AppContext.jsx
│   │   └── utils\
│   │       └── api.js
│   ├── public\
│   ├── package.json
│   ├── vite.config.js
│   ├── Dockerfile
│   ├── nginx.conf
│   └── index.html
├── sdk\
│   ├── csharp\
│   │   ├── Partio.Sdk.sln
│   │   ├── Partio.Sdk\
│   │   │   ├── Partio.Sdk.csproj
│   │   │   ├── PartioClient.cs
│   │   │   └── Models\                        (DTOs)
│   │   └── Partio.Sdk.TestHarness\
│   │       ├── Partio.Sdk.TestHarness.csproj
│   │       └── Program.cs
│   ├── python\
│   │   ├── partio_sdk.py
│   │   ├── test_harness.py
│   │   └── requirements.txt
│   └── js\
│       ├── partio-sdk.js
│       ├── test-harness.js
│       └── package.json
├── docker\
│   ├── compose.yaml
│   └── partio.json                            (example config for Docker)
├── build-server.bat
├── build-dashboard.bat
├── .gitignore
├── README.md
├── REST_API.md
├── LICENSE.md
├── CHANGELOG.md
└── PARTIO.md                                  (this file)
```

### Checklist — Structure

- [x] Create solution file `src/Partio.sln`
- [x] Create `src/Partio.Core/Partio.Core.csproj` (net8.0;net10.0, class library)
- [x] Create `src/Partio.Server/Partio.Server.csproj` (net10.0, exe)
- [x] Create `src/Test.Automated/Test.Automated.csproj` (net10.0, exe)
- [x] Add all three projects to the solution
- [x] Create `sdk/csharp/Partio.Sdk.sln` and add SDK + test harness projects to main solution
- [x] Create directory stubs for `dashboard/`, `sdk/python/`, `sdk/js/`, `docker/`

---

## 3. Phase 1 — Partio.Core Foundation

### 3.1 Constants

- [x] `Constants.cs` — ASCII art logo, version string, settings filename (`partio.json`), log directory, log filename, content types, headers, ID prefixes

### 3.2 ID Generator

- [x] `IdGenerator.cs` — Uses PrettyId for k-sortable IDs
  - Prefixes: `ten_` (tenant), `usr_` (user), `cred_` (credential), `ep_` (embedding endpoint), `req_` (request history)
  - `NewTenantId()`, `NewUserId()`, `NewCredentialId()`, `NewEmbeddingEndpointId()`, `NewRequestHistoryId()`
  - `NewBearerToken()` — 64-character random alphanumeric token

### 3.3 Enums

- [x] `DatabaseTypeEnum` — Sqlite, Postgresql, Mysql, SqlServer
- [x] `ApiFormatEnum` — Ollama, OpenAI
- [x] `ChunkStrategyEnum` — FixedTokenCount, SentenceBased, ParagraphBased, WholeList, ListEntry
- [x] `OverlapStrategyEnum` — SlidingWindow, SentenceBoundaryAware, SemanticBoundaryAware
- [x] `AtomTypeEnum` — Text, List, Binary, Table, Unknown, Image, Hyperlink, Code, Meta (mirrors DocumentAtom)
- [x] `EnumerationOrderEnum` — CreatedAscending, CreatedDescending, NameAscending, NameDescending

### 3.4 Settings Classes

All settings classes use private backing fields named `_LikeThis`, property-level validation via throw expressions, sensible defaults, and XML documentation.

- [x] `ServerSettings.cs` — Root settings container
  ```csharp
  public class ServerSettings
  {
      public RestSettings Rest { get; set; }
      public DatabaseSettings Database { get; set; }
      public LoggingSettings Logging { get; set; }
      public DebugSettings Debug { get; set; }
      public RequestHistorySettings RequestHistory { get; set; }
      public List<string> AdminApiKeys { get; set; }           // Default: ["partioadmin"]
      public List<DefaultEmbeddingEndpoint> DefaultEmbeddingEndpoints { get; set; }
  }
  ```
- [x] `RestSettings.cs` — Hostname (default "localhost"), Port (default 8400, range 0–65535), Ssl (default false)
- [x] `LoggingSettings.cs` — SyslogServers, ConsoleLogging (true), EnableColors (false), LogDirectory ("./logs/"), LogFilename ("partio.log"), FileLogging (true), IncludeDateInFilename (true), MinimumSeverity (0, range 0–7)
- [x] `SyslogServer.cs` — Hostname, Port
- [x] `DatabaseSettings.cs` — Type (Sqlite), Filename ("./partio.db"), Hostname, Port, DatabaseName, Username, Password, Instance, Schema, RequireEncryption, LogQueries
- [x] `DebugSettings.cs` — Authentication (false), Exceptions (true), Requests (false), DatabaseQueries (false)
- [x] `RequestHistorySettings.cs` — Enabled (true), Directory ("./request-history/"), RetentionDays (7, range 1–365), CleanupIntervalMinutes (60, range 1–1440), MaxRequestBodyBytes (65536, range 1024–1048576), MaxResponseBodyBytes (65536, range 1024–1048576)
- [x] `DefaultEmbeddingEndpoint.cs` — Model (string), Endpoint (string), ApiFormat (ApiFormatEnum), ApiKey (string, nullable)

### 3.5 Models

All models use private backing fields `_LikeThis`, PascalCase public properties with XML docs, value-checking setters, and static `FromDataRow()`/`FromDataTable()` factory methods.

- [x] **TenantMetadata.cs**
  - Id (k-sortable, prefix `ten_`, 48 chars)
  - Name (required, non-empty)
  - Active (default true)
  - Labels (List\<string\>)
  - Tags (Dictionary\<string, string\>)
  - CreatedUtc, LastUpdateUtc

- [x] **UserMaster.cs**
  - Id (k-sortable, prefix `usr_`, 48 chars)
  - TenantId (required)
  - Email (required)
  - PasswordSha256 (64-char hex)
  - FirstName, LastName
  - IsAdmin (default false)
  - Active (default true)
  - Labels, Tags
  - CreatedUtc, LastUpdateUtc
  - `SetPassword(string plainText)`, `VerifyPassword(string plainText)`, static `ComputePasswordHash(string)`
  - static `Redact(UserMaster)` — masks password in responses

- [x] **Credential.cs**
  - Id (k-sortable, prefix `cred_`, 48 chars)
  - TenantId, UserId (required)
  - Name
  - BearerToken (64-char, generated via IdGenerator.NewBearerToken())
  - Active (default true)
  - Labels, Tags
  - CreatedUtc, LastUpdateUtc

- [x] **EmbeddingEndpoint.cs**
  - Id (k-sortable, prefix `ep_`, 48 chars)
  - TenantId (required)
  - Model (required, e.g. "all-minilm", "text-embedding-3-small")
  - Endpoint (required, URL)
  - ApiFormat (ApiFormatEnum)
  - ApiKey (nullable)
  - Active (default true)
  - Labels, Tags
  - CreatedUtc, LastUpdateUtc

- [x] **RequestHistoryEntry.cs**
  - Id (k-sortable, prefix `req_`, 48 chars)
  - TenantId, UserId, CredentialId
  - RequestorIp, HttpMethod, HttpUrl
  - RequestBodyLength, ResponseBodyLength
  - HttpStatus, ResponseTimeMs
  - ObjectKey (filesystem BLOB key)
  - CreatedUtc, CompletedUtc

- [x] **EnumerationRequest.cs**
  - MaxResults (default 100, range 1–1000)
  - ContinuationToken (nullable)
  - Order (EnumerationOrderEnum, default CreatedDescending)
  - NameFilter (nullable, partial match)
  - LabelFilter (nullable, exact match)
  - TagKeyFilter (nullable), TagValueFilter (nullable)
  - ActiveFilter (nullable bool)

- [x] **EnumerationResult\<T\>.cs**
  - Data (List\<T\>)
  - ContinuationToken (nullable)
  - TotalCount (nullable long)
  - HasMore (bool)

- [x] **ChunkingConfiguration.cs**
  - Strategy (ChunkStrategyEnum, default FixedTokenCount)
  - FixedTokenCount (default 256, min 1)
  - OverlapCount (default 0, min 0) — token/character count to overlap
  - OverlapPercentage (nullable double, 0.0–1.0) — alternative to OverlapCount
  - OverlapStrategy (OverlapStrategyEnum, default SlidingWindow)
  - ContextPrefix (nullable string) — prepended to each chunk before embedding

- [x] **EmbeddingConfiguration.cs**
  - Model (required string, e.g. "all-minilm")
  - L2Normalization (default false)

- [x] **SemanticCellRequest.cs** — The inbound request body
  - Type (AtomTypeEnum)
  - Text (nullable string)
  - UnorderedList (nullable List\<string\>)
  - OrderedList (nullable List\<string\>)
  - Table (nullable, serializable DataTable or List\<List\<string\>\>)
  - Binary (nullable byte[])
  - ChunkingConfiguration (required)
  - EmbeddingConfiguration (required)
  - Labels (nullable List\<string\>)
  - Tags (nullable Dictionary\<string, string\>)

- [x] **SemanticCellResponse.cs** — The outbound response body
  - Cells (int — number of input cells processed)
  - TotalChunks (int)
  - Chunks (List\<ChunkResult\>)
  - Labels (List\<string\> — echoed from request)
  - Tags (Dictionary\<string, string\> — echoed from request)

- [x] **ChunkResult.cs**
  - Text (string — original text of this chunk)
  - ChunkedText (string — context prefix + chunk text, the string that was actually embedded)
  - Embeddings (List\<float\>)

- [x] **ApiErrorResponse.cs**
  - Error (string)
  - Message (string)
  - StatusCode (int)
  - TimestampUtc (DateTime)

### 3.6 Serialization

- [x] `PartioSerializer.cs` — Implements SwiftStack `ISerializer`
  - Uses `SerializationHelper` (Serializer class)
  - PascalCase output (no naming policy)
  - WriteIndented = true for pretty output
  - No JsonPropertyName attributes anywhere in the codebase

---

## 4. Phase 2 — Database Layer

### 4.1 Abstract Base

- [x] `DatabaseDriverBase.cs`
  - Properties: `Tenant` (ITenantMethods), `User` (IUserMethods), `Credential` (ICredentialMethods), `EmbeddingEndpoint` (IEmbeddingEndpointMethods), `RequestHistory` (IRequestHistoryMethods)
  - Abstract methods: `InitializeAsync()`, `ExecuteQueryAsync(string query, bool isTransaction)`, `ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction)`
  - Helper methods: `Sanitize(string)` (replace `'` with `''`), `FormatBoolean()`, `FormatDateTime()`, `FormatNullable()`, `FormatNullableString()`
  - Protected: `LoggingModule`, `ServerSettings`

### 4.2 Database Interfaces

- [x] **ITenantMethods**
  - `CreateAsync(TenantMetadata)`, `ReadByIdAsync(string)`, `UpdateAsync(TenantMetadata)`, `DeleteByIdAsync(string)`, `ExistsByIdAsync(string)`, `EnumerateAsync(EnumerationRequest)`, `CountAsync()`

- [x] **IUserMethods**
  - `CreateAsync(UserMaster)`, `ReadByIdAsync(string)`, `ReadByEmailAsync(string tenantId, string email)`, `UpdateAsync(UserMaster)`, `DeleteByIdAsync(string)`, `ExistsByIdAsync(string)`, `EnumerateAsync(string tenantId, EnumerationRequest)`, `CountAsync(string tenantId)`

- [x] **ICredentialMethods**
  - `CreateAsync(Credential)`, `ReadByIdAsync(string)`, `ReadByBearerTokenAsync(string)`, `UpdateAsync(Credential)`, `DeleteByIdAsync(string)`, `ExistsByIdAsync(string)`, `EnumerateAsync(string tenantId, EnumerationRequest)`, `CountAsync(string tenantId)`

- [x] **IEmbeddingEndpointMethods**
  - `CreateAsync(EmbeddingEndpoint)`, `ReadByIdAsync(string)`, `ReadByModelAsync(string tenantId, string model)`, `UpdateAsync(EmbeddingEndpoint)`, `DeleteByIdAsync(string)`, `ExistsByIdAsync(string)`, `EnumerateAsync(string tenantId, EnumerationRequest)`, `CountAsync(string tenantId)`

- [x] **IRequestHistoryMethods**
  - `CreateAsync(RequestHistoryEntry)`, `UpdateAsync(RequestHistoryEntry)`, `ReadByIdAsync(string)`, `EnumerateAsync(string tenantId, EnumerationRequest)`, `DeleteByIdAsync(string)`, `DeleteExpiredAsync(DateTime cutoff)`, `GetExpiredObjectKeysAsync(DateTime cutoff)`, `CountAsync(string tenantId)`

### 4.3 SQLite Implementation

- [x] `SqliteDatabaseDriver.cs` — Uses Microsoft.Data.Sqlite
  - SemaphoreSlim(1,1) for concurrency
  - WAL mode enabled
  - Creates all tables on `InitializeAsync()`
- [x] `Sqlite/Queries/SetupQueries.cs` — CREATE TABLE IF NOT EXISTS for all 5 tables
- [x] `Sqlite/Implementations/TenantMethods.cs`
- [x] `Sqlite/Implementations/UserMethods.cs`
- [x] `Sqlite/Implementations/CredentialMethods.cs`
- [x] `Sqlite/Implementations/EmbeddingEndpointMethods.cs`
- [x] `Sqlite/Implementations/RequestHistoryMethods.cs`

### 4.4 PostgreSQL Implementation

- [x] `PostgresqlDatabaseDriver.cs` — Uses Npgsql
- [x] `Postgresql/Queries/SetupQueries.cs`
- [x] `Postgresql/Implementations/TenantMethods.cs`
- [x] `Postgresql/Implementations/UserMethods.cs`
- [x] `Postgresql/Implementations/CredentialMethods.cs`
- [x] `Postgresql/Implementations/EmbeddingEndpointMethods.cs`
- [x] `Postgresql/Implementations/RequestHistoryMethods.cs`

### 4.5 MySQL Implementation

- [x] `MysqlDatabaseDriver.cs` — Uses MySql.Data
- [x] `Mysql/Queries/SetupQueries.cs`
- [x] `Mysql/Implementations/TenantMethods.cs`
- [x] `Mysql/Implementations/UserMethods.cs`
- [x] `Mysql/Implementations/CredentialMethods.cs`
- [x] `Mysql/Implementations/EmbeddingEndpointMethods.cs`
- [x] `Mysql/Implementations/RequestHistoryMethods.cs`

### 4.6 SQL Server Implementation

- [x] `SqlServerDatabaseDriver.cs` — Uses Microsoft.Data.SqlClient
- [x] `Sqlserver/Queries/SetupQueries.cs`
- [x] `Sqlserver/Implementations/TenantMethods.cs`
- [x] `Sqlserver/Implementations/UserMethods.cs`
- [x] `Sqlserver/Implementations/CredentialMethods.cs`
- [x] `Sqlserver/Implementations/EmbeddingEndpointMethods.cs`
- [x] `Sqlserver/Implementations/RequestHistoryMethods.cs`

### 4.7 Database Tables

See [Appendix D](#appendix-d--database-schema) for full DDL.

**Tables:**
1. `tenants` — id, name, active, labels_json, tags_json, created_utc, last_update_utc
2. `users` — id, tenant_id, email, password_sha256, first_name, last_name, is_admin, active, labels_json, tags_json, created_utc, last_update_utc
3. `credentials` — id, tenant_id, user_id, name, bearer_token, active, labels_json, tags_json, created_utc, last_update_utc
4. `embedding_endpoints` — id, tenant_id, model, endpoint, api_format, api_key, active, labels_json, tags_json, created_utc, last_update_utc
5. `request_history` — id, tenant_id, user_id, credential_id, requestor_ip, http_method, http_url, request_body_length, response_body_length, http_status, response_time_ms, object_key, created_utc, completed_utc

---

## 5. Phase 3 — Chunking Engine

### 5.1 SharpToken Integration

- [x] Add SharpToken NuGet package to Partio.Core
- [x] Create token counting helper that uses SharpToken (cl100k_base encoding by default)

### 5.2 Chunking Strategies

Each chunker implements a common pattern: accepts text + ChunkingConfiguration, returns List\<string\> of chunk texts.

- [x] **ChunkingEngine.cs** — Factory/dispatcher
  - `ChunkAsync(SemanticCellRequest request)` → `List<ChunkResult>` (before embeddings)
  - Dispatches to the correct chunker based on `ChunkStrategyEnum`
  - Handles context prefix prepending (for the `ChunkedText` field)
  - Handles different atom types: Text → direct chunk; List → WholeList or ListEntry; Table → serialize rows; Binary/Image → pass-through or error

- [x] **FixedTokenChunker.cs**
  - Splits text into chunks of exactly N tokens (using SharpToken)
  - Overlap: slides back by OverlapCount tokens (or OverlapPercentage × N)
  - OverlapStrategy:
    - SlidingWindow: mechanical overlap by token count
    - SentenceBoundaryAware: adjust overlap boundaries to nearest sentence boundary
    - SemanticBoundaryAware: adjust overlap boundaries to nearest paragraph/heading boundary

- [x] **SentenceChunker.cs**
  - Splits text at sentence boundaries (`.` `!` `?` followed by whitespace or end)
  - Groups sentences until the token limit is reached
  - Overlap: re-includes trailing sentences from the previous chunk

- [x] **ParagraphChunker.cs**
  - Splits text at paragraph boundaries (double newline `\n\n`)
  - Each paragraph is a chunk (or groups paragraphs to fill token budget)
  - Overlap: re-includes trailing paragraph(s) from previous chunk

- [x] **WholeListChunker.cs**
  - Takes an ordered or unordered list and treats the entire list as a single chunk
  - Serializes list items with bullets/numbers into a single text block

- [x] **ListEntryChunker.cs**
  - Each list item becomes its own chunk
  - Context prefix is prepended to each entry

### 5.3 Table Handling

- [x] Tables: serialize each row as a text line (or configurable format), then apply the selected text chunking strategy to the serialized text

---

## 6. Phase 4 — Embedding Clients

### 6.1 Base Client

- [x] `EmbeddingClientBase.cs`
  - Abstract: `EmbedAsync(string text, string model)` → `List<float>`
  - Abstract: `EmbedBatchAsync(List<string> texts, string model)` → `List<List<float>>`
  - Concrete: `NormalizeL2(List<float> embeddings)` → `List<float>`
  - Protected: `LoggingModule`, endpoint URL, API key

### 6.2 Ollama Client

- [x] `OllamaEmbeddingClient.cs`
  - POST to `{endpoint}/api/embed` (Ollama embedding API)
  - Request body: `{ "model": "...", "input": "..." }` or `{ "model": "...", "input": ["...", "..."] }`
  - Response: `{ "model": "...", "embeddings": [[...], [...]] }`
  - Uses HttpClient with no auth header (or optional API key if configured)

### 6.3 OpenAI Client

- [x] `OpenAiEmbeddingClient.cs`
  - POST to `{endpoint}/v1/embeddings`
  - Request body: `{ "model": "...", "input": "..." }` or `{ "model": "...", "input": ["...", "..."] }`
  - Response: `{ "data": [{ "embedding": [...] }] }`
  - Uses HttpClient with `Authorization: Bearer {apiKey}` header

---

## 7. Phase 5 — Partio.Server

### 7.1 Main Entry Point

- [x] `PartioServer.cs`
  - **Startup flow** (following Conductor/Chronos pattern):
    1. Load settings from `partio.json` (create with defaults if missing)
    2. Initialize LoggingModule (SyslogLogging)
    3. Create and initialize database driver (based on settings)
    4. Call `InitializeFirstRunAsync()` — create default records if no tenants exist
    5. Initialize AuthenticationService
    6. Initialize RequestHistoryService (if enabled)
    7. Start RequestHistoryCleanupService (background task, if enabled)
    8. Initialize SwiftStackApp with REST routes
    9. Run server (await cancellation token / Console.ReadLine)
    10. Graceful shutdown of all services

- [x] **InitializeFirstRunAsync()**
  - Check if any tenants exist; if yes, skip
  - Create tenant: id="default", name="Default Tenant"
  - Create user: id="default", tenantId="default", email="admin@partio", password="password", isAdmin=true
  - Create credential: id="default", tenantId="default", userId="default", name="Default API Key", bearerToken="default"
  - Create embedding endpoints from `ServerSettings.DefaultEmbeddingEndpoints` for the default tenant
  - Log credentials to console (with warning they won't be shown again)

### 7.2 Authentication Service

- [x] `AuthenticationService.cs`
  - `AuthenticateBearerAsync(string token)` → AuthContext
    - First check if token is in `AdminApiKeys` → return admin context
    - Else look up Credential by bearer token
    - Validate credential active, user active, tenant active
    - Return AuthContext with TenantId, UserId, CredentialId, IsAdmin
  - `AuthContext` class: IsAuthenticated, IsGlobalAdmin, TenantId, UserId, CredentialId, Token

### 7.3 Request History Service

- [x] `RequestHistoryService.cs`
  - `CreateEntryAsync(HttpContextBase ctx, AuthContext auth)` — create entry at request start
  - `UpdateWithResponseAsync(RequestHistoryEntry entry, int statusCode, long responseTimeMs, string requestBody, string responseBody)` — update with response, persist bodies to filesystem as JSON
  - Directory: `RequestHistorySettings.Directory`
  - Files stored as `{objectKey}.json`

- [x] `RequestHistoryCleanupService.cs`
  - Background task (Task.Run loop with delay)
  - On interval: get expired object keys, delete files, delete DB entries
  - Respects CancellationToken

### 7.4 SwiftStack REST Routes

- [x] Initialize `SwiftStackApp("Partio Server")`
- [x] Configure: hostname, port, SSL from settings
- [x] Set custom serializer: `PartioSerializer`
- [x] Configure OpenAPI with tags: Health, Tenants, Users, Credentials, EmbeddingEndpoints, ChunkEmbed, RequestHistory
- [x] Configure SecuritySchemes: Bearer token

#### Authentication Middleware

- [x] `AuthenticationRoute` — extract bearer token from `Authorization: Bearer {token}` header, call AuthenticationService, store AuthContext in `ctx.Metadata`

#### Pre/Post Routing

- [x] `PreRoutingRoute` — set default content type to application/json
- [x] `PostRoutingRoute` — log request method, URL, status code, response time

#### Exception Route

- [x] Map exceptions to HTTP status codes (400, 401, 404, 500, etc.)

#### Health Endpoints (no auth required)

- [x] `HEAD /` — 200 OK
- [x] `GET /` — HTML homepage or JSON health
- [x] `GET /v1.0/health` — `{ "Status": "Healthy", "Version": "..." }`

#### Core Chunk+Embed Endpoint (auth required)

- [x] `POST /v1.0/process` — Single semantic cell
  - Accept: `SemanticCellRequest`
  - Resolve embedding endpoint from DB for the authenticated tenant + requested model
  - Run ChunkingEngine to produce chunks
  - Call embedding client for each chunk (batch if supported)
  - Apply L2 normalization if requested
  - Log to request history
  - Return: `SemanticCellResponse`

- [x] `POST /v1.0/process/batch` — Multiple semantic cells
  - Accept: `List<SemanticCellRequest>`
  - Process each cell, aggregate results
  - Return: `List<SemanticCellResponse>`

#### Tenant Admin Endpoints (admin auth required)

- [x] `PUT /v1.0/tenants` — Create tenant (also creates default user, credential, and embedding endpoints from system defaults)
- [x] `GET /v1.0/tenants/{id}` — Read tenant
- [x] `PUT /v1.0/tenants/{id}` — Update tenant
- [x] `DELETE /v1.0/tenants/{id}` — Delete tenant (cascade delete users, credentials, endpoints)
- [x] `HEAD /v1.0/tenants/{id}` — Check existence
- [x] `POST /v1.0/tenants/enumerate` — Enumerate tenants (accepts EnumerationRequest)

#### User Admin Endpoints (admin auth required)

- [x] `PUT /v1.0/users` — Create user
- [x] `GET /v1.0/users/{id}` — Read user (redacted password)
- [x] `PUT /v1.0/users/{id}` — Update user
- [x] `DELETE /v1.0/users/{id}` — Delete user
- [x] `HEAD /v1.0/users/{id}` — Check existence
- [x] `POST /v1.0/users/enumerate` — Enumerate users (scoped to tenant)

#### Credential Admin Endpoints (admin auth required)

- [x] `PUT /v1.0/credentials` — Create credential
- [x] `GET /v1.0/credentials/{id}` — Read credential
- [x] `PUT /v1.0/credentials/{id}` — Update credential
- [x] `DELETE /v1.0/credentials/{id}` — Delete credential
- [x] `HEAD /v1.0/credentials/{id}` — Check existence
- [x] `POST /v1.0/credentials/enumerate` — Enumerate credentials (scoped to tenant)

#### Embedding Endpoint Admin Endpoints (admin auth required)

- [x] `PUT /v1.0/endpoints` — Create embedding endpoint
- [x] `GET /v1.0/endpoints/{id}` — Read endpoint
- [x] `PUT /v1.0/endpoints/{id}` — Update endpoint
- [x] `DELETE /v1.0/endpoints/{id}` — Delete endpoint
- [x] `HEAD /v1.0/endpoints/{id}` — Check existence
- [x] `POST /v1.0/endpoints/enumerate` — Enumerate endpoints (scoped to tenant)

#### Request History Endpoints (admin auth required)

- [x] `GET /v1.0/requests/{id}` — Read request history entry
- [x] `GET /v1.0/requests/{id}/detail` — Read full detail (filesystem BLOB)
- [x] `POST /v1.0/requests/enumerate` — Enumerate request history (scoped to tenant)
- [x] `DELETE /v1.0/requests/{id}` — Delete request history entry

---

## 8. Phase 6 — Dashboard

### 8.1 Setup

- [x] `package.json` — React 19, React Router 7, Vite
- [x] `vite.config.js` — Proxy `/v1.0` to Partio.Server (default localhost:8400)
- [x] `index.html` — SPA entry
- [x] `Dockerfile` — Multi-stage: Node build → Nginx runtime
- [x] `nginx.conf` — SPA routing with `try_files`, listen on port 8401

### 8.2 Application Shell

- [x] `AppContext.jsx` — Global state: serverUrl, bearerToken, isAdmin, isConnected, error
- [x] `App.jsx` — Routing: Login → Workspace with nested routes
- [x] `main.jsx` — Entry point, render App with context provider
- [x] `index.css` — Global styles (dark sidebar, clean layout — match Conductor/Verbex style)

### 8.3 Components

- [x] **Login.jsx** — Server URL + bearer token (from AdminApiKeys). On success, store in context and navigate to workspace
- [x] **Sidebar.jsx** — Navigation links: Dashboard, Tenants, Users, Credentials, Embedding Endpoints, Request History, Chunk & Embed
- [x] **Topbar.jsx** — App name, connection status, logout
- [x] **Workspace.jsx** — Layout wrapper with Sidebar + content area (Outlet)

### 8.4 Views

- [x] **TenantsView.jsx** — List/create/edit/delete tenants. Pagination via EnumerationRequest. CopyableId for IDs. ActionMenu for row actions.
- [x] **UsersView.jsx** — List/create/edit/delete users (scoped to selected tenant). Password field. Admin toggle.
- [x] **CredentialsView.jsx** — List/create/delete credentials. Show bearer token (CopyableId). Regenerate token.
- [x] **EmbeddingEndpointsView.jsx** — List/create/edit/delete embedding endpoints. Model, endpoint URL, API format dropdown, API key field.
- [x] **RequestHistoryView.jsx** — List request history entries. Click to view detail (request/response bodies).
- [x] **ChunkEmbedView.jsx** — Interactive form:
  - Text area for content (or JSON editor for full SemanticCellRequest)
  - Dropdown for atom type
  - Chunking config fields: strategy, token count, overlap, overlap strategy, context prefix
  - Embedding config fields: model dropdown, L2 normalization toggle
  - Labels (TagInput) and Tags (KeyValueEditor)
  - Submit button → POST /v1.0/process
  - Results panel: show chunks with text, chunked text, and embeddings (truncated with expand)

### 8.5 Reusable Components

- [x] **Modal.jsx** — Generic modal wrapper
- [x] **ActionMenu.jsx** — Three-dot menu with edit/delete/view actions
- [x] **CopyableId.jsx** — Click-to-copy ID display
- [x] **Pagination.jsx** — Page controls using continuation tokens
- [x] **KeyValueEditor.jsx** — Editable key-value pairs for tags
- [x] **TagInput.jsx** — Tag chips with add/remove for labels
- [x] **JsonEditor.jsx** — Raw JSON editing with syntax highlighting

---

## 9. Phase 7 — SDKs

### 9.1 C# SDK (`sdk/csharp/`)

- [x] `Partio.Sdk.csproj` — net8.0;net10.0 class library, zero external dependencies beyond System.Net.Http and System.Text.Json
- [x] `PartioClient.cs`
  - Constructor: `PartioClient(string endpoint, string accessKey)`
  - Implements `IDisposable` (disposes HttpClient)
  - Private `MakeRequestAsync<T>(HttpMethod, string path, object data)` — core HTTP method
  - Bearer token in `Authorization` header
  - **Methods:**
    - `ProcessAsync(SemanticCellRequest)` → `SemanticCellResponse`
    - `ProcessBatchAsync(List<SemanticCellRequest>)` → `List<SemanticCellResponse>`
    - `CreateTenantAsync(...)`, `GetTenantAsync(...)`, `UpdateTenantAsync(...)`, `DeleteTenantAsync(...)`, `TenantExistsAsync(...)`, `EnumerateTenantsAsync(...)`
    - Same pattern for Users, Credentials, EmbeddingEndpoints
    - `GetRequestHistoryAsync(...)`, `GetRequestHistoryDetailAsync(...)`, `EnumerateRequestHistoryAsync(...)`
  - Custom exception: `PartioException` with StatusCode and Response

- [x] `Models/` — DTOs matching server models (SemanticCellRequest, SemanticCellResponse, ChunkResult, etc.)

- [x] **Test Harness** (`Partio.Sdk.TestHarness/Program.cs`)
  - Console app, configurable endpoint and access key
  - Each test: name, PASS/FAIL, runtime (ms)
  - Tests: health check, create tenant, create user, create credential, create endpoint, process text, process batch, enumerate each entity, delete entities, error cases
  - Summary: total tests, passed, failed, overall PASS/FAIL, total runtime
  - Enumeration of failed tests at the end

### 9.2 Python SDK (`sdk/python/`)

- [x] `partio_sdk.py` — Single-file SDK
  - Class `PartioClient(endpoint, access_key)`
  - Uses `requests` library (Session for connection pooling)
  - Bearer token auth
  - Methods mirror C# SDK (snake_case)
  - PascalCase → snake_case key conversion in responses
  - Custom exception: `PartioError`
  - Context manager support (`__enter__`, `__exit__`)

- [x] `requirements.txt` — `requests>=2.28.0`

- [x] `test_harness.py` — Same pattern as C# test harness
  - Each test: name, PASS/FAIL, runtime
  - Summary at end with failed test enumeration

### 9.3 JavaScript SDK (`sdk/js/`)

- [x] `partio-sdk.js` — Single-file SDK (ES6)
  - Class `PartioClient(endpoint, accessKey)`
  - Uses native `fetch` (no external dependencies)
  - Bearer token auth
  - Methods mirror C# SDK (camelCase)
  - PascalCase → camelCase key conversion in responses
  - Custom error: `PartioError` extends `Error`
  - Async/await throughout

- [x] `package.json` — name: "partio-sdk", main: "partio-sdk.js", engines: node >=18

- [x] `test-harness.js` — Same pattern as C# test harness
  - Each test: name, PASS/FAIL, runtime
  - Summary at end with failed test enumeration

---

## 10. Phase 8 — Test.Automated

- [x] `Test.Automated.csproj` — net10.0 console app, references Partio.Sdk
- [x] `Program.cs`
  - Configurable: server endpoint (default `http://localhost:8400`), admin API key (default `partioadmin`), test bearer token (default `default`)
  - Test framework: simple class with `RunTest(string name, Func<Task> test)` that catches exceptions, measures time, records PASS/FAIL

### Test Cases

**Health:**
- [x] Health check GET /
- [ ] Health check GET /v1.0/health

**Tenants:**
- [x] Create tenant
- [x] Read tenant
- [x] Update tenant
- [x] Check tenant existence (HEAD)
- [x] Enumerate tenants
- [x] Delete tenant

**Users:**
- [x] Create user
- [x] Read user
- [x] Update user
- [x] Check user existence
- [x] Enumerate users
- [x] Delete user

**Credentials:**
- [x] Create credential
- [x] Read credential
- [x] Update credential
- [x] Check credential existence
- [x] Enumerate credentials
- [x] Authenticate with new credential bearer token
- [x] Delete credential

**Embedding Endpoints:**
- [x] Create embedding endpoint
- [x] Read embedding endpoint
- [x] Update embedding endpoint
- [x] Check endpoint existence
- [x] Enumerate endpoints
- [x] Delete embedding endpoint

**Chunk & Embed (requires running embedding model):**
- [ ] Process single text cell — FixedTokenCount strategy
- [ ] Process single text cell — SentenceBased strategy
- [ ] Process single text cell — ParagraphBased strategy
- [ ] Process list cell — WholeList strategy
- [ ] Process list cell — ListEntry strategy
- [ ] Process with context prefix
- [ ] Process with L2 normalization
- [ ] Process with overlap (SlidingWindow)
- [ ] Process with overlap (SentenceBoundaryAware)
- [ ] Process batch (multiple cells)
- [ ] Process with labels and tags (verify echoed in response)

**Request History:**
- [x] Enumerate request history (verify entries created)
- [ ] Read request history entry
- [ ] Read request history detail (filesystem BLOB)
- [ ] Delete request history entry

**Error Cases:**
- [x] Unauthenticated request → 401
- [x] Invalid bearer token → 401
- [x] Non-existent resource → 404
- [ ] Invalid request body → 400
- [ ] Non-existent model → 400 or 404

**Summary Output:**
- [x] Total tests, passed, failed
- [x] Overall PASS/FAIL
- [x] Total runtime
- [x] List of failed test names

---

## 11. Phase 9 — Docker & Build

### 11.1 Server Dockerfile

- [x] `src/Partio.Server/Dockerfile`
  ```dockerfile
  FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
  ARG TARGETARCH
  WORKDIR /src
  COPY Partio.Core/Partio.Core.csproj Partio.Core/
  COPY Partio.Server/Partio.Server.csproj Partio.Server/
  RUN dotnet restore Partio.Server/Partio.Server.csproj -a $TARGETARCH
  COPY Partio.Core/ Partio.Core/
  COPY Partio.Server/ Partio.Server/
  RUN dotnet publish Partio.Server/Partio.Server.csproj -c Release -f net10.0 -a $TARGETARCH --no-restore -o /app/publish

  FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
  RUN apt-get update && apt-get install -y --no-install-recommends \
      iputils-ping net-tools traceroute wget curl vim dnsutils netcat-openbsd iproute2 \
      && rm -rf /var/lib/apt/lists/*
  WORKDIR /app
  RUN mkdir -p /app/logs /app/data /app/request-history
  COPY --from=build /app/publish .
  EXPOSE 8400
  ENTRYPOINT ["dotnet", "Partio.Server.dll"]
  ```

### 11.2 Dashboard Dockerfile

- [x] `dashboard/Dockerfile`
  ```dockerfile
  FROM node:22-alpine AS build
  WORKDIR /app
  COPY package*.json ./
  RUN npm ci
  COPY . .
  RUN npm run build

  FROM nginx:alpine AS runtime
  COPY nginx.conf /etc/nginx/conf.d/default.conf
  COPY --from=build /app/dist /usr/share/nginx/html
  EXPOSE 8401
  CMD ["nginx", "-g", "daemon off;"]
  ```

### 11.3 Docker Compose

- [x] `docker/compose.yaml`
  ```yaml
  services:
    partio-server:
      image: jchristn77/partio:latest
      container_name: partio-server
      ports:
        - "8400:8400"
      volumes:
        - ./partio.json:/app/partio.json:ro
        - ./data:/app/data
        - ./logs:/app/logs
        - ./request-history:/app/request-history
      restart: unless-stopped
      healthcheck:
        test: ["CMD", "curl", "-f", "http://localhost:8400/"]
        interval: 5s
        timeout: 2s
        retries: 3
        start_period: 5s

    partio-dashboard:
      image: jchristn77/partio-ui:latest
      container_name: partio-dashboard
      ports:
        - "8401:8401"
      depends_on:
        partio-server:
          condition: service_healthy
      restart: unless-stopped
  ```

- [x] `docker/partio.json` — Example config for Docker (hostname `0.0.0.0`, port 8400, SQLite at `/app/data/partio.db`, logs at `/app/logs/`)

### 11.4 Build Scripts

- [x] `build-server.bat`
  ```batch
  @echo off
  set TAG=%1
  if "%TAG%"=="" set TAG=latest
  cd /d "%~dp0"
  if "%TAG%"=="latest" (
      docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/partio:latest -f src/Partio.Server/Dockerfile --push src
  ) else (
      docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/partio:%TAG% -t jchristn77/partio:latest -f src/Partio.Server/Dockerfile --push src
  )
  ```

- [x] `build-dashboard.bat`
  ```batch
  @echo off
  set TAG=%1
  if "%TAG%"=="" set TAG=latest
  cd /d "%~dp0"
  if "%TAG%"=="latest" (
      docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/partio-ui:latest -f dashboard/Dockerfile --push dashboard
  ) else (
      docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/partio-ui:%TAG% -t jchristn77/partio-ui:latest -f dashboard/Dockerfile --push dashboard
  )
  ```

---

## 12. Phase 10 — Documentation

- [x] **README.md** — Overview, quick start (Docker Compose and native), configuration reference, API summary, SDK usage examples, build instructions, contributing guidelines
- [x] **REST_API.md** — Full API reference: every endpoint with method, path, auth requirement, request body, response body, status codes, examples
- [x] **LICENSE.md** — MIT License
- [x] **CHANGELOG.md** — Initial v0.1.0 entry
- [x] **.gitignore** — bin/, obj/, .vs/, *.user, *.db, logs/, request-history/, node_modules/, dist/, partio.json (in root, not in docker/)

---

## Appendix A — Data Models

### SemanticCellRequest (inbound)

```json
{
    "Type": "Text",
    "Text": "This is some text",
    "UnorderedList": null,
    "OrderedList": null,
    "Table": null,
    "Binary": null,
    "ChunkingConfiguration": {
        "Strategy": "FixedTokenCount",
        "FixedTokenCount": 256,
        "OverlapCount": 32,
        "OverlapPercentage": null,
        "OverlapStrategy": "SlidingWindow",
        "ContextPrefix": "document-id-123 "
    },
    "EmbeddingConfiguration": {
        "Model": "all-minilm",
        "L2Normalization": true
    },
    "Labels": ["document-id-123", "blue"],
    "Tags": {
        "my-key": "my-value",
        "foo": "bar"
    }
}
```

### SemanticCellResponse (outbound)

```json
{
    "Cells": 1,
    "TotalChunks": 1,
    "Chunks": [
        {
            "Text": "This is some text",
            "ChunkedText": "document-id-123 This is some text",
            "Embeddings": [-0.4418234, 0.1234, ...]
        }
    ],
    "Labels": ["document-id-123", "blue"],
    "Tags": {
        "my-key": "my-value",
        "foo": "bar"
    }
}
```

### TenantMetadata

```json
{
    "Id": "ten_abc123...",
    "Name": "My Tenant",
    "Active": true,
    "Labels": ["production"],
    "Tags": { "env": "prod" },
    "CreatedUtc": "2026-02-06T00:00:00Z",
    "LastUpdateUtc": "2026-02-06T00:00:00Z"
}
```

### UserMaster (redacted)

```json
{
    "Id": "usr_abc123...",
    "TenantId": "ten_abc123...",
    "Email": "user@example.com",
    "PasswordSha256": "********",
    "FirstName": "John",
    "LastName": "Doe",
    "IsAdmin": false,
    "Active": true,
    "Labels": [],
    "Tags": {},
    "CreatedUtc": "2026-02-06T00:00:00Z",
    "LastUpdateUtc": "2026-02-06T00:00:00Z"
}
```

### Credential

```json
{
    "Id": "cred_abc123...",
    "TenantId": "ten_abc123...",
    "UserId": "usr_abc123...",
    "Name": "My API Key",
    "BearerToken": "abc123def456...",
    "Active": true,
    "Labels": [],
    "Tags": {},
    "CreatedUtc": "2026-02-06T00:00:00Z",
    "LastUpdateUtc": "2026-02-06T00:00:00Z"
}
```

### EmbeddingEndpoint

```json
{
    "Id": "ep_abc123...",
    "TenantId": "ten_abc123...",
    "Model": "all-minilm",
    "Endpoint": "http://localhost:11434",
    "ApiFormat": "Ollama",
    "ApiKey": null,
    "Active": true,
    "Labels": [],
    "Tags": {},
    "CreatedUtc": "2026-02-06T00:00:00Z",
    "LastUpdateUtc": "2026-02-06T00:00:00Z"
}
```

### EnumerationRequest

```json
{
    "MaxResults": 100,
    "ContinuationToken": null,
    "Order": "CreatedDescending",
    "NameFilter": null,
    "LabelFilter": null,
    "TagKeyFilter": null,
    "TagValueFilter": null,
    "ActiveFilter": null
}
```

### EnumerationResult

```json
{
    "Data": [ ... ],
    "ContinuationToken": "ten_xyz789...",
    "TotalCount": 42,
    "HasMore": true
}
```

---

## Appendix B — API Routes

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `HEAD` | `/` | No | Health check |
| `GET` | `/` | No | Health / homepage |
| `GET` | `/v1.0/health` | No | Health status JSON |
| `POST` | `/v1.0/process` | Bearer | Process single semantic cell |
| `POST` | `/v1.0/process/batch` | Bearer | Process multiple semantic cells |
| `PUT` | `/v1.0/tenants` | Admin | Create tenant |
| `GET` | `/v1.0/tenants/{id}` | Admin | Read tenant |
| `PUT` | `/v1.0/tenants/{id}` | Admin | Update tenant |
| `DELETE` | `/v1.0/tenants/{id}` | Admin | Delete tenant |
| `HEAD` | `/v1.0/tenants/{id}` | Admin | Check tenant existence |
| `POST` | `/v1.0/tenants/enumerate` | Admin | Enumerate tenants |
| `PUT` | `/v1.0/users` | Admin | Create user |
| `GET` | `/v1.0/users/{id}` | Admin | Read user |
| `PUT` | `/v1.0/users/{id}` | Admin | Update user |
| `DELETE` | `/v1.0/users/{id}` | Admin | Delete user |
| `HEAD` | `/v1.0/users/{id}` | Admin | Check user existence |
| `POST` | `/v1.0/users/enumerate` | Admin | Enumerate users |
| `PUT` | `/v1.0/credentials` | Admin | Create credential |
| `GET` | `/v1.0/credentials/{id}` | Admin | Read credential |
| `PUT` | `/v1.0/credentials/{id}` | Admin | Update credential |
| `DELETE` | `/v1.0/credentials/{id}` | Admin | Delete credential |
| `HEAD` | `/v1.0/credentials/{id}` | Admin | Check credential existence |
| `POST` | `/v1.0/credentials/enumerate` | Admin | Enumerate credentials |
| `PUT` | `/v1.0/endpoints` | Admin | Create embedding endpoint |
| `GET` | `/v1.0/endpoints/{id}` | Admin | Read embedding endpoint |
| `PUT` | `/v1.0/endpoints/{id}` | Admin | Update embedding endpoint |
| `DELETE` | `/v1.0/endpoints/{id}` | Admin | Delete embedding endpoint |
| `HEAD` | `/v1.0/endpoints/{id}` | Admin | Check endpoint existence |
| `POST` | `/v1.0/endpoints/enumerate` | Admin | Enumerate embedding endpoints |
| `GET` | `/v1.0/requests/{id}` | Admin | Read request history entry |
| `GET` | `/v1.0/requests/{id}/detail` | Admin | Read request history detail |
| `POST` | `/v1.0/requests/enumerate` | Admin | Enumerate request history |
| `DELETE` | `/v1.0/requests/{id}` | Admin | Delete request history entry |

---

## Appendix C — Settings File (partio.json)

```json
{
    "Rest": {
        "Hostname": "localhost",
        "Port": 8400,
        "Ssl": false
    },
    "Database": {
        "Type": "Sqlite",
        "Filename": "./partio.db",
        "Hostname": null,
        "Port": 0,
        "DatabaseName": null,
        "Username": null,
        "Password": null,
        "Instance": null,
        "Schema": null,
        "RequireEncryption": false,
        "LogQueries": false
    },
    "Logging": {
        "SyslogServers": [],
        "ConsoleLogging": true,
        "EnableColors": false,
        "LogDirectory": "./logs/",
        "LogFilename": "partio.log",
        "FileLogging": true,
        "IncludeDateInFilename": true,
        "MinimumSeverity": 0
    },
    "Debug": {
        "Authentication": false,
        "Exceptions": true,
        "Requests": false,
        "DatabaseQueries": false
    },
    "RequestHistory": {
        "Enabled": true,
        "Directory": "./request-history/",
        "RetentionDays": 7,
        "CleanupIntervalMinutes": 60,
        "MaxRequestBodyBytes": 65536,
        "MaxResponseBodyBytes": 65536
    },
    "AdminApiKeys": [
        "partioadmin"
    ],
    "DefaultEmbeddingEndpoints": [
        {
            "Model": "all-minilm",
            "Endpoint": "http://localhost:11434",
            "ApiFormat": "Ollama",
            "ApiKey": null
        },
        {
            "Model": "text-embedding-3-small",
            "Endpoint": "https://api.openai.com",
            "ApiFormat": "OpenAI",
            "ApiKey": null
        },
        {
            "Model": "text-embedding-3-large",
            "Endpoint": "https://api.openai.com",
            "ApiFormat": "OpenAI",
            "ApiKey": null
        }
    ]
}
```

---

## Appendix D — Database Schema

### tenants

| Column | Type | Constraints |
|--------|------|-------------|
| id | VARCHAR(48) | PRIMARY KEY |
| name | VARCHAR(256) | NOT NULL |
| active | BOOLEAN | NOT NULL DEFAULT 1 |
| labels_json | TEXT | NULL |
| tags_json | TEXT | NULL |
| created_utc | TEXT | NOT NULL |
| last_update_utc | TEXT | NOT NULL |

### users

| Column | Type | Constraints |
|--------|------|-------------|
| id | VARCHAR(48) | PRIMARY KEY |
| tenant_id | VARCHAR(48) | NOT NULL, FK → tenants.id |
| email | VARCHAR(256) | NOT NULL |
| password_sha256 | VARCHAR(64) | NOT NULL |
| first_name | VARCHAR(128) | NULL |
| last_name | VARCHAR(128) | NULL |
| is_admin | BOOLEAN | NOT NULL DEFAULT 0 |
| active | BOOLEAN | NOT NULL DEFAULT 1 |
| labels_json | TEXT | NULL |
| tags_json | TEXT | NULL |
| created_utc | TEXT | NOT NULL |
| last_update_utc | TEXT | NOT NULL |

### credentials

| Column | Type | Constraints |
|--------|------|-------------|
| id | VARCHAR(48) | PRIMARY KEY |
| tenant_id | VARCHAR(48) | NOT NULL, FK → tenants.id |
| user_id | VARCHAR(48) | NOT NULL, FK → users.id |
| name | VARCHAR(256) | NULL |
| bearer_token | VARCHAR(64) | NOT NULL, UNIQUE |
| active | BOOLEAN | NOT NULL DEFAULT 1 |
| labels_json | TEXT | NULL |
| tags_json | TEXT | NULL |
| created_utc | TEXT | NOT NULL |
| last_update_utc | TEXT | NOT NULL |

### embedding_endpoints

| Column | Type | Constraints |
|--------|------|-------------|
| id | VARCHAR(48) | PRIMARY KEY |
| tenant_id | VARCHAR(48) | NOT NULL, FK → tenants.id |
| model | VARCHAR(256) | NOT NULL |
| endpoint | VARCHAR(512) | NOT NULL |
| api_format | VARCHAR(32) | NOT NULL |
| api_key | VARCHAR(512) | NULL |
| active | BOOLEAN | NOT NULL DEFAULT 1 |
| labels_json | TEXT | NULL |
| tags_json | TEXT | NULL |
| created_utc | TEXT | NOT NULL |
| last_update_utc | TEXT | NOT NULL |

### request_history

| Column | Type | Constraints |
|--------|------|-------------|
| id | VARCHAR(48) | PRIMARY KEY |
| tenant_id | VARCHAR(48) | NULL |
| user_id | VARCHAR(48) | NULL |
| credential_id | VARCHAR(48) | NULL |
| requestor_ip | VARCHAR(64) | NULL |
| http_method | VARCHAR(16) | NULL |
| http_url | VARCHAR(512) | NULL |
| request_body_length | BIGINT | NULL |
| response_body_length | BIGINT | NULL |
| http_status | INTEGER | NULL |
| response_time_ms | BIGINT | NULL |
| object_key | VARCHAR(256) | NULL |
| created_utc | TEXT | NOT NULL |
| completed_utc | TEXT | NULL |

---

## Code Standards Reference

| Rule | Detail |
|------|--------|
| No `var` | Always use explicit types |
| No tuples | Use named classes/structs instead |
| No `JsonPropertyName` | No `JsonNamingPolicy` — serialize PascalCase natively |
| Private members | Named `_LikeThis` |
| Public properties | Value-checking on set where appropriate |
| XML documentation | On all public members |
| Min/max/default | Documented in XML `<remarks>` or `<summary>` |
| Logging | SyslogLogging `LoggingModule` with `_Header` prefix pattern |
| IDs | K-sortable via PrettyId with semantic prefix, 48 chars |
| Serialization | SerializationHelper (Serializer class), WriteIndented, PascalCase |
| Async | `ConfigureAwait(false)` throughout, CancellationToken where applicable |

---

## NuGet Dependencies

### Partio.Core

| Package | Purpose |
|---------|---------|
| SwiftStack | REST framework (referenced for ISerializer) |
| SyslogLogging | Logging |
| PrettyId | K-sortable ID generation |
| SerializationHelper | JSON serialization |
| SharpToken | BPE token counting (tiktoken) |
| Microsoft.Data.Sqlite | SQLite driver |
| Npgsql | PostgreSQL driver |
| MySql.Data | MySQL driver |
| Microsoft.Data.SqlClient | SQL Server driver |

### Partio.Server

| Package | Purpose |
|---------|---------|
| SwiftStack | REST server framework |
| SyslogLogging | Logging |
| SerializationHelper | JSON serialization |
| Inputty | Console input helpers |

### Test.Automated

| Package | Purpose |
|---------|---------|
| (Project reference to Partio.Sdk) | SDK for API calls |

---

## Implementation Order

1. **Partio.Core foundation** — Constants, IdGenerator, Enums, Settings, Models, Serialization
2. **Database layer** — Abstract base, interfaces, SQLite implementation first (fastest to test)
3. **Chunking engine** — All 5 strategies with SharpToken
4. **Embedding clients** — Ollama and OpenAI
5. **Partio.Server** — Main entry, auth, routes, request history
6. **Test locally** — Run server, test with curl/Postman
7. **Remaining DB drivers** — PostgreSQL, MySQL, SQL Server
8. **Dashboard** — React app with all views
9. **SDKs** — C#, Python, JavaScript with test harnesses
10. **Test.Automated** — Comprehensive test suite
11. **Docker & build** — Dockerfiles, compose, build scripts
12. **Documentation** — README, REST_API, LICENSE, CHANGELOG, .gitignore
