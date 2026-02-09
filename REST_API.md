# Partio REST API Reference

Base URL: `http://localhost:8400`

All requests require `Authorization: Bearer {token}` header unless noted otherwise.
All request/response bodies are JSON with `Content-Type: application/json`.

---

## Health

### HEAD /
Health check (no auth required).

**Response**: `200 OK`

### GET /
Health status (no auth required).

**Response**: `200 OK`
```json
{ "Status": "Healthy", "Version": "0.1.0" }
```

### GET /v1.0/health
Health status JSON (no auth required).

**Response**: `200 OK`
```json
{ "Status": "Healthy", "Version": "0.1.0" }
```

---

## Identity

### GET /v1.0/whoami
Returns the role and tenant of the authenticated caller.

**Response**: `200 OK`
```json
{ "Role": "Admin", "TenantName": "Admin" }
```
- `Role` — `"Admin"` or `"User"`
- `TenantName` — `"Admin"` for global admins, or the tenant's name

---

## Process (Chunk & Embed)

### POST /v1.0/endpoints/{id}/process
Process a single semantic cell using the specified embedding endpoint. Requires bearer token authentication.

**Path Parameters**:
- `id` — Embedding endpoint ID (e.g. `ep_xxxx`). The endpoint must belong to the caller's tenant (non-admin) and be active.

**Request Body**: `SemanticCellRequest`

The `Type` field determines which content field is used. Supported types: `Text`, `List`, `Table`, `Code`, `Hyperlink`, `Meta`.

#### Text
```json
{
    "Type": "Text",
    "Text": "Your text content here...",
    "ChunkingConfiguration": {
        "Strategy": "FixedTokenCount",
        "FixedTokenCount": 256,
        "OverlapCount": 32,
        "OverlapStrategy": "SlidingWindow",
        "ContextPrefix": "doc-123 "
    },
    "EmbeddingConfiguration": {
        "L2Normalization": true
    },
    "Labels": ["label1"],
    "Tags": { "key": "value" }
}
```

#### Unordered List
```json
{
    "Type": "List",
    "UnorderedList": ["First item", "Second item", "Third item"],
    "ChunkingConfiguration": {
        "Strategy": "WholeList"
    },
    "EmbeddingConfiguration": {
        "L2Normalization": false
    }
}
```

#### Ordered List
```json
{
    "Type": "List",
    "OrderedList": ["Step one", "Step two", "Step three"],
    "ChunkingConfiguration": {
        "Strategy": "ListEntry"
    },
    "EmbeddingConfiguration": {
        "L2Normalization": false
    }
}
```

#### Table (RowWithHeaders)
```json
{
    "Type": "Table",
    "Table": [
        ["Name", "Age", "City"],
        ["Alice", "30", "New York"],
        ["Bob", "25", "London"]
    ],
    "ChunkingConfiguration": {
        "Strategy": "RowWithHeaders"
    },
    "EmbeddingConfiguration": {
        "L2Normalization": false
    }
}
```

#### Code
```json
{
    "Type": "Code",
    "Text": "function hello() {\n  return 'world';\n}",
    "ChunkingConfiguration": {
        "Strategy": "FixedTokenCount",
        "FixedTokenCount": 256
    },
    "EmbeddingConfiguration": {
        "L2Normalization": false
    }
}
```

#### Hyperlink
```json
{
    "Type": "Hyperlink",
    "Text": "https://example.com - Example website description",
    "ChunkingConfiguration": {
        "Strategy": "FixedTokenCount",
        "FixedTokenCount": 256
    },
    "EmbeddingConfiguration": {
        "L2Normalization": false
    }
}
```

#### Meta
```json
{
    "Type": "Meta",
    "Text": "Author: John Doe | Created: 2026-01-15 | Version: 2.1",
    "ChunkingConfiguration": {
        "Strategy": "FixedTokenCount",
        "FixedTokenCount": 256
    },
    "EmbeddingConfiguration": {
        "L2Normalization": false
    }
}
```

