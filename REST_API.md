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
- `400 Bad Request` — Endpoint is inactive, or request body is missing/invalid

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
    "ApiKey": null
}
```

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

## Request History (Admin)

### GET /v1.0/requests/{id}
Read a request history entry.

**Response**: `200 OK` — `RequestHistoryEntry`

### GET /v1.0/requests/{id}/detail
Read request/response body detail from filesystem.

**Response**: `200 OK` — JSON with `RequestBody` and `ResponseBody` fields

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
