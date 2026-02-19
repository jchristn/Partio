<p align="center">
  <img src="assets/logo-dark-text.png" alt="Partio" width="192" height="192">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square" alt=".NET 10.0">
  <img src="https://img.shields.io/badge/license-MIT-green?style=flat-square" alt="MIT License">
  <img src="https://img.shields.io/badge/docker-jchristn77%2Fpartio--server-2496ED?style=flat-square&logo=docker&logoColor=white" alt="Docker Server">
  <img src="https://img.shields.io/badge/docker-jchristn77%2Fpartio--dashboard-2496ED?style=flat-square&logo=docker&logoColor=white" alt="Docker Dashboard">
</p>

Partio is a multi-tenant RESTful platform that accepts semantic cells (text, lists, tables, images, code, and more) with a chunking and embedding configuration, and returns chunked text with computed embeddings. Partio also supports optional LLM-powered summarization of cells before chunking and embedding. Partio lets you define endpoints (provider + model + chunking policy) and call them consistently across tenants and applications, so you can focus on building your product instead of managing chunking and embedding infrastructure.

### Quick Example

Send a semantic cell with a chunking config:

```bash
curl -X POST http://localhost:8400/v1.0/process \
  -H "Authorization: Bearer partioadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "Type": "Text",
    "Text": "Partio centralizes your chunking and embedding workflow. It accepts semantic cells and returns chunks with embeddings.",
    "ChunkingConfiguration": {
      "Strategy": "SentenceBased"
    },
    "EmbeddingConfiguration": {
      "EmbeddingEndpointId": "eep_abc123"
    }
  }'
```

Get back chunks with computed embeddings:

```json
{
  "Text": "Partio centralizes your chunking and embedding workflow. It accepts semantic cells and returns chunks with embeddings.",
  "Chunks": [
    {
      "Text": "Partio centralizes your chunking and embedding workflow.",
      "Labels": [],
      "Tags": {},
      "Embeddings": [0.0123, -0.0456, 0.0789, "... (384 floats for all-minilm)"]
    },
    {
      "Text": "It accepts semantic cells and returns chunks with embeddings.",
      "Labels": [],
      "Tags": {},
      "Embeddings": [0.0321, -0.0654, 0.0987, "... (384 floats for all-minilm)"]
    }
  ]
}
```

### What Is a Semantic Cell?

A semantic cell is a typed unit of content from a parsed document. Rather than sending raw text, you send structured content that Partio can chunk intelligently based on its type:

```json
{
  "Type": "Text | List | Table | Code | Image | Hyperlink | Meta | Binary",
  "Text": "string (for Text, Code, Hyperlink, Meta, Image, Binary)",
  "OrderedList": ["item 1", "item 2"],
  "UnorderedList": ["item a", "item b"],
  "Table": [["id", "name"], ["1", "Alice"], ["2", "Bob"]],
  "Labels": ["source:readme", "section:intro"],
  "Tags": { "page": "3", "heading": "Introduction" }
}
```

Each type unlocks different chunking strategies. Text can be split by tokens, sentences, or paragraphs. Lists can be chunked whole or per-entry. Tables can be chunked by row, row groups, key-value pairs, or as a whole. Labels and tags are echoed back on each chunk for downstream traceability.

## Who Is This For?

- **AI/ML Engineers** building RAG pipelines who need a dedicated chunking and embeddings service that decouples document processing from the rest of the stack
- **DevOps Teams** looking to centralize and scale embedding generation behind a single API, with support for multiple models and providers
- **Platform Engineers** who need multi-tenant isolation, audit logging, and database portability for chunking and embeddings workloads
- **Developers** prototyping semantic search, knowledge bases, or AI-powered features who want to get started quickly without wiring up chunking and embedding logic by hand

## Why Partio vs. Rolling Your Own?