#### Table (Row)
```json
{
    "Type": "Table",
    "Table": [
        ["Name", "Age", "City"],
        ["Alice", "30", "New York"],
        ["Bob", "25", "London"]
    ],
    "ChunkingConfiguration": {
        "Strategy": "Row"
    },
    "EmbeddingConfiguration": {
        "L2Normalization": false
    }
}
```

Each data row becomes a chunk of space-separated values: `"Alice 30 New York"`.

#### Table (RowGroupWithHeaders)
```json
{
    "Type": "Table",
    "Table": [
        ["Name", "Age", "City"],
        ["Alice", "30", "New York"],
        ["Bob", "25", "London"],
        ["Carol", "35", "Paris"]
    ],
    "ChunkingConfiguration": {
        "Strategy": "RowGroupWithHeaders",
        "RowGroupSize": 2
    },
    "EmbeddingConfiguration": {
        "L2Normalization": false
    }
}
```

Groups of `RowGroupSize` rows with headers prepended as a markdown table. Default `RowGroupSize` is 5.

#### Table (KeyValuePairs)
```json
{
    "Type": "Table",
    "Table": [
        ["Name", "Age", "City"],
        ["Alice", "30", "New York"],
        ["Bob", "25", "London"]
    ],
    "ChunkingConfiguration": {
        "Strategy": "KeyValuePairs"
    },
    "EmbeddingConfiguration": {
        "L2Normalization": false
    }
}
```

Each data row becomes: `"Name: Alice, Age: 30, City: New York"`.

#### Table (WholeTable)
```json
{
    "Type": "Table",
    "Table": [
        ["Name", "Age", "City"],
        ["Alice", "30", "New York"],
        ["Bob", "25", "London"]
    ],
    "ChunkingConfiguration": {
        "Strategy": "WholeTable"
    },
    "EmbeddingConfiguration": {
        "L2Normalization": false
    }
}
```

Entire table serialized as a single markdown table chunk.

#### Text with RegexBased (Markdown headings)
```json
{
    "Type": "Text",
    "Text": "# Introduction\nPartio is a chunking platform.\n\n# Architecture\nPartio uses a ...\n\n# Deployment\nUse Docker Compose to ...",
    "ChunkingConfiguration": {
        "Strategy": "RegexBased",
        "RegexPattern": "(?=^#{1,3}\\s)",
        "FixedTokenCount": 512
    },
    "EmbeddingConfiguration": {
        "L2Normalization": false
    }
}
```

Split at boundaries defined by the `RegexPattern`. Text is split using `Regex.Split` at every match. Useful for Markdown headings, log timestamps, LaTeX sections, function definitions, and more.

**Response**: `200 OK` — `SemanticCellResponse`
```json
{
    "Text": "Your text content here...",
    "Chunks": [
        {
            "Text": "Your text content here...",
            "Labels": ["label1"],
            "Tags": { "key": "value" },
            "Embeddings": [-0.4418, 0.1234, ...]
        }
    ]
}
```

**Errors**:
- `404 Not Found` — Endpoint ID not found or does not belong to the caller's tenant
- `400 Bad Request` — Endpoint is inactive, request body is missing/invalid, or strategy is incompatible with atom type

#### Strategy-to-Type Validation

The API validates that the chunking strategy is compatible with the atom type. Incompatible combinations return `400 Bad Request`.

- **Generic strategies** (`FixedTokenCount`, `SentenceBased`, `ParagraphBased`, `RegexBased`) work with all types
- **List strategies** (`WholeList`, `ListEntry`) only work with `List`
- **Table strategies** (`Row`, `RowWithHeaders`, `RowGroupWithHeaders`, `KeyValuePairs`, `WholeTable`) only work with `Table`

Example error response for missing `RegexPattern`:
```json
{
    "Error": "BadRequest",
    "Message": "RegexPattern is required when using the RegexBased strategy.",
    "StatusCode": 400
}
```

Example error response for invalid `RegexPattern`:
```json
{
    "Error": "BadRequest",
    "Message": "RegexPattern is not a valid regular expression: parsing '([' - Unterminated [] set.",
    "StatusCode": 400
}
```

