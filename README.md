<img src="assets/logo-dark-text.png" alt="Partio" width="192" height="192">

Partio is a multi-tenant RESTful platform that accepts semantic cells (text, lists, tables, images, etc.) with a chunking and embedding configuration, and returns chunked text with computed embeddings.

## Quick Start

### Docker Compose

```bash
cd docker
docker compose up -d
```

The server will be available at `http://localhost:8400` and the dashboard at `http://localhost:8401`.

Default admin API key: `partioadmin`

### Native (requires .NET 10 SDK)

```bash
cd src
dotnet build
dotnet run --project Partio.Server
```

On first run, Partio creates a `partio.json` settings file and initializes the database with default records:

- **Tenant**: Default Tenant (id: `default`)
- **User**: admin@partio / password (id: `default`)
- **Credential**: Bearer token `default`
- **Admin API Key**: `partioadmin`

> **Warning**: Change these credentials before production use.

## Configuration

Partio is configured via `partio.json`. See [Appendix C in PARTIO.md](PARTIO.md#appendix-c--settings-file-partiojson) for the full schema.

Key settings:

| Setting | Default | Description |
|---------|---------|-------------|
| `Rest.Hostname` | `localhost` | Bind hostname |
| `Rest.Port` | `8400` | Listen port |
| `Database.Type` | `Sqlite` | Database backend (Sqlite, Postgresql, Mysql, SqlServer) |
| `Database.Filename` | `./partio.db` | SQLite database path |
| `AdminApiKeys` | `["partioadmin"]` | Admin bearer tokens |

## API Overview

All API endpoints use JSON and require a `Authorization: Bearer {token}` header (except health checks).

| Category | Endpoints |
|----------|-----------|
| Health | `HEAD /`, `GET /`, `GET /v1.0/health` |
| Process | `POST /v1.0/process`, `POST /v1.0/process/batch` |
| Tenants | CRUD at `/v1.0/tenants` |
| Users | CRUD at `/v1.0/users` |
| Credentials | CRUD at `/v1.0/credentials` |
| Endpoints | CRUD at `/v1.0/endpoints` |
| History | `/v1.0/requests` |

See [REST_API.md](REST_API.md) for full API documentation.

### Example: Process Text

```bash
curl -X POST http://localhost:8400/v1.0/process \
  -H "Authorization: Bearer partioadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "Type": "Text",
    "Text": "This is some text to chunk and embed.",
    "ChunkingConfiguration": {
      "Strategy": "FixedTokenCount",
      "FixedTokenCount": 256
    },
    "EmbeddingConfiguration": {
      "Model": "all-minilm"
    }
  }'
```

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
    EmbeddingConfiguration = new EmbeddingConfiguration { Model = "all-minilm" }
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
        "EmbeddingConfiguration": {"Model": "all-minilm"}
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
  EmbeddingConfiguration: { Model: 'all-minilm' }
});
```

## Architecture

```
Partio.Core          - Models, settings, database, chunking, embedding clients
Partio.Server        - SwiftStack REST server, authentication, request history
dashboard/           - React/Vite admin dashboard
sdk/csharp/          - C# SDK and test harness
sdk/python/          - Python SDK and test harness
sdk/js/              - JavaScript SDK and test harness
```

## Database Support

- **SQLite** (default) - Zero configuration, file-based
- **PostgreSQL** - Via Npgsql
- **MySQL** - Via MySql.Data
- **SQL Server** - Via Microsoft.Data.SqlClient

## Building Docker Images

```bash
# Server
build-server.bat [tag]

# Dashboard
build-dashboard.bat [tag]
```

## License

MIT License. See [LICENSE.md](LICENSE.md).