- **Semantic-cell-aware chunking**: Partio understands the structure of your content (text, lists, tables, code) and applies type-appropriate chunking strategies, not just naive token splitting
- **Policy-managed endpoints**: Define endpoints with a specific provider, model, and chunking policy, then call them uniformly across tenants and applications
- **Traceability and audit**: Every request is logged with full history, and labels/tags flow through from input to each output chunk for end-to-end traceability
- **Database portability**: Switch between SQLite, PostgreSQL, MySQL, and SQL Server with a config change, no code modifications
- **Multi-tenant isolation**: Tenants, users, credentials, and endpoints are fully isolated, with scoped bearer token authentication
- **SDKs and dashboard included**: Ship with C#, Python, and JavaScript SDKs plus a React admin dashboard, so you're not just getting an API

## Features

- **Multiple chunking strategies** including fixed token count, sentence-based, paragraph-based, whole list, and list entry, with configurable overlap via sliding window
- **Pluggable embedding providers** supporting both Ollama and OpenAI-compatible APIs, selectable per endpoint
- **Multi-tenant architecture** with tenant, user, credential, and endpoint isolation
- **Four database backends** out of the box: SQLite (default, zero config), PostgreSQL, MySQL, and SQL Server
- **Request history and audit logging** with automatic cleanup, filesystem body persistence, configurable retention, and upstream embedding call capture (request/response headers, bodies, timing, and status for each call to the embedding provider)
- **Bearer token authentication** with global admin API keys and tenant-scoped credentials
- **Endpoint health checks** with configurable background monitoring, threshold-based state transitions, and automatic request gating (unhealthy endpoints return 502)
- **Batch processing** for submitting multiple semantic cells in a single request
- **Optional summarization** with LLM-powered cell summarization before chunking and embedding, supporting top-down and bottom-up strategies
- **Completion endpoint management** for configuring LLM inference endpoints (Ollama, OpenAI) with health checks
- **Admin dashboard** (React/Vite) for managing tenants, users, credentials, endpoints, and viewing request history
- **SDKs** for C#, Python, and JavaScript
- **Docker images** with multi-architecture support (amd64/arm64)
- **Pagination and filtering** with cursor-based continuation tokens, sorting, and label/tag/name/active filters on all list endpoints

## Getting Started

### Prerequisites