Example error response for using `Row` strategy on a `Text` type:
```json
{
    "Error": "BadRequest",
    "Message": "Strategy 'Row' is only compatible with atom type 'Table', but got 'Text'.",
    "StatusCode": 400
}
```

#### ChunkingConfiguration Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Strategy` | string | `FixedTokenCount` | Chunking strategy to use |
| `FixedTokenCount` | int | `256` | Tokens per chunk (for FixedTokenCount) |
| `OverlapCount` | int | `0` | Overlap tokens between chunks |
| `OverlapPercentage` | float? | `null` | Overlap as percentage (0.0-1.0) |
| `OverlapStrategy` | string | `SlidingWindow` | Overlap boundary strategy |
| `ContextPrefix` | string? | `null` | Prefix prepended to each chunk |
| `RowGroupSize` | int | `5` | Rows per group (for RowGroupWithHeaders). Minimum: 1 |
| `RegexPattern` | string? | `null` | Regular expression split pattern (required for `RegexBased` strategy). Text is split at every match of this pattern. |

### POST /v1.0/endpoints/{id}/process/batch
Process multiple semantic cells using the specified embedding endpoint.

**Path Parameters**:
- `id` — Embedding endpoint ID

**Request Body**: `List<SemanticCellRequest>`

**Response**: `200 OK` — `List<SemanticCellResponse>`

---

## Tenants (Admin)

### PUT /v1.0/tenants
Create a tenant. Also creates a default user, credential, and embedding endpoints.

**Request Body**:
```json
{
    "Name": "My Tenant",
    "Labels": ["production"],
    "Tags": { "env": "prod" }
}
```

**Response**: `201 Created` — `TenantMetadata`

### GET /v1.0/tenants/{id}
Read a tenant by ID.

**Response**: `200 OK` — `TenantMetadata`

### PUT /v1.0/tenants/{id}
Update a tenant.

**Request Body**: `TenantMetadata` (partial update)

**Response**: `200 OK` — `TenantMetadata`

### DELETE /v1.0/tenants/{id}
Delete a tenant.

**Response**: `204 No Content`

### HEAD /v1.0/tenants/{id}
Check if a tenant exists.

**Response**: `200 OK` or `404 Not Found`

### POST /v1.0/tenants/enumerate
List tenants with pagination and filtering.

**Request Body**: `EnumerationRequest`
```json
{
    "MaxResults": 100,
    "ContinuationToken": null,
    "Order": "CreatedDescending",
    "NameFilter": null,
    "ActiveFilter": null
}
```

**Response**: `200 OK` — `EnumerationResult<TenantMetadata>`

---

## Users (Admin)

### PUT /v1.0/users
Create a user.

**Request Body**:
```json
{
    "TenantId": "ten_...",
    "Email": "user@example.com",
    "Password": "plaintext-password",
    "FirstName": "John",
    "LastName": "Doe",
    "IsAdmin": false
}
```

**Response**: `200 OK` — `UserMaster` (password redacted)

### GET /v1.0/users/{id}
Read a user by ID (password redacted).

**Response**: `200 OK` — `UserMaster`

### PUT /v1.0/users/{id}
Update a user.

**Response**: `200 OK` — `UserMaster`

### DELETE /v1.0/users/{id}
Delete a user.

**Response**: `204 No Content`

### HEAD /v1.0/users/{id}
Check if a user exists.

**Response**: `200 OK` or `404 Not Found`

### POST /v1.0/users/enumerate
List users with pagination.

**Request/Response**: Same pattern as tenants.

---

## Credentials (Admin)

### PUT /v1.0/credentials
Create a credential (generates a bearer token).

**Request Body**:
```json
{
    "TenantId": "ten_...",
    "UserId": "usr_...",
    "Name": "My API Key"
}
```

**Response**: `201 Created` — `Credential` (includes generated `BearerToken`)

### GET /v1.0/credentials/{id}
Read a credential.

**Response**: `200 OK` — `Credential`

### PUT /v1.0/credentials/{id}
Update a credential.

**Response**: `200 OK` — `Credential`

### DELETE /v1.0/credentials/{id}
Delete a credential.

**Response**: `204 No Content`

### HEAD /v1.0/credentials/{id}
Check if a credential exists.

