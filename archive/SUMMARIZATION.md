# Partio v0.2.0 — Summarization Integration Plan

**Status:** Complete (All 12 Phases Done)
**Breaking Change:** Yes (v0.1.0 → v0.2.0)
**Date:** 2026-02-18

---

## Table of Contents

1. [Overview](#1-overview)
2. [Architecture](#2-architecture)
3. [Partio.Core — Models & Enums](#3-partiocore--models--enums)
4. [Partio.Core — Completion Clients](#4-partiocore--completion-clients)
5. [Partio.Core — Summarization Engine](#5-partiocore--summarization-engine)
6. [Partio.Core — Database Layer](#6-partiocore--database-layer)
7. [Partio.Server — Endpoint Routes](#7-partioserver--endpoint-routes)
8. [Partio.Server — Processing Pipeline](#8-partioserver--processing-pipeline)
9. [Dashboard](#9-dashboard)
10. [C# SDK](#10-c-sdk)
11. [Python SDK](#11-python-sdk)
12. [JavaScript SDK](#12-javascript-sdk)
13. [SDK Test Harnesses](#13-sdk-test-harnesses)
14. [Postman Collection](#14-postman-collection)
15. [REST_API.md](#15-rest_apimd)
16. [README.md](#16-readmemd)
17. [CHANGELOG.md](#17-changelogmd)
18. [Marketing Website](#18-marketing-website)
19. [Automated Tests](#19-automated-tests)
20. [Task Checklist](#20-task-checklist)

---

## 1. Overview

### What

Add **summarization** as an optional processing step in Partio's pipeline, occurring **before** chunking and embedding. When a `SummarizationConfiguration` is present on a request, Partio calls completion/generation APIs against the specified inference endpoint to produce summary child cells, then chunks and embeds everything (originals + summaries) together.

### Pipeline Change

**v0.1.0:** `Upload cells → Chunk → Embed`
**v0.2.0:** `Upload cells → (Summarize if configured) → Chunk → Embed`

### Key Design Decisions

| Decision | Resolution |
|----------|------------|
| Completion endpoint | New persisted resource type (`CompletionEndpoint`) with API type, base URL, optional bearer token |
| Cell hierarchy | `SemanticCellRequest` gains `Children` (recursive) and `ParentGUID`, modeled after DocumentAtom's Atom/Quarks pattern |
| SummarizationConfiguration | Inline on the request (same pattern as `ChunkingConfiguration` and `EmbeddingConfiguration`), per-cell |
| Summary cell treatment | Summaries are injected into the hierarchy as child cells, then chunked and embedded alongside originals |
| Pre-summarized cells | If a cell has `Type = Summary`, skip summarization for that cell (client may pre-summarize) |
| Endpoint scope | Both `/v1.0/process` (single) and `/v1.0/process/batch` support summarization |
| Route restructure | Embedding endpoints move from `/v1.0/endpoints` to `/v1.0/endpoints/embedding`; completion endpoints at `/v1.0/endpoints/completion`; processing moves to `/v1.0/process` (embedding endpoint ID moves into `EmbeddingConfiguration`) |
| Input flexibility | Clients may send cells as flat (using `ParentGUID`), hierarchical (using `Children`), or genuinely flat; backend normalizes via deflatten/flatten helpers (ported from View's `SemanticCell.Flatten`/`SemanticCell.Deflatten`) |
| Retry semantics | `MaxRetriesPerSummary` = max attempts per individual cell; `MaxRetries` = global failure counter across all cells. When global limit is hit, the entire operation fails |
| Pipeline failure | If any pipeline step (summarization, chunking, embedding) exhausts retries, the entire request fails with an error indicating which step failed |
| Backward compatibility | Breaking change — not a concern for v0.2.0 |

### Reference Implementations

| Source | Location |
|--------|----------|
| View.Summarization | `C:\code\view\backend\core\csharp\View.Summarization\ViewSummarizer.cs` |
| View.Models SummarizationRule | `C:\code\view\backend\core\csharp\View.Models\SummarizationRule.cs` |
| View.Models SemanticCell (Flatten/Deflatten) | `C:\code\view\Backend\core\csharp\View.Models\SemanticCell.cs` (lines 596-697) |
| DocumentAtom Atom hierarchy | `C:\code\documentatom\src\DocumentAtom.Core\Atoms\Atom.cs` |
| Partio EmbeddingEndpoint (health check defaults) | `src/Partio.Core/Models/EmbeddingEndpoint.cs` (ApplyHealthCheckDefaults) |

---

## 2. Architecture

### New Processing Pipeline (Detailed)

```
Client sends SemanticCellRequest to POST /v1.0/process
│
├─ 1. Validate request
│     ├─ Strategy/type compatibility (existing)
│     ├─ Resolve EmbeddingEndpoint from DB using EmbeddingConfiguration.EmbeddingEndpointId
│     └─ If SummarizationConfiguration present:
│           ├─ Validate CompletionEndpointId references a valid, active endpoint
│           └─ Validate summarization parameters
│
├─ 2. NORMALIZE HIERARCHY (new step)
│     ├─ Accept input in any form: flat, flat-with-ParentGUID, or hierarchical (Children)
│     ├─ Deflatten: build tree using ParentGUID relationships (ported from View's SemanticCell.Deflatten)
│     └─ Result: normalized hierarchical cell tree
│
├─ 3. SUMMARIZE (new step, only if SummarizationConfiguration is present)
│     ├─ Resolve CompletionEndpoint from database
│     ├─ Skip cells with Type = Summary (pre-summarized by client)
│     ├─ Depending on Order (TopDown or BottomUp):
│     │     ├─ TopDown: process from root → leaves, using parent context
│     │     └─ BottomUp: process from leaves → root, using child context
│     ├─ For each eligible cell (content length >= MinCellLength, Type != Summary):
│     │     ├─ Build prompt from SummarizationPrompt template
│     │     ├─ Call completion endpoint (Ollama /api/generate or OpenAI /v1/chat/completions)
│     │     ├─ If valid summary returned (not "None", not empty):
│     │     │     └─ Create summary child cell (Type = Summary) attached to parent
│     │     └─ If null/empty/"None" → skip, no child created
│     ├─ Retry: per-cell retries (MaxRetriesPerSummary) + global failure counter (MaxRetries)
│     ├─ If global retry limit exceeded → FAIL entire request with error
│     └─ All cells proceed to chunking
│
├─ 4. CHUNK (existing, now processes all cells including summaries)
│     └─ Each cell → ChunkingEngine.Chunk() → List<ChunkResult>
│     └─ If chunking fails beyond retries → FAIL entire request with error
│
├─ 5. EMBED (existing, now embeds all chunks including summary chunks)
│     └─ EmbedBatchAsync() → attach embedding vectors
│     └─ If embedding fails beyond retries → FAIL entire request with error
│
└─ 6. Return SemanticCellResponse (now hierarchical with summaries included)
```

### Route Restructure (Breaking Change)

The v0.1.0 routes are restructured in v0.2.0:

```
v0.1.0                                    v0.2.0
──────────────────────────────────────    ──────────────────────────────────────────
/v1.0/endpoints                       →  /v1.0/endpoints/embedding
/v1.0/endpoints/{id}                  →  /v1.0/endpoints/embedding/{id}
/v1.0/endpoints/enumerate             →  /v1.0/endpoints/embedding/enumerate
/v1.0/endpoints/health                →  /v1.0/endpoints/embedding/health
/v1.0/endpoints/{id}/health           →  /v1.0/endpoints/embedding/{id}/health
/v1.0/endpoints/{id}/process          →  /v1.0/process          (embedding endpoint ID moves to EmbeddingConfiguration)
/v1.0/endpoints/{id}/process/batch    →  /v1.0/process/batch    (embedding endpoint ID moves to EmbeddingConfiguration)
(none)                                →  /v1.0/endpoints/completion
(none)                                →  /v1.0/endpoints/completion/{id}
(none)                                →  /v1.0/endpoints/completion/enumerate
(none)                                →  /v1.0/endpoints/completion/health
(none)                                →  /v1.0/endpoints/completion/{id}/health
```

### New Resource: CompletionEndpoint

Parallel to the existing `EmbeddingEndpoint`, a new `CompletionEndpoint` resource provides the LLM completion API configuration for summarization. This is a persisted, CRUD-managed entity (like tenants, users, credentials, embedding endpoints).

```
Partio Server
├── EmbeddingEndpoint  (existing - for /api/embed, /v1/embeddings)
└── CompletionEndpoint (new - for /api/generate, /v1/chat/completions)
```

### Dashboard Navigation Change

The current dashboard has a single "Endpoints" navigation item for embedding endpoints. In v0.2.0, this is restructured into two sub-sections under "Endpoints":

```
Endpoints (nav item)
├── Embeddings   (existing EmbeddingEndpointsView, renamed/relocated)
└── Inference    (new CompletionEndpointsView)
```

Both sub-sections have full parity: CRUD management, health checks, health status indicators, and health histograms. Inference endpoints receive the same health check infrastructure as embedding endpoints (periodic probes, healthy/unhealthy thresholds, status tracking, health check history).

---

## 3. Partio.Core — Models & Enums

### 3.1 New Enum: `SummarizationOrderEnum`

- [ ] **File:** `src/Partio.Core/Enums/SummarizationOrderEnum.cs`
- [ ] **Values:** `TopDown`, `BottomUp`
- [ ] Follow existing enum pattern (see `ChunkStrategyEnum.cs`)

### 3.2 Extend Enum: `AtomTypeEnum`

- [ ] **File:** `src/Partio.Core/Enums/AtomTypeEnum.cs`
- [ ] **Add:** `Summary` value (e.g., `Summary = 9`)
- [ ] This identifies cells created by the summarization engine

### 3.3 New Model: `SummarizationConfiguration`

- [ ] **File:** `src/Partio.Core/Models/SummarizationConfiguration.cs`
- [ ] Inline configuration on the request, analogous to `ChunkingConfiguration`
- [ ] Properties (derived from View's `SummarizationRule`, stripped of persistence fields):

```
Property                  Type                       Default    Validation
────────────────────────  ─────────────────────────  ─────────  ──────────────────────
CompletionEndpointId      string                     (required) Must reference valid endpoint
Order                     SummarizationOrderEnum     BottomUp   TopDown or BottomUp
SummarizationPrompt       string                     (default)  Non-empty; supports {tokens}, {content}, {context}
MaxSummaryTokens          int                        1024       Min: 128
MinCellLength             int                        128        Min: 0
MaxParallelTasks          int                        4          Min: 1
MaxRetriesPerSummary      int                        3          Min: 0; max attempts per individual cell
MaxRetries                int                        9          Min: 0; global failure counter across all cells — when reached, entire operation fails
TimeoutMs                 int                        30000      Min: 100
```

- [ ] **Retry semantics:**
  - `MaxRetriesPerSummary`: how many times to retry summarization for a single cell before giving up on that cell
  - `MaxRetries`: global failure counter incremented each time any cell fails. When this limit is reached, the entire summarization step fails and the request returns an error indicating summarization failure. This acts as a circuit breaker to prevent runaway failures.
  - Thread-safe via `Interlocked.Increment` on a shared global counter

- [ ] Default prompt (from View):
```
"You must follow these rules exactly:\n"
"1. If the content cannot be summarized (empty, insufficient, non-text, or meaningless), output exactly: None\n"
"2. If the content can be summarized, output ONLY the summary text\n"
"3. Never include prefixes like 'Summary:', 'Summary text:', or any introductory phrases\n"
"4. Never explain why you cannot summarize, in these cases just output exactly: None\n"
"5. Maximum length: {tokens} tokens\n\n"
"Content to summarize:\n{content}\n\n"
"Context information:\n{context}\n\n"
"Output:"
```

### 3.4 New Model: `CompletionEndpoint`

- [ ] **File:** `src/Partio.Core/Models/CompletionEndpoint.cs`
- [ ] Persisted resource, modeled after `EmbeddingEndpoint` with full health check support
- [ ] Properties:

```
Property                    Type                          Default           Notes
──────────────────────────  ────────────────────────────  ────────────────  ────────────────────────────
Id                          string                        Auto-generated    Prefix: "cep_" (completion endpoint)
TenantId                    string                        (required)        Tenant association
Name                        string                        null              Human-readable name
Endpoint                    string                        (required)        Base URL (e.g., http://ollama:11434)
ApiFormat                   ApiFormatEnum                 Ollama            Ollama or OpenAI
ApiKey                      string?                       null              Bearer token (optional, required for OpenAI)
Model                       string                        (required)        Model name (e.g., "llama3", "gpt-4o")
Active                      bool                          true              Enable/disable
EnableRequestHistory        bool                          true              Record request history for this endpoint
Labels                      List<string>?                 null              User labels
Tags                        Dictionary<string, string>?   null              User tags
HealthCheckEnabled          bool                          true              Enable periodic health probing
HealthCheckUrl              string?                       null              Custom health check URL (defaults per ApiFormat)
HealthCheckMethod           HealthCheckMethodEnum         GET               GET or HEAD
HealthCheckIntervalMs       int                           30000             Probe interval in ms
HealthCheckTimeoutMs        int                           5000              Probe timeout in ms
HealthCheckExpectedStatus   int                           200               Expected HTTP status code
HealthyThreshold            int                           3                 Consecutive successes to mark healthy
UnhealthyThreshold          int                           3                 Consecutive failures to mark unhealthy
HealthCheckUseAuth          bool                          false             Send bearer token with health checks
CreatedUtc                  DateTime                      Now               Creation timestamp
LastUpdateUtc               DateTime                      Now               Last update timestamp
```

- [ ] Include `ApplyHealthCheckDefaults()` static method mirroring `EmbeddingEndpoint.ApplyHealthCheckDefaults()`, with the same defaults per `ApiFormat`:
  - **Ollama:** HealthCheckUrl = `{baseUrl}/api/tags`, IntervalMs = 5000, TimeoutMs = 2000
  - **OpenAI:** HealthCheckUrl = `{baseUrl}/v1/models`, IntervalMs = 30000, TimeoutMs = 10000
  - Both: ExpectedStatusCode = 200, HealthyThreshold = 2, UnhealthyThreshold = 2
  - OpenAI: default HealthCheckUseAuth = true if ApiKey is present

### 3.5 Update Model: `EmbeddingConfiguration`

- [ ] **File:** `src/Partio.Core/Models/EmbeddingConfiguration.cs`
- [ ] **Add property:**

```csharp
/// Embedding endpoint ID (required — previously came from URL path).
public string EmbeddingEndpointId { get; set; }
```

- [ ] This replaces the `{id}` URL path parameter from the old `/v1.0/endpoints/{id}/process` route
- [ ] Required field — validation must ensure it references a valid, active embedding endpoint

### 3.6 Update Model: `SemanticCellRequest`

- [ ] **File:** `src/Partio.Core/Models/SemanticCellRequest.cs`
- [ ] **Add properties:**

```csharp
/// Unique identifier for this cell (auto-generated if not supplied).
public Guid GUID { get; set; } = Guid.NewGuid();

/// Parent cell GUID (null for root-level cells).
public Guid? ParentGUID { get; set; } = null;

/// Child cells forming a hierarchy.
public List<SemanticCellRequest>? Children { get; set; } = null;

/// Summarization configuration (null = skip summarization).
public SummarizationConfiguration? SummarizationConfiguration { get; set; } = null;
```

- [ ] The `Children` property enables recursive hierarchical cells, following DocumentAtom's `Quarks` pattern
- [ ] `SummarizationConfiguration` is nullable — when null, the summarization step is skipped entirely
- [ ] **Input flexibility:** Clients may submit cells in any of these forms:
  1. **Genuinely flat:** No ParentGUID, no Children — each cell is independent
  2. **Flat with ParentGUID:** Cells use ParentGUID to define relationships; backend calls `Deflatten()` to build the tree
  3. **Hierarchical:** Cells use Children to define the tree directly
  4. **Mixed:** Some cells use ParentGUID, some use Children — backend normalizes via deflatten
- [ ] Backend always normalizes to hierarchical form before processing, then flattens back for output if needed

### 3.7 Update Model: `SemanticCellResponse`

- [ ] **File:** `src/Partio.Core/Models/SemanticCellResponse.cs`
- [ ] **Add properties:**

```csharp
/// Unique identifier for this cell.
public Guid GUID { get; set; }

/// Parent cell GUID (null for root-level cells).
public Guid? ParentGUID { get; set; } = null;

/// Cell type (Text, Summary, etc.).
public AtomTypeEnum Type { get; set; }

/// Child cell responses forming a hierarchy.
public List<SemanticCellResponse>? Children { get; set; } = null;
```

- [ ] The response now mirrors the hierarchy, including injected summary cells
- [ ] Each child (including summary cells) has its own `Text`, `Chunks`, `Type`, etc.

### 3.8 Update Model: `ChunkResult`

- [ ] **File:** `src/Partio.Core/Models/ChunkResult.cs`
- [ ] **Add property:**

```csharp
/// The GUID of the SemanticCellRequest that produced this chunk.
public Guid CellGUID { get; set; }
```

- [ ] This allows consumers to trace which cell (original or summary) a chunk came from

### 3.9 Update: `Constants.cs`

- [ ] **File:** `src/Partio.Core/Constants.cs`
- [ ] **Change:** `Version` from `"0.1.0"` to `"0.2.0"`
- [ ] **Add:** `CompletionEndpointIdPrefix = "cep_"`

### 3.10 Update: `IdGenerator.cs`

- [ ] **File:** `src/Partio.Core/IdGenerator.cs`
- [ ] **Add method:**

```csharp
/// Generate a new completion endpoint ID with prefix 'cep_'.
public static string NewCompletionEndpointId()
{
    return _Generator.Generate(Constants.CompletionEndpointIdPrefix, 48);
}
```

---

## 4. Partio.Core — Completion Clients

New HTTP clients for calling completion/generation APIs, parallel to the existing embedding clients.

### 4.1 New Abstract Base: `CompletionClientBase`

- [ ] **File:** `src/Partio.Core/ThirdParty/CompletionClientBase.cs`
- [ ] Abstract class mirroring `EmbeddingClientBase` pattern
- [ ] Key method:

```csharp
public abstract Task<string?> GenerateCompletionAsync(
    string prompt,
    string model,
    int maxTokens,
    int timeoutMs,
    CancellationToken token = default);
```

- [ ] Returns the completion text, or null on failure
- [ ] Records `CompletionCallDetail` for request history auditing

### 4.2 New Model: `CompletionCallDetail`

- [ ] **File:** `src/Partio.Core/Models/CompletionCallDetail.cs`
- [ ] Mirrors `EmbeddingCallDetail` pattern for auditing completion API calls
- [ ] Properties: Url, Method, RequestHeaders, RequestBody, ResponseHeaders, ResponseBody, StatusCode, ResponseTimeMs, Timestamp, ErrorMessage

### 4.3 New: `OllamaCompletionClient`

- [ ] **File:** `src/Partio.Core/ThirdParty/OllamaCompletionClient.cs`
- [ ] Calls Ollama `/api/generate` endpoint
- [ ] Request body: `{ "model": "...", "prompt": "...", "stream": false, "options": { "num_predict": maxTokens } }`
- [ ] Parses `response` field from JSON result
- [ ] Handles timeouts, records call details

### 4.4 New: `OpenAiCompletionClient`

- [ ] **File:** `src/Partio.Core/ThirdParty/OpenAiCompletionClient.cs`
- [ ] Calls OpenAI-compatible `/v1/chat/completions` endpoint
- [ ] Request body: `{ "model": "...", "messages": [{"role": "user", "content": "..."}], "max_tokens": ... }`
- [ ] Parses `choices[0].message.content` from JSON result
- [ ] Bearer token auth, handles timeouts, records call details

---

## 5. Partio.Core — Summarization Engine

### 5.1 New: `SummarizationEngine`

- [ ] **File:** `src/Partio.Core/Summarization/SummarizationEngine.cs`
- [ ] Ported from View's `ViewSummarizer`, adapted to Partio's model types
- [ ] Key public method:

```csharp
public async Task<List<SemanticCellRequest>> SummarizeAsync(
    List<SemanticCellRequest> cells,
    SummarizationConfiguration config,
    CompletionClientBase completionClient,
    CancellationToken token = default);
```

- [ ] Returns the same cells with summary children injected

### 5.2 Internal: Hierarchy Helpers

Ported from View's `SemanticCell.Flatten()` and `SemanticCell.Deflatten()` (see `C:\code\view\Backend\core\csharp\View.Models\SemanticCell.cs` lines 596-697).

- [ ] **Deflatten:** Convert flat cell list to tree using `ParentGUID` / `Children`. Uses a `Dictionary<Guid, SemanticCellRequest>` lookup table. Returns only root-level cells (those without parents in the collection). Handles mixed input where some cells use Children and some use ParentGUID.
- [ ] **Flatten:** Convert tree back to flat list (all cells including new summary children). Recursively walks the tree collecting all cells. Optionally clones to preserve the original hierarchy.
- [ ] **GetCellsByDepthLevel:** Organize cells into `Dictionary<int, List<SemanticCellRequest>>`
- [ ] **FindParentCell:** Recursive search by GUID
- [ ] **GetCellContent:** Extract text from cell regardless of type (Text, List, Table, Binary)

### 5.3 Internal: Bottom-Up Processing

- [ ] Process cells from deepest level to root
- [ ] At each level, process cells in parallel (up to `MaxParallelTasks` via `SemaphoreSlim`)
- [ ] **Skip cells with `Type = Summary`** (pre-summarized by client)
- [ ] For each eligible cell (content >= `MinCellLength`, Type != Summary):
  - Collect child content and child summaries as context
  - Build prompt via template substitution (`{tokens}`, `{content}`, `{context}`)
  - Call `CompletionClientBase.GenerateCompletionAsync()`
  - If valid response: create summary child cell with `Type = Summary`
  - Attach as child of the current cell
- [ ] Retry logic: per-summary retries (`MaxRetriesPerSummary`) and global retries (`MaxRetries`), thread-safe via `Interlocked`
- [ ] If global `MaxRetries` exceeded: throw exception, fail entire request

### 5.4 Internal: Top-Down Processing

- [ ] Process cells from root to deepest level
- [ ] Same parallel processing pattern as bottom-up
- [ ] **Skip cells with `Type = Summary`** (pre-summarized by client)
- [ ] Context differs: uses **parent** content and parent summaries instead of children
- [ ] Same prompt building, completion calling, and retry logic
- [ ] Same global failure counter behavior

### 5.5 Summary Cell Creation

- [ ] New child cell properties:
  - `GUID`: new `Guid.NewGuid()`
  - `ParentGUID`: set to parent cell's GUID
  - `Type`: `AtomTypeEnum.Summary`
  - `Text`: the generated summary text
  - All other content fields null
- [ ] Cell is added to parent's `Children` list

---

## 6. Partio.Core — Database Layer

New CRUD operations for `CompletionEndpoint`, following the existing pattern for `EmbeddingEndpoint`.

### 6.1 Update: `DatabaseDriverBase`

- [ ] **File:** `src/Partio.Core/Database/DatabaseDriverBase.cs`
- [ ] **Add abstract methods:**
  - `CreateCompletionEndpoint(CompletionEndpoint)`
  - `ReadCompletionEndpoint(tenantId, endpointId)`
  - `UpdateCompletionEndpoint(CompletionEndpoint)`
  - `DeleteCompletionEndpoint(tenantId, endpointId)`
  - `ExistsCompletionEndpoint(tenantId, endpointId)`
  - `EnumerateCompletionEndpoints(tenantId, EnumerationRequest)`

### 6.2 Update: SQLite Implementation

- [ ] **File:** `src/Partio.Core/Database/Sqlite/Implementations/CompletionEndpoints.cs` (new)
- [ ] **Setup:** Add `completionendpoints` table creation to SQLite setup queries
- [ ] Table schema mirrors `embeddingendpoints` including health check columns
- [ ] Columns: `id`, `tenantid`, `name`, `endpoint`, `apiformat`, `apikey`, `model`, `active`, `enablerequesthistory`, `labels`, `tags`, `healthcheckenabled`, `healthcheckurl`, `healthcheckmethod`, `healthcheckintervalms`, `healthchecktimeoutms`, `healthcheckexpectedstatus`, `healthythreshold`, `unhealthythreshold`, `healthcheckuseauth`, `createdutc`, `lastupdateutc`

### 6.3 Update: PostgreSQL Implementation

- [ ] **File:** `src/Partio.Core/Database/Postgresql/Implementations/CompletionEndpoints.cs` (new)
- [ ] **Setup:** Add table creation to PostgreSQL setup queries
- [ ] Same schema as SQLite, adapted for PostgreSQL types

### 6.4 Update: MySQL Implementation

- [ ] **File:** `src/Partio.Core/Database/Mysql/Implementations/CompletionEndpoints.cs` (new)
- [ ] **Setup:** Add table creation to MySQL setup queries

### 6.5 Update: SQL Server Implementation

- [ ] **File:** `src/Partio.Core/Database/SqlServer/Implementations/CompletionEndpoints.cs` (new)
- [ ] **Setup:** Add table creation to SQL Server setup queries

---

## 7. Partio.Server — Endpoint Routes

### 7.1 Rename Existing Embedding Endpoint Routes

- [ ] **File:** `src/Partio.Server/PartioServer.cs`
- [ ] **Rename** all existing embedding endpoint routes from `/v1.0/endpoints` to `/v1.0/endpoints/embedding`:

```
v0.1.0                                  v0.2.0
PUT    /v1.0/endpoints              →  PUT    /v1.0/endpoints/embedding
GET    /v1.0/endpoints/{id}         →  GET    /v1.0/endpoints/embedding/{id}
PUT    /v1.0/endpoints/{id}         →  PUT    /v1.0/endpoints/embedding/{id}
DELETE /v1.0/endpoints/{id}         →  DELETE /v1.0/endpoints/embedding/{id}
HEAD   /v1.0/endpoints/{id}         →  HEAD   /v1.0/endpoints/embedding/{id}
POST   /v1.0/endpoints/enumerate    →  POST   /v1.0/endpoints/embedding/enumerate
GET    /v1.0/endpoints/health       →  GET    /v1.0/endpoints/embedding/health
GET    /v1.0/endpoints/{id}/health  →  GET    /v1.0/endpoints/embedding/{id}/health
```

### 7.2 New Completion Endpoint Routes

- [ ] **File:** `src/Partio.Server/PartioServer.cs`
- [ ] Add routes:

```
PUT    /v1.0/endpoints/completion              → Create completion endpoint
GET    /v1.0/endpoints/completion/{id}         → Read single completion endpoint
PUT    /v1.0/endpoints/completion/{id}         → Update completion endpoint
DELETE /v1.0/endpoints/completion/{id}         → Delete completion endpoint
HEAD   /v1.0/endpoints/completion/{id}         → Check if completion endpoint exists
POST   /v1.0/endpoints/completion/enumerate    → Enumerate with pagination
GET    /v1.0/endpoints/completion/health       → Health status for all completion endpoints
GET    /v1.0/endpoints/completion/{id}/health  → Health status for single completion endpoint
```

### 7.3 Update Processing Routes

- [ ] **File:** `src/Partio.Server/PartioServer.cs`
- [ ] **Move** processing routes from `/v1.0/endpoints/{id}/process` to `/v1.0/process`:

```
v0.1.0                                         v0.2.0
POST /v1.0/endpoints/{id}/process          →  POST /v1.0/process
POST /v1.0/endpoints/{id}/process/batch    →  POST /v1.0/process/batch
```

- [ ] The embedding endpoint ID is now resolved from `EmbeddingConfiguration.EmbeddingEndpointId` in the request body instead of the URL path

### 7.4 Route Handlers

- [ ] Follow the exact same pattern as the existing embedding endpoint handlers
- [ ] Authentication required (bearer token)
- [ ] Tenant scoping for non-admin callers
- [ ] Standard error responses (400, 401, 404, 500)

### 7.5 Completion Endpoint Health Check Service

- [ ] New `CompletionHealthCheckService` (or extend existing `EmbeddingHealthCheckService` to be generic)
- [ ] Periodic health probes against completion endpoints, same logic as embedding health checks
- [ ] Threshold-based healthy/unhealthy state transitions
- [ ] Health check history recording
- [ ] Returns 502 Bad Gateway if a completion endpoint is unhealthy when used for summarization

---

## 8. Partio.Server — Processing Pipeline

### 8.1 Update: `ProcessCellAsync`

- [ ] **File:** `src/Partio.Server/PartioServer.cs`
- [ ] **Modify** the processing flow to insert summarization before chunking:

```
ProcessCellAsync (updated):
  1. Resolve EmbeddingEndpoint from DB using EmbeddingConfiguration.EmbeddingEndpointId (moved from URL path)
  2. Validate request (existing)
  3. Normalize hierarchy: deflatten cells (handles flat, ParentGUID-based, or pre-hierarchical input)
  4. NEW: If SummarizationConfiguration is present:
     a. Resolve CompletionEndpoint from DB using CompletionEndpointId
     b. Validate endpoint is active (if unhealthy → 502 Bad Gateway)
     c. Create CompletionClient (Ollama or OpenAI based on ApiFormat)
     d. Call SummarizationEngine.SummarizeAsync()
     e. If MaxRetries exceeded → fail with error "Summarization failed: global retry limit exceeded"
     f. Result: cells now include summary children
  5. Chunk all cells (existing, but now iterates hierarchy including summaries)
     → If failure → fail with error "Chunking failed: ..."
  6. Embed all chunks (existing, but now includes summary chunks)
     → If failure → fail with error "Embedding failed: ..."
  7. Build hierarchical SemanticCellResponse
```

### 8.2 Update: Hierarchical Processing

- [ ] The chunking and embedding steps must now traverse the cell hierarchy recursively
- [ ] For each cell in the tree (depth-first):
  - Chunk its content → `List<ChunkResult>`
  - Embed the chunks
  - Build a `SemanticCellResponse` node
  - Recurse into `Children`
- [ ] Summary cells (Type = Summary) are processed identically to original cells

### 8.3 Update: Batch Processing

- [ ] The `/process/batch` endpoint receives `List<SemanticCellRequest>`
- [ ] Each request in the batch may independently have a `SummarizationConfiguration`
- [ ] Processing is per-request (each request is a self-contained hierarchy)

### 8.4 Update: Request History

- [ ] Record completion API calls (via `CompletionCallDetail`) alongside embedding API calls
- [ ] Add completion call details to the request history entry

---

## 9. Dashboard

### 9.1 Restructure: Endpoints Navigation

- [ ] **File:** `dashboard/src/views/Workspace.jsx`
- [ ] Replace the single "Endpoints" nav item with an "Endpoints" group containing two sub-items:
  - **Embeddings** — the existing `EmbeddingEndpointsView` (relocated under Endpoints)
  - **Inference** — the new `CompletionEndpointsView`
- [ ] Both sub-items are always visible in the navigation

### 9.2 New View: `CompletionEndpointsView` (Inference)

- [ ] **File:** `dashboard/src/views/CompletionEndpointsView.jsx` (new)
- [ ] CRUD management for completion endpoints (inference endpoints)
- [ ] Follow the pattern of `EmbeddingEndpointsView.jsx` with full feature parity:
  - Fields: Name, Endpoint URL, API Format (Ollama/OpenAI), API Key, Model, Active
  - DataTable with enumerate/create/edit/delete
  - **Health check configuration:** HealthCheckEnabled, HealthCheckUrl, HealthCheckMethod, HealthCheckIntervalMs, HealthCheckTimeoutMs, HealthCheckExpectedStatus, HealthyThreshold, UnhealthyThreshold, HealthCheckUseAuth
  - **Health status indicators:** healthy/unhealthy/unknown badge per endpoint
  - **Health histogram:** visual health check history (same component/pattern as embedding endpoints)

### 9.3 Update: `App.jsx`

- [ ] **File:** `dashboard/src/App.jsx`
- [ ] Update routes to reflect the new structure:
  - `endpoints/embeddings` → `EmbeddingEndpointsView`
  - `endpoints/inference` → `CompletionEndpointsView`
  - Consider redirect from old `endpoints` route for convenience

### 9.4 Update: `ChunkEmbedView.jsx`

- [ ] **File:** `dashboard/src/components/ChunkEmbedView.jsx`
- [ ] Rename or supplement to reflect summarization capability (e.g., update title/heading)
- [ ] **Add summarization configuration UI:**
  - Toggle: "Enable Summarization" checkbox
  - When enabled, show:
    - Completion Endpoint dropdown (populated from `/v1.0/endpoints/completion/enumerate`)
    - Order: TopDown / BottomUp radio buttons
    - Max Summary Tokens: number input (default 1024)
    - Min Cell Length: number input (default 128)
    - Max Parallel Tasks: number input (default 4)
    - Custom Prompt: expandable textarea (with default shown)
- [ ] **Add hierarchy input support:**
  - Allow defining parent-child cell relationships in the UI
  - This could be a tree editor or a simpler "add child cell" button approach
- [ ] **Update results display:**
  - Show hierarchical results (tree view)
  - Distinguish summary cells visually (e.g., different background color, "Summary" badge)
  - Show which chunks came from summary cells vs original cells

### 9.5 Update: `api.js`

- [ ] **File:** `dashboard/src/utils/api.js`
- [ ] **Add methods:**

```javascript
// Completion Endpoints
createCompletionEndpoint(data)          // PUT /v1.0/endpoints/completion
getCompletionEndpoint(id)              // GET /v1.0/endpoints/completion/{id}
updateCompletionEndpoint(id, data)     // PUT /v1.0/endpoints/completion/{id}
deleteCompletionEndpoint(id)           // DELETE /v1.0/endpoints/completion/{id}
enumerateCompletionEndpoints(req)      // POST /v1.0/endpoints/completion/enumerate

// Completion Endpoint Health
getCompletionEndpointHealth(id)        // GET /v1.0/endpoints/completion/{id}/health
getAllCompletionEndpointHealth()        // GET /v1.0/endpoints/completion/health

// Update existing embedding endpoint methods to use new paths:
// /v1.0/endpoints → /v1.0/endpoints/embedding (all embedding endpoint methods)
// /v1.0/endpoints/{id}/process → /v1.0/process (processing methods)
```

---

## 10. C# SDK

### 10.1 New Model Classes

- [ ] **File:** `sdk/csharp/Models/SummarizationConfiguration.cs`
  - Same properties as Partio.Core version
- [ ] **File:** `sdk/csharp/Models/SummarizationOrderEnum.cs`
- [ ] **File:** `sdk/csharp/Models/CompletionEndpoint.cs`
  - Client-side representation of the completion endpoint resource

### 10.2 Update Existing Models

- [ ] **File:** `sdk/csharp/Models/EmbeddingConfiguration.cs`
  - Add: `EmbeddingEndpointId`
- [ ] **File:** `sdk/csharp/Models/SemanticCellRequest.cs`
  - Add: `GUID`, `ParentGUID`, `Children`, `SummarizationConfiguration`
- [ ] **File:** `sdk/csharp/Models/SemanticCellResponse.cs`
  - Add: `GUID`, `ParentGUID`, `Type`, `Children`
- [ ] **File:** `sdk/csharp/Models/ChunkResult.cs`
  - Add: `CellGUID`
- [ ] **File:** `sdk/csharp/Models/AtomTypeEnum.cs`
  - Add: `Summary`

### 10.3 Update Client

- [ ] **File:** `sdk/csharp/PartioClient.cs`
- [ ] **Update existing embedding endpoint methods** to use `/v1.0/endpoints/embedding` routes
- [ ] **Update existing process methods** to use `/v1.0/process` routes (no longer pass endpoint ID in URL)
- [ ] **Add methods:**

```csharp
// Completion Endpoints (at /v1.0/endpoints/completion)
Task<CompletionEndpoint> CreateCompletionEndpointAsync(CompletionEndpoint endpoint)
Task<CompletionEndpoint> ReadCompletionEndpointAsync(string id)
Task<CompletionEndpoint> UpdateCompletionEndpointAsync(CompletionEndpoint endpoint)
Task DeleteCompletionEndpointAsync(string id)
Task<bool> ExistsCompletionEndpointAsync(string id)
Task<EnumerationResult<CompletionEndpoint>> EnumerateCompletionEndpointsAsync(EnumerationRequest request)

// Completion Endpoint Health
Task<EndpointHealthStatus> GetCompletionEndpointHealthAsync(string id)
Task<List<EndpointHealthStatus>> GetAllCompletionEndpointHealthAsync()
```

---

## 11. Python SDK

### 11.1 Update Models

- [ ] **File:** `sdk/python/partio_sdk.py`
- [ ] **Add classes/dicts for:**
  - `SummarizationConfiguration` — inline config dict pattern
  - `CompletionEndpoint` — resource dict pattern
- [ ] **Update existing models:**
  - `EmbeddingConfiguration`: add `embedding_endpoint_id`
  - `SemanticCellRequest`: add `guid`, `parent_guid`, `children`, `summarization_configuration`
  - `SemanticCellResponse`: add `guid`, `parent_guid`, `type`, `children`
  - `ChunkResult`: add `cell_guid`

### 11.2 Update Client

- [ ] **File:** `sdk/python/partio_sdk.py`
- [ ] **Update existing embedding endpoint methods** to use `/v1.0/endpoints/embedding` routes
- [ ] **Update existing process methods** to use `/v1.0/process` routes
- [ ] **Add methods:**

```python
# Completion Endpoints (at /v1.0/endpoints/completion)
create_completion_endpoint(self, data)
get_completion_endpoint(self, endpoint_id)
update_completion_endpoint(self, endpoint_id, data)
delete_completion_endpoint(self, endpoint_id)
enumerate_completion_endpoints(self, request=None)

# Completion Endpoint Health
get_completion_endpoint_health(self, endpoint_id)
get_all_completion_endpoint_health(self)
```

---

## 12. JavaScript SDK

### 12.1 Update Models/Types

- [ ] **File:** `sdk/js/partio-sdk.js`
- [ ] Document new fields in JSDoc comments
- [ ] Update request/response object shapes

### 12.2 Update Client

- [ ] **File:** `sdk/js/partio-sdk.js`
- [ ] **Update existing embedding endpoint methods** to use `/v1.0/endpoints/embedding` routes
- [ ] **Update existing process methods** to use `/v1.0/process` routes
- [ ] **Add methods:**

```javascript
// Completion Endpoints (at /v1.0/endpoints/completion)
createCompletionEndpoint(data)
getCompletionEndpoint(id)
updateCompletionEndpoint(id, data)
deleteCompletionEndpoint(id)
enumerateCompletionEndpoints(request)

// Completion Endpoint Health
getCompletionEndpointHealth(id)
getAllCompletionEndpointHealth()
```

---

## 13. SDK Test Harnesses

All three test harnesses must be updated to exercise the new functionality.

### 13.1 C# Test Harness

- [ ] **File:** `sdk/csharp/Partio.Sdk.TestHarness/Program.cs`
- [ ] **Add tests:**
  1. Completion Endpoint CRUD (create, read, update, enumerate, exists, delete)
  2. Process single cell **without** summarization (verify backward compat of pipeline)
  3. Process single cell **with** summarization (flat cell, verify summary child in response)
  4. Process hierarchical cells with bottom-up summarization
  5. Process hierarchical cells with top-down summarization
  6. Process batch with mixed summarization configs (some with, some without)
  7. Error: invalid CompletionEndpointId → expected error response
  8. Verify summary cells have `Type = Summary` in response
  9. Verify summary chunks have embeddings

### 13.2 Python Test Harness

- [ ] **File:** `sdk/python/test_harness.py`
- [ ] Same test coverage as C# harness, adapted to Python patterns

### 13.3 JavaScript Test Harness

- [ ] **File:** `sdk/js/test-harness.js`
- [ ] Same test coverage as C# harness, adapted to JavaScript patterns

---

## 14. Postman Collection

- [ ] **File:** `Partio.postman_collection.json`

### 14.1 New Variables

- [ ] `completionEndpointId` — completion endpoint ID for variable substitution
- [ ] `completionEndpointUrl` — base URL for completion API
- [ ] `completionModel` — model name for completion
- [ ] `completionApiFormat` — Ollama or OpenAI
- [ ] `summarizationOrder` — TopDown or BottomUp

### 14.2 Update Folder: "Embedding Endpoints"

- [ ] Update all embedding endpoint requests to use `/v1.0/endpoints/embedding` routes

### 14.3 New Folder: "Completion Endpoints"

- [ ] Create Completion Endpoint (PUT `/v1.0/endpoints/completion`)
- [ ] Read Completion Endpoint (GET `/v1.0/endpoints/completion/{{completionEndpointId}}`)
- [ ] Update Completion Endpoint (PUT `/v1.0/endpoints/completion/{{completionEndpointId}}`)
- [ ] Delete Completion Endpoint (DELETE `/v1.0/endpoints/completion/{{completionEndpointId}}`)
- [ ] Check Completion Endpoint Exists (HEAD `/v1.0/endpoints/completion/{{completionEndpointId}}`)
- [ ] Enumerate Completion Endpoints (POST `/v1.0/endpoints/completion/enumerate`)

### 14.4 Update Folder: "Process"

- [ ] Update "Process Single" URL from `/v1.0/endpoints/{{endpointId}}/process` to `/v1.0/process`
- [ ] Update "Process Batch" URL from `/v1.0/endpoints/{{endpointId}}/process/batch` to `/v1.0/process/batch`
- [ ] Update request bodies to include `EmbeddingEndpointId` in `EmbeddingConfiguration`
- [ ] Update request bodies to include optional `SummarizationConfiguration` with all fields
- [ ] Add new request: "Process Single with Summarization" (dedicated example)
- [ ] Add new request: "Process Hierarchical Cells with Summarization" (shows parent/child cells)

---

## 15. REST_API.md

- [ ] **File:** `REST_API.md`

### 15.1 Update Section: Embedding Endpoints

- [ ] Update all route documentation from `/v1.0/endpoints` to `/v1.0/endpoints/embedding`

### 15.2 Update Section: Process

- [ ] Update processing route documentation from `/v1.0/endpoints/{id}/process` to `/v1.0/process`
- [ ] Document `EmbeddingConfiguration.EmbeddingEndpointId` as required field

### 15.3 New Section: Completion Endpoints

- [ ] Document all routes at `/v1.0/endpoints/completion` (Create, Read, Update, Delete, Exists, Enumerate, Health, HealthAll)
- [ ] Request/response JSON examples for each
- [ ] CompletionEndpoint property table (including all health check properties)

### 15.4 Update Section: Process Schemas

- [ ] Document the updated `SemanticCellRequest` schema including:
  - `GUID`, `ParentGUID`, `Children` (hierarchical structure)
  - `SummarizationConfiguration` (all properties with types, defaults, validation)
- [ ] Document the updated `EmbeddingConfiguration` schema including `EmbeddingEndpointId`
- [ ] Document the updated `SemanticCellResponse` schema including:
  - `GUID`, `ParentGUID`, `Type`, `Children`
- [ ] Document the updated `ChunkResult` schema including `CellGUID`
- [ ] Add full JSON example: hierarchical request with summarization
- [ ] Add full JSON example: hierarchical response with summary cells

### 15.5 New Section: Summarization

- [ ] Explain TopDown vs BottomUp strategies
- [ ] Explain the prompt template system (`{tokens}`, `{content}`, `{context}`)
- [ ] Explain the default prompt
- [ ] Document the pipeline order: Summarize → Chunk → Embed
- [ ] Explain how summary cells are created and integrated into the hierarchy

### 15.6 Update Section: AtomTypeEnum

- [ ] Add `Summary` to the enumeration table

---

## 16. README.md

- [ ] **File:** `README.md`

### 16.1 Updates

- [ ] Update version badge to v0.2.0
- [ ] Update the "Features" list to include summarization
- [ ] Update the pipeline description: "Summarize → Chunk → Embed"
- [ ] Add a "Summarization" section explaining:
  - What it does (LLM-generated summaries of semantic cells)
  - TopDown vs BottomUp strategies
  - How to configure (inline `SummarizationConfiguration`)
  - Completion endpoint setup
- [ ] Update the API overview table to include completion endpoint routes
- [ ] Update the architecture diagram to show completion endpoint
- [ ] Update SDK code examples to show summarization usage
- [ ] Update the `partio.json` configuration schema if any new settings are added
- [ ] Update curl example to show a summarization request

---

## 17. CHANGELOG.md

- [ ] **File:** `CHANGELOG.md`

### 17.1 New Entry

```markdown
## v0.2.0 (YYYY-MM-DD)

### Added
- **Summarization pipeline step** — optional LLM-powered summarization of semantic cells before chunking and embedding
- **Hierarchical semantic cells** — `SemanticCellRequest` now supports parent-child relationships via `GUID`, `ParentGUID`, and `Children`
- **Completion endpoints** — new CRUD resource type for managing LLM completion/inference API endpoints (Ollama, OpenAI), with full health check support
- **SummarizationConfiguration** — inline configuration supporting TopDown and BottomUp strategies, customizable prompts, parallel processing, and retry logic
- **Summary cell type** — new `AtomTypeEnum.Summary` for cells generated by summarization
- **Dashboard endpoints restructure** — "Endpoints" navigation split into "Embeddings" and "Inference" sub-sections, each with CRUD, health status, and health histograms
- **Dashboard summarization UI** — updated processing view with summarization configuration
- **SDK support** — all three SDKs (C#, Python, JavaScript) updated with completion endpoint methods and summarization models
- **Postman collection** — new completion endpoint folder and summarization request examples

### Breaking Changes
- **Route restructure:** Embedding endpoint routes moved from `/v1.0/endpoints` to `/v1.0/endpoints/embedding`
- **Route restructure:** Processing routes moved from `/v1.0/endpoints/{id}/process` to `/v1.0/process` (embedding endpoint ID now in request body via `EmbeddingConfiguration.EmbeddingEndpointId`)
- `EmbeddingConfiguration` schema changed: added `EmbeddingEndpointId` (required)
- `SemanticCellRequest` schema changed: added `GUID`, `ParentGUID`, `Children`, `SummarizationConfiguration`
- `SemanticCellResponse` schema changed: added `GUID`, `ParentGUID`, `Type`, `Children`
- `ChunkResult` schema changed: added `CellGUID`
- `AtomTypeEnum` extended with `Summary` value
- Dashboard "Endpoints" navigation restructured into "Embeddings" and "Inference" sub-sections
```

---

## 18. Marketing Website

- [ ] **File:** `C:\Code\Partio\partio.github.io\index.html`

### 18.1 Updates

- [ ] Update version badge from `v0.1.0` to `v0.2.0`
- [ ] Update hero/tagline to mention summarization (e.g., "Summarize. Chunk. Embed. Ship Faster.")
- [ ] Add "Summarization" to the trust badges or feature badges
- [ ] Update the "Benefits" cards:
  - Add a new card for summarization (LLM-powered summaries, TopDown/BottomUp strategies)
  - Or update "Semantic Cell Processing" card to include summarization
- [ ] Update the "Features → Processing" section:
  - Add: "Top-down and bottom-up summarization"
  - Add: "Hierarchical semantic cell support"
  - Add: "Completion endpoint management"
- [ ] Update the architecture diagram to show:
  - Completion endpoints alongside embedding endpoints
  - Summarization step in the pipeline
- [ ] Update the terminal demo/example if it shows the processing pipeline
- [ ] Update the API section to mention completion endpoint routes

---

## 19. Automated Tests

- [ ] **File:** `src/Test.Automated/` (new or updated test files)

### 19.1 Unit Tests

- [ ] `SummarizationEngineTests.cs` — test hierarchy building, depth-level organization, cell content extraction
- [ ] `SummarizationConfigurationTests.cs` — test validation (min values, defaults, prompt template)
- [ ] `CompletionEndpointTests.cs` — test model validation
- [ ] `HierarchyHelperTests.cs` — test flatten/deflatten, find parent, get cells by depth

### 19.2 Integration Tests (if applicable)

- [ ] End-to-end processing with mock completion client
- [ ] Hierarchical cell processing through the full pipeline
- [ ] Verify summary cells are chunked and embedded

---

## 20. Task Checklist

Master checklist for tracking implementation progress. Mark each item when complete.

### Phase 1: Core Models & Enums ✅
- [x] 3.1 — Create `SummarizationOrderEnum`
- [x] 3.2 — Add `Summary` to `AtomTypeEnum`
- [x] 3.3 — Create `SummarizationConfiguration` model (with retry semantics documentation)
- [x] 3.4 — Create `CompletionEndpoint` model (with health check defaults matching EmbeddingEndpoint)
- [x] 3.5 — Update `EmbeddingConfiguration` (add `EmbeddingEndpointId`)
- [x] 3.6 — Update `SemanticCellRequest` (GUID, ParentGUID, Children, SummarizationConfiguration)
- [x] 3.7 — Update `SemanticCellResponse` (GUID, ParentGUID, Type, Children)
- [x] 3.8 — Update `ChunkResult` (CellGUID)
- [x] 3.9 — Update `Constants.cs` (version, prefix)
- [x] 3.10 — Update `IdGenerator.cs` (add `NewCompletionEndpointId()`)

### Phase 2: Completion Clients ✅
- [x] 4.1 — Create `CompletionClientBase`
- [x] 4.2 — Create `CompletionCallDetail` model
- [x] 4.3 — Create `OllamaCompletionClient`
- [x] 4.4 — Create `OpenAiCompletionClient`

### Phase 3: Summarization Engine ✅
- [x] 5.1 — Create `SummarizationEngine` main class
- [x] 5.2 — Implement hierarchy helpers (deflatten, flatten, depth levels, find parent, get content)
- [x] 5.3 — Implement bottom-up processing
- [x] 5.4 — Implement top-down processing
- [x] 5.5 — Implement summary cell creation

### Phase 4: Database Layer ✅
- [x] 6.1 — Update `DatabaseDriverBase` with abstract methods
- [x] 6.2 — SQLite: table creation + CRUD implementation
- [x] 6.3 — PostgreSQL: table creation + CRUD implementation
- [x] 6.4 — MySQL: table creation + CRUD implementation
- [x] 6.5 — SQL Server: table creation + CRUD implementation

### Phase 5: Server — Endpoint Routes & Health ✅
- [x] 7.1 — Rename existing embedding endpoint routes from `/v1.0/endpoints` to `/v1.0/endpoints/embedding`
- [x] 7.2 — Add completion endpoint CRUD routes at `/v1.0/endpoints/completion`
- [x] 7.3 — Move processing routes from `/v1.0/endpoints/{id}/process` to `/v1.0/process`
- [x] 7.4 — Implement completion endpoint route handlers
- [x] 7.5 — Implement completion endpoint health check service

### Phase 6: Server — Processing Pipeline ✅
- [x] 8.1 — Update `ProcessCellAsync` (resolve embedding endpoint from request body, add hierarchy normalization, add summarization step, add pipeline error handling)
- [x] 8.2 — Implement hierarchical chunking/embedding traversal
- [x] 8.3 — Update batch processing for hierarchical cells
- [x] 8.4 — Update request history to include completion call details

### Phase 7: Dashboard ✅
- [x] 9.1 — Restructure "Endpoints" nav into "Embeddings" and "Inference" sub-sections
- [x] 9.2 — Create `CompletionEndpointsView` with CRUD, health checks, health status, and health histogram
- [x] 9.3 — Update `App.jsx` routes (`endpoints/embeddings`, `endpoints/inference`)
- [x] 9.4 — Update `ChunkEmbedView.jsx` with summarization UI
- [x] 9.5 — Update `api.js` (update embedding endpoint URLs, update process URLs, add completion endpoint + health check methods)

### Phase 8: SDKs ✅
- [x] 10.1 — C# SDK: new model classes
- [x] 10.2 — C# SDK: update existing models (including EmbeddingConfiguration.EmbeddingEndpointId)
- [x] 10.3 — C# SDK: update client (update embedding/process URLs, add completion endpoint methods)
- [x] 11.1 — Python SDK: update models (including embedding_endpoint_id)
- [x] 11.2 — Python SDK: update client (update embedding/process URLs, add completion endpoint methods)
- [x] 12.1 — JavaScript SDK: update models/types
- [x] 12.2 — JavaScript SDK: update client (update embedding/process URLs, add completion endpoint methods)

### Phase 9: SDK Test Harnesses ✅
- [x] 13.1 — C# test harness: update process calls, add completion endpoint CRUD tests, add EmbeddingEndpointId
- [x] 13.2 — Python test harness: update process calls, add completion endpoint CRUD tests, add EmbeddingEndpointId
- [x] 13.3 — JavaScript test harness: update process calls, add completion endpoint CRUD tests, add EmbeddingEndpointId
- [x] Test.Automated: update process calls, add completion endpoint CRUD tests, add EmbeddingEndpointId

### Phase 10: Documentation & Collection ✅
- [x] 14.1 — Postman: add variables
- [x] 14.2 — Postman: update embedding endpoint folder URLs
- [x] 14.3 — Postman: add completion endpoint folder
- [x] 14.4 — Postman: update process requests (new URLs, EmbeddingEndpointId in body, SummarizationConfiguration)
- [x] 15.1 — REST_API.md: update embedding endpoint section URLs
- [x] 15.2 — REST_API.md: update process section URLs + EmbeddingEndpointId
- [x] 15.3 — REST_API.md: completion endpoint section
- [x] 15.4 — REST_API.md: update process schemas (SemanticCellRequest, Response, ChunkResult, EmbeddingConfiguration)
- [x] 15.5 — REST_API.md: summarization section
- [x] 15.6 — REST_API.md: update AtomTypeEnum
- [x] 16.1 — README.md: all updates
- [x] 17.1 — CHANGELOG.md: v0.2.0 entry
- [x] SDK README.md files (C#, Python, JavaScript)
- [x] SDK launcher scripts (go.bat, go.sh for each SDK)
- [x] SDK test harness consistency (43 identical tests across all 3 SDKs)

### Phase 11: Marketing Website ✅
- [x] 18.1 — Update partio.github.io (version badge, hero, meta tags, trust badges, terminal demo, features, benefits cards, API routes, architecture diagram)

### Phase 12: Automated Tests ✅
- [x] 19.1 — Unit tests (38 tests in SummarizationTests.cs: SummarizationConfiguration defaults, CompletionEndpoint model, SemanticCellRequest hierarchy, SemanticCellResponse, round-trip)
- [x] 19.2 — Integration tests (covered by existing SDK test harnesses and Test.Automated Program.cs)

---

*End of plan.*