Partio requires an embedding provider to generate embeddings. Out of the box, Partio is configured to use [Ollama](https://ollama.com/) with the `all-minilm` model.

When using **Docker Compose** (recommended), Ollama is included automatically — just pull a model after starting the services (see below).

When running **from source** or via **Docker (server only)**, install Ollama separately:

1. [Install Ollama](https://ollama.com/download) and start it (default: `http://localhost:11434`)
2. Pull an embedding model: `ollama pull all-minilm`

### Docker Compose (Recommended)

The fastest way to run Partio with all components. The Compose file includes Partio server, dashboard, and a local Ollama instance with persistent model storage.

```bash
git clone https://github.com/jchristn/partio.git
cd partio/docker
docker compose up -d
```

Pull the default embedding model:

```bash
# Bash / macOS / Linux
curl http://localhost:11434/api/pull -d '{"name": "all-minilm"}'

# Windows Terminal (cmd)
curl http://localhost:11434/api/pull -d "{\"name\": \"all-minilm\"}"
```

| Component | URL | Docker Image |
|-----------|-----|--------------|
| Server | http://localhost:8400 | `jchristn77/partio-server` |
| Dashboard | http://localhost:8401 | `jchristn77/partio-dashboard` |
| Ollama | (internal via shared network) | `ollama/ollama` |

The Ollama container shares the server's network namespace, so the default `localhost:11434` endpoint works without any configuration changes. Models are persisted in a Docker volume across restarts.

Default admin API key: `partioadmin`

### Docker (Server Only)

```bash
docker run -d -p 8400:8400 jchristn77/partio-server
```

### From Source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/jchristn/partio.git
cd partio/src
dotnet build
dotnet run --project Partio.Server
```

### First Run Defaults

On first startup, Partio creates a `partio.json` settings file and initializes the database with these defaults:

> [!CAUTION]
> **Local development only.** Change all default credentials before any production or shared deployment.
>
> | Resource | ID | Details |
> |----------|----|---------|
> | Tenant | `default` | Default Tenant |
> | User | `default` | admin@partio / password (admin) |
> | Credential | `default` | Bearer token `default` |
> | Admin API Key | &mdash; | `partioadmin` |

## API Overview

All endpoints use JSON and require an `Authorization: Bearer {token}` header unless otherwise noted. See [REST_API.md](REST_API.md) for the full API reference. A [Postman collection](Partio.postman_collection.json) is also included in the repository.

### Health and Identity

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `HEAD` | `/` | No | Health check |
| `GET` | `/` | No | Health status |
| `GET` | `/v1.0/health` | No | Health (JSON) |
| `GET` | `/v1.0/whoami` | Yes | Caller identity |

### Processing

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/v1.0/process` | Process a single semantic cell |
| `POST` | `/v1.0/process/batch` | Process multiple semantic cells |

### Endpoint Health

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/v1.0/endpoints/embedding/{id}/health` | Yes | Health status for one endpoint |
| `GET` | `/v1.0/endpoints/embedding/health` | Yes | Health status for all endpoints |

### Administration (CRUD + Enumerate)

Each admin resource supports `PUT` (create), `GET` (read), `PUT /{id}` (update), `DELETE /{id}`, `HEAD /{id}` (exists), and `POST /enumerate` (list).

| Resource | Route Prefix | ID Prefix |
|----------|-------------|-----------|
| Tenants | `/v1.0/tenants` | `ten_` |
| Users | `/v1.0/users` | `usr_` |
| Credentials | `/v1.0/credentials` | `cred_` |
| Endpoints | `/v1.0/endpoints/embedding` | `eep_` |
| Completion Endpoints | `/v1.0/endpoints/completion` | `cep_` |
| Request History | `/v1.0/requests` | `req_` |

### Example: Process a Text Cell

```bash
curl -X POST http://localhost:8400/v1.0/process \
  -H "Authorization: Bearer partioadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "Type": "Text",
    "Text": "Partio virtualizes your chunking and embedding workflow.",
    "ChunkingConfiguration": {
      "Strategy": "FixedTokenCount",
      "FixedTokenCount": 256
    },
    "EmbeddingConfiguration": {
      "EmbeddingEndpointId": "eep_YOUR_ENDPOINT_ID",
      "L2Normalization": false
    }
  }'
```

### Example: Batch Processing

```bash
curl -X POST http://localhost:8400/v1.0/process/batch \
  -H "Authorization: Bearer partioadmin" \
  -H "Content-Type: application/json" \
  -d '[
    { "Type": "Text", "Text": "First document to embed." },
    { "Type": "Text", "Text": "Second document to embed." }
  ]'