**Response**: `200 OK` or `404 Not Found`

### POST /v1.0/credentials/enumerate
List credentials with pagination.

---

## Embedding Endpoints (Admin)

### PUT /v1.0/endpoints
Create an embedding endpoint.

**Request Body**:
```json
{
    "TenantId": "ten_...",
    "Model": "all-minilm",
    "Endpoint": "http://localhost:11434",
    "ApiFormat": "Ollama",
    "ApiKey": null,
    "Active": true,
    "EnableRequestHistory": true,
    "HealthCheckEnabled": false,
    "HealthCheckUrl": null,
    "HealthCheckMethod": "GET",
    "HealthCheckIntervalMs": 5000,
    "HealthCheckTimeoutMs": 2000,
    "HealthCheckExpectedStatusCode": 200,
    "HealthyThreshold": 3,
    "UnhealthyThreshold": 3,
    "HealthCheckUseAuth": false
}
```

#### Health Check Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `HealthCheckEnabled` | bool | `false` | Enable background health checking for this endpoint |
| `HealthCheckUrl` | string? | `null` | Custom URL to check (defaults to the endpoint URL if null) |
| `HealthCheckMethod` | string | `"GET"` | HTTP method for health checks (`GET` or `HEAD`) |
| `HealthCheckIntervalMs` | int | `5000` | Milliseconds between health checks |
| `HealthCheckTimeoutMs` | int | `2000` | Timeout per health check request in milliseconds |
| `HealthCheckExpectedStatusCode` | int | `200` | Expected HTTP status code for a healthy response |
| `HealthyThreshold` | int | `3` | Consecutive successes required to transition to healthy |
| `UnhealthyThreshold` | int | `3` | Consecutive failures required to transition to unhealthy |
| `HealthCheckUseAuth` | bool | `false` | Include the endpoint's API key as a Bearer token in health checks |

When `HealthCheckEnabled` is `true` and the endpoint is active, the server runs a background loop that periodically checks the endpoint. If the endpoint becomes unhealthy, process requests to it return `502 Bad Gateway`.

Health check defaults are applied automatically based on `ApiFormat` when creating or updating an endpoint:
- **Ollama**: URL defaults to `{Endpoint}/api/tags`, 10s interval, 5s timeout, no auth
- **OpenAI**: URL defaults to `{Endpoint}/v1/models`, 30s interval, 10s timeout, auth enabled

**Response**: `201 Created` — `EmbeddingEndpoint`

### GET /v1.0/endpoints/{id}
Read an embedding endpoint.

**Response**: `200 OK` — `EmbeddingEndpoint`

### PUT /v1.0/endpoints/{id}
Update an embedding endpoint.

**Response**: `200 OK` — `EmbeddingEndpoint`

### DELETE /v1.0/endpoints/{id}
Delete an embedding endpoint.

**Response**: `204 No Content`

### HEAD /v1.0/endpoints/{id}
Check if an endpoint exists.

**Response**: `200 OK` or `404 Not Found`

### POST /v1.0/endpoints/enumerate
List endpoints with pagination.

---

## Endpoint Health (Admin)

### GET /v1.0/endpoints/{id}/health
Get the health status for a specific monitored endpoint.

**Path Parameters**:
- `id` — Embedding endpoint ID

**Response**: `200 OK` — `EndpointHealthStatus`
```json
{
    "EndpointId": "ep_xxxx",
    "EndpointName": "all-minilm",
    "TenantId": "ten_xxxx",
    "IsHealthy": true,
    "FirstCheckUtc": "2026-02-07T12:00:00Z",
    "LastCheckUtc": "2026-02-07T12:01:00Z",
    "LastHealthyUtc": "2026-02-07T12:00:30Z",
    "LastUnhealthyUtc": null,
    "LastStateChangeUtc": "2026-02-07T12:00:30Z",
    "TotalUptimeMs": 60000,
    "TotalDowntimeMs": 30000,
    "UptimePercentage": 66.67,
    "ConsecutiveSuccesses": 3,
    "ConsecutiveFailures": 0,
    "LastError": null,
    "History": [
        { "TimestampUtc": "2026-02-07T12:00:10Z", "Success": false },
        { "TimestampUtc": "2026-02-07T12:00:20Z", "Success": true },
        { "TimestampUtc": "2026-02-07T12:00:30Z", "Success": true }
    ]
}
```