```

## Chunking Strategies

| Strategy | Description |
|----------|-------------|
| `FixedTokenCount` | Split content into chunks of a fixed token count (uses cl100k_base encoding). Configurable overlap via `OverlapCount` or `OverlapPercentage`. |
| `SentenceBased` | Split at sentence boundaries. |
| `ParagraphBased` | Split at paragraph boundaries. |
| `WholeList` | Treat an entire list as a single chunk. |
| `ListEntry` | Each list entry becomes its own chunk. |
| `Row` | Each table data row as space-separated values (no headers). |
| `RowWithHeaders` | Each table data row as a markdown table with headers prepended. |
| `RowGroupWithHeaders` | Groups of N table rows with headers (configurable via `RowGroupSize`, default 5). |
| `KeyValuePairs` | Each table row as key-value pairs (e.g. `"id: 1, firstname: george, lastname: bush"`). |
| `WholeTable` | Entire table as a single markdown table chunk. |
| `RegexBased` | Split at boundaries defined by a user-supplied regular expression (`RegexPattern`). Works with any content type. |

Supported content types: Text, Code, Hyperlink, Meta, Lists (ordered/unordered), Tables, Binary, and Image.

### Strategy Compatibility

Not all strategies work with all content types. The generic strategies (`FixedTokenCount`, `SentenceBased`, `ParagraphBased`, `RegexBased`) work with any type. List strategies (`WholeList`, `ListEntry`) only work with `List`. Table strategies (`Row`, `RowWithHeaders`, `RowGroupWithHeaders`, `KeyValuePairs`, `WholeTable`) only work with `Table`. The API returns `400 Bad Request` if an incompatible strategy is used.

| Strategy | Text | Code | Hyperlink | Meta | List | Table | Binary | Image | Unknown |
|---|---|---|---|---|---|---|---|---|---|
| FixedTokenCount | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| SentenceBased | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| ParagraphBased | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| RegexBased | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| WholeList | | | | | Y | | | | |
| ListEntry | | | | | Y | | | | |
| Row | | | | | | Y | | | |
| RowWithHeaders | | | | | | Y | | | |
| RowGroupWithHeaders | | | | | | Y | | | |
| KeyValuePairs | | | | | | Y | | | |
| WholeTable | | | | | | Y | | | |

## Configuration

Partio is configured via `partio.json`, created automatically on first run.

```json
{
  "Rest": {
    "Hostname": "0.0.0.0",
    "Port": 8400,
    "Ssl": false
  },
  "Database": {
    "Type": "Sqlite",
    "Filename": "./partio.db"
  },
  "Logging": {
    "ConsoleLogging": true,
    "FileLogging": true,
    "LogDirectory": "./logs/",
    "LogFilename": "partio.log",
    "MinimumSeverity": 0
  },
  "RequestHistory": {
    "Enabled": true,
    "Directory": "./request-history/",
    "RetentionDays": 7,
    "CleanupIntervalMinutes": 60
  },
  "AdminApiKeys": ["partioadmin"],
  "DefaultEmbeddingEndpoints": [
    {
      "Model": "all-minilm",
      "Endpoint": "http://localhost:11434",
      "ApiFormat": "Ollama"
    }
  ]
}
```

### Database Options

| Type | Config Value | Notes |
|------|-------------|-------|
| SQLite | `Sqlite` | Default. Zero configuration, file-based. |
| PostgreSQL | `Postgresql` | Set `Hostname`, `Port`, `DatabaseName`, `Username`, `Password`. |
| MySQL | `Mysql` | Set `Hostname`, `Port`, `DatabaseName`, `Username`, `Password`. |
| SQL Server | `SqlServer` | Set `Hostname`, `Port`, `DatabaseName`, `Username`, `Password`. |

## SDKs

### C#

```csharp
using Partio.Sdk;
using Partio.Sdk.Models;

using PartioClient client = new PartioClient("http://localhost:8400", "partioadmin");

SemanticCellResponse? response = await client.ProcessAsync(new SemanticCellRequest
{
    Type = "Text",
    Text = "Hello world",
    EmbeddingConfiguration = new EmbeddingConfiguration { EmbeddingEndpointId = "eep_YOUR_ENDPOINT_ID" }
});
```

### Python

```python
from partio_sdk import PartioClient

with PartioClient("http://localhost:8400", "partioadmin") as client:
    result = client.process({
        "Type": "Text",
        "Text": "Hello world",
        "ChunkingConfiguration": {"Strategy": "FixedTokenCount", "FixedTokenCount": 256},
        "EmbeddingConfiguration": {"EmbeddingEndpointId": "eep_YOUR_ENDPOINT_ID"}
    })
```

### JavaScript

```javascript
import { PartioClient } from './partio-sdk.js';

const client = new PartioClient('http://localhost:8400', 'partioadmin');
const result = await client.process({
  Type: 'Text',
  Text: 'Hello world',
  ChunkingConfiguration: { Strategy: 'FixedTokenCount', FixedTokenCount: 256 },
  EmbeddingConfiguration: { EmbeddingEndpointId: 'eep_YOUR_ENDPOINT_ID' }
});
```

## Architecture

```
Partio.Core          - Models, settings, database, chunking engine, embedding clients
Partio.Server        - REST API server, authentication, request history
dashboard/           - React/Vite admin dashboard
sdk/csharp/          - C# SDK and test harness
sdk/python/          - Python SDK and test harness
sdk/js/              - JavaScript SDK and test harness
docker/              - Docker Compose setup and default configuration
```

## Docker Images

| Image | Description | Default Port |
|-------|-------------|-------------|
| `jchristn77/partio-server` | Partio REST API server | 8400 |
| `jchristn77/partio-dashboard` | React admin dashboard (Nginx) | 8401 |

Both images support `linux/amd64` and `linux/arm64`.

### Building Locally

```bash
# Server
build-server.bat [tag]

# Dashboard
build-dashboard.bat [tag]
```

## Production Notes

- **Rotate credentials**: Change the default admin API key (`partioadmin`) and default user password immediately. Use the admin API to create tenant-scoped credentials with limited access.
- **Multi-tenant isolation**: Tenants are isolated at the database row level. Each tenant has its own users, credentials, and endpoints. Cross-tenant access is prevented by scoped bearer tokens.
- **Request history and retention**: Request/response bodies are persisted to the filesystem. Configure `RetentionDays` and `CleanupIntervalMinutes` in `partio.json` to control disk usage. Set `RequestHistory.Enabled` to `false` to disable entirely.
- **Horizontal scaling**: Partio is stateless beyond the database. Run multiple instances behind a load balancer pointing to the same database for horizontal scale. Request history files should use shared storage (e.g. NFS, EFS) if enabled across instances.
- **Rate limiting**: Not currently built in. Place Partio behind a reverse proxy (Nginx, Envoy, API gateway) to enforce rate limits and quotas.
- **Reproducibility**: Chunk output is deterministic for a given input, chunking strategy, and configuration. Embedding output depends on the upstream model and provider.

## Troubleshooting

### Server won't start or port is in use

Verify that port 8400 (server) and 8401 (dashboard) are not in use by another process. You can change the port in `partio.json` under `Rest.Port`.

### 401 Unauthorized on every request

Ensure you are passing the `Authorization: Bearer {token}` header. The default admin API key is `partioadmin`. If using a credential token, verify the credential, its associated user, and the tenant are all marked as active.

### Embedding requests fail or return errors

- If using Docker Compose, make sure you pulled a model:
  - Bash: `curl http://localhost:11434/api/pull -d '{"name": "all-minilm"}'`
  - Windows cmd: `curl http://localhost:11434/api/pull -d "{\"name\": \"all-minilm\"}"`
- If running the server standalone in Docker with an external Ollama, `localhost` inside the container is not the host machine; use `host.docker.internal` or the container network address instead.
- Verify the model name matches what your embedding provider expects (e.g. `all-minilm` for Ollama).
- Check that `ApiFormat` is set correctly (`Ollama` or `OpenAI`).

### Dashboard cannot connect to the server

The dashboard connects to the server URL you enter on the login screen. If both are running in Docker, use `http://localhost:8400` from the browser (the dashboard runs client-side). If the server is on a different host, use that host's address.

### Database connection issues (PostgreSQL, MySQL, SQL Server)

Verify `Hostname`, `Port`, `DatabaseName`, `Username`, and `Password` in `partio.json` under `Database`. Ensure the target database server is running and accessible from the Partio server's network.

### Chunks are too large or too small

Adjust `FixedTokenCount` in your `ChunkingConfiguration`. The server caps chunk size to the model's context window automatically. Use `OverlapCount` or `OverlapPercentage` with `OverlapStrategy: "SlidingWindow"` for overlapping chunks.

### Request history filling up disk

Request history bodies are stored on the filesystem under the configured `RequestHistory.Directory`. Reduce `RetentionDays` or `CleanupIntervalMinutes` in `partio.json`, or set `RequestHistory.Enabled` to `false` to disable logging entirely.

### Enable debug logging

Set the following in `partio.json` and restart the server:

```json
{
  "Logging": { "MinimumSeverity": 0 },
  "Debug": {
    "Authentication": true,
    "Exceptions": true,
    "Requests": true,
    "DatabaseQueries": true
  }
}
```

Logs are written to `./logs/` by default.

## Issues and Feedback

Have a question, found a bug, or want to request a feature?

- **Bug reports and feature requests**: [Open an issue](https://github.com/jchristn/partio/issues)
- **Questions and discussions**: [Start a discussion](https://github.com/jchristn/partio/discussions)

When filing an issue, please include:
1. The Partio version (`v0.2.0`, or the Docker image tag)
2. Steps to reproduce the problem
3. The request/response (redact any credentials)
4. Relevant log output from `./logs/`

## Version History

Refer to [CHANGELOG.md](CHANGELOG.md) for the full version history.

## License

[MIT](LICENSE.md) &copy; 2026 Joel Christner