**Errors**:
- `404 Not Found` — No health state exists (health check not enabled or endpoint not found)

### GET /v1.0/endpoints/health
Get health status for all monitored endpoints. Non-admin callers see only their tenant's endpoints.

**Response**: `200 OK` — `List<EndpointHealthStatus>`

---

## Request History (Admin)

### GET /v1.0/requests/{id}
Read a request history entry.

**Response**: `200 OK` — `RequestHistoryEntry`

### GET /v1.0/requests/{id}/detail
Read request/response body detail from filesystem.

**Response**: `200 OK` — JSON object with the following fields:

| Field | Type | Description |
|-------|------|-------------|
| `RequestHeaders` | object? | Outer request headers (key-value pairs) |
| `RequestBody` | string? | Outer request body (may be truncated) |
| `ResponseHeaders` | object? | Outer response headers (key-value pairs) |
| `ResponseBody` | string? | Outer response body (may be truncated) |
| `EmbeddingCalls` | array? | Upstream embedding HTTP call details (present only for process requests) |

Each item in the `EmbeddingCalls` array:

| Field | Type | Description |
|-------|------|-------------|
| `Url` | string? | Full URL called on the upstream embedding endpoint |
| `Method` | string? | HTTP method (e.g. `POST`) |
| `RequestHeaders` | object? | Headers sent to the upstream endpoint |
| `RequestBody` | string? | Body sent to the upstream endpoint (may be truncated) |
| `StatusCode` | int? | HTTP status code returned by the upstream endpoint |
| `ResponseHeaders` | object? | Response headers from the upstream endpoint |
| `ResponseBody` | string? | Response body from the upstream endpoint (may be truncated) |
| `ResponseTimeMs` | long? | Round-trip time for this call in milliseconds |
| `Success` | bool | Whether the call returned a success status code |
| `Error` | string? | Error message if the call failed |
| `TimestampUtc` | string | ISO 8601 timestamp when the call was initiated |

### DELETE /v1.0/requests/{id}
Delete a request history entry.

**Response**: `204 No Content`

### POST /v1.0/requests/enumerate
List request history with pagination.

---

## Error Responses

All errors return an `ApiErrorResponse`:

```json
{
    "Error": "ArgumentException",
    "Message": "Request body is required.",
    "StatusCode": 400,
    "TimestampUtc": "2026-02-06T12:00:00Z"
}
```

| Status Code | Meaning |
|------------|---------|
| 400 | Bad Request (invalid input) |
| 401 | Unauthorized (missing/invalid token) |
| 404 | Not Found |
| 502 | Bad Gateway (endpoint is unhealthy) |
| 500 | Internal Server Error |

---

## Authentication

Include the bearer token in the `Authorization` header:

```
Authorization: Bearer partioadmin
```

- **Admin API keys** (from `partio.json` `AdminApiKeys` array) grant full admin access
- **Credential bearer tokens** grant tenant-scoped access for processing
- Health endpoints (`/`, `/v1.0/health`) do not require authentication

---

## Enumeration

All enumeration endpoints accept `POST` with an `EnumerationRequest` body and return `EnumerationResult<T>`.

Use `ContinuationToken` from the response to fetch the next page:

```json
// First page
POST /v1.0/tenants/enumerate
{ "MaxResults": 10 }

// Next page
POST /v1.0/tenants/enumerate
{ "MaxResults": 10, "ContinuationToken": "ten_abc123..." }
```

### Ordering

- `CreatedAscending` — oldest first
- `CreatedDescending` — newest first (default)
- `NameAscending` — alphabetical A-Z
- `NameDescending` — alphabetical Z-A

### Filtering

- `NameFilter` — partial match on name field
- `LabelFilter` — exact match on labels
- `TagKeyFilter` / `TagValueFilter` — filter by tag key/value
- `ActiveFilter` — filter by active status (true/false)
