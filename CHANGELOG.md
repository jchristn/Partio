# Changelog

## v0.5.0 - 2026-08-26

### Added
- **`/v1.0/proxy/{endpointId}/<provider-native-subpath>`**: a transparent completion proxy that relays a
  caller's **native** provider request (OpenAI, Ollama, Gemini, or vLLM) to a configured completion
  endpoint's upstream and returns the provider's response **verbatim**. Point a native provider SDK's base
  URL at `…/v1.0/proxy/{endpointId}` and the SDK's own sub-path (`/v1/chat/completions`, `/api/chat`,
  `/v1beta/models/{model}:generateContent`, …) is relayed unchanged. Partio authenticates the caller
  (tenant bearer), resolves the endpoint (tenant scope + active + healthy), **injects the upstream API key**
  (so the caller never holds the provider credential), enforces the endpoint's `MaxConcurrentRequests` /
  `MaxQueueDepth` / `MaximumTimeoutMs`, and records request history — but performs **no request/response
  translation**: an endpoint speaks exactly one dialect and only that dialect's native sub-paths are
  relayed (any other path is rejected `404`, never forwarded). Unlike `POST /v1.0/completion`, the proxy
  **passes the upstream HTTP status code through** (real `429`/`500`/`504` and native error bodies) rather
  than wrapping failures as `200` + `Success=false`, so off-the-shelf provider SDKs behave normally.
  **Streaming works**: the proxy relays the upstream response as it arrives (chunked transfer for SSE and
  provider streaming such as Ollama NDJSON), making it Partio's streaming path while `/v1.0/completion`
  stays buffered. Registered as authenticated dynamic (regex) `GET`/`POST` routes. Partio-level failures
  (unknown/inactive/unhealthy endpoint → `404`/`502`, disallowed sub-path → `404`, upstream unreachable →
  `502`, concurrency limit → `429`, timeout → `504`) return a Partio JSON error. Added to the C# SDK
  (`PartioClient.ProxyAsync` / `ProxyPostAsync` / `ProxyGetAsync`, new `ProxyResponse` model), the Python
  SDK (`proxy` / `proxy_post` / `proxy_get`), and the JavaScript SDK (`proxy` / `proxyPost` / `proxyGet`);
  surfaced in the dashboard as a **Proxy** mode in the API Explorer and a **Chat** view — a live
  conversation with an endpoint through the proxy in its native dialect (no client-side provider key), with
  **token streaming** for Ollama (NDJSON), OpenAI/vLLM (SSE), and Gemini (SSE via `:streamGenerateContent`),
  configurable generation parameters (temperature, top-p, max tokens), a **Stop** control that cancels an
  in-flight response, and a per-response metrics tooltip (time-to-first-token, total time,
  prompt/output/total/cached tokens, and provider timings — the request is massaged to request usage,
  e.g. OpenAI `stream_options.include_usage`); exposed as the `partio_proxy` MCP tool
  (bringing the MCP tool count to fifteen); with `ProxyPathPolicy` unit tests, self-hosted integration
  tests (OpenAI chat relay, GET model discovery passthrough, disallowed sub-path `404`, unknown endpoint
  `404`), a **live-upstream** proxy integration test in the general harness (opt-in via `--inference-endpoint`
  / `--upstream-bearer` / `--upstream-format` / `--inference-model`), proxy exercises in all three SDK test
  harnesses (C#, Python, JavaScript), a self-booting `Partio.Sdk.ProxyLiveTest` console for live end-to-end
  runs, REST API docs, MCP API docs, and Postman entries.
- **`POST /v1.0/completion`**: a production completion endpoint that generates a completion for a prompt
  through a configured completion endpoint using a **single upstream call**. It mirrors `POST /v1.0/embed`
  for completions and is the recommended way to validate that a completion endpoint is reachable and
  working, without the extra diagnostics (and probing) of `POST /v1.0/explorer/completion`. Request body
  `CompletionRequest` (`EndpointId`, `Prompt`, optional `SystemPrompt`, `MaxTokens`, `TimeoutMs`); response
  `CompletionResponse` (`Success`, `StatusCode`, `Error`, `EndpointId`, `Model`, `Prompt`, `SystemPrompt`,
  `Output`, `ResponseTimeMs`, `RequestHistoryId`, `CompletionCalls`). Validation and resolve failures return
  real HTTP status codes (`400`/`404`); an upstream failure such as a timeout returns `200` with
  `Success=false` and `StatusCode` set to the upstream failure code (for example `504`); an endpoint
  concurrency limit returns `429`. Added to the C# SDK as `PartioClient.CompleteAsync`, with integration
  tests, REST API docs, and a Postman entry.
- **Per-endpoint request queueing** via a new `MaxQueueDepth` setting on embedding and completion
  endpoints (default `0`, clamped server-side to `>= 0`). Once `MaxConcurrentRequests` upstream calls
  are in flight, up to `MaxQueueDepth` further requests wait for a slot instead of being rejected. A
  queued request that waits past `MaximumTimeoutMs` returns `504 Gateway Timeout`; when the queue is
  full the endpoint returns `429 Too Many Requests`. The default of `0` preserves today's behavior:
  requests over the concurrency limit are rejected immediately with `429`.
- **MCP server** (`src/Partio.McpServer`, executable `partio-mcp`), a standalone Model Context Protocol
  server built on Voltaic. It exposes tools to manage embedding and completion endpoints — including
  `MaxConcurrentRequests` and `MaxQueueDepth` — and to run summarize/chunk/embed, calling Partio over the
  REST SDK. JSON-RPC 2.0 over Streamable HTTP at `/mcp` (and `/rpc`), bearer authentication (rejected with
  `401` before any tool runs), and permissive CORS with an OPTIONS preflight handler enabled by default.
  A `partio-mcp mcp install` / `mcp remove` command wires it into Claude Code, Codex, Gemini, Cursor, and
  Mux, and a `partio-mcp mcp stdio` bridge serves stdio-only harnesses. See `MCP_API.md` and
  `docs/INSTRUCTIONS_FOR_*.md`.
- **Observability, built in.** Metrics and traces are emitted from the whole product, collected into a
  local stack that comes up with `docker compose up`, and rendered in per-domain Grafana dashboards.
  - **Watson 7.1 built-in telemetry** enabled (`Settings.Telemetry`): the entire HTTP surface
    (`http_server_*`, `watson_*`) and one server span per request, served on the in-process Prometheus
    endpoint at `GET /metrics`.
  - **Application metrics** (`PartioMetrics`, dependency-free Prometheus registry) served at
    `GET /v1.0/metrics`, covering the processing pipeline and its stages, outbound provider integrations,
    endpoint health checks and gauges, model loading, request-history persistence/cleanup, and
    authorization decisions. Every family is prefixed `partio_`.
  - **Application traces** on a `Partio` activity source, exported to Tempo over OTLP via a Radiant host
    that also subscribes to Watson's server spans — a `process` root span with per-stage children, and a
    client span per provider call, all nested in one trace. Best-effort: telemetry failures never affect
    request handling.
  - **New `Telemetry` settings section** (`Enabled`, `ServiceName`, `OtlpEndpoint`, `OtlpProtocol`,
    `PrometheusEnabled`) with a `127.0.0.1` loopback default.
- **Observability stack in `docker/compose.yaml`**: Prometheus, Tempo (pinned `2.6.1`), Loki, an OTLP
  collector (tails Partio's logs into Loki), and Grafana — healthchecked, wired together, and provisioned
  as code with five per-domain dashboards (Overview, HTTP, Processing, Integrations, Endpoint Health) in a
  `Partio` folder.
- **Dashboard "External Services" card** on the home page linking operators to Grafana, Prometheus,
  Tempo, Loki, and Ollama with their URLs and default credentials.
- **`TELEMETRY.md`** documenting the metric families, endpoints, Grafana access, and reading workflows.
- **Test coverage**: positive and negative unit tests for the metrics registry, instrumentation scopes,
  telemetry settings, and the trace-host lifecycle, plus an integration check that the `/metrics` and
  `/v1.0/metrics` endpoints expose the expected families.
- **`docker/update.bat`** — one-shot stack refresh: `docker compose pull` → `down` → `up -d` → `ps -a`.

### Changed
- Version metadata synchronized at `0.5.0` across the server (`Constants.Version`), dashboard, and the
  C# and JavaScript SDKs.
- `docker/compose.yaml` now pins the Partio images to **`v0.5.0`** (`jchristn77/partio-server:v0.5.0`,
  `jchristn77/partio-dashboard:v0.5.0`) instead of `latest`.
- The `Telemetry` settings section is present in both `docker/partio.json` and `docker/factory/partio.json`.
- Dashboard **Process Cells** mode selector no longer renders a redundant "Process" button; the three
  "…only" modes toggle against the default full-process mode, leaving a single Process action.
- Dashboard **Processing** navigation reordered to Process Cells, API Explorer, Request History.
- Dashboard remembers the Request History **timeframe** and **request type** filters and the table
  **rows-per-page** choice across navigation and reloads (via `localStorage`).
- Grafana **Overview → Server Up** stat now renders `UP` (green) / `DOWN` (red) instead of a bare `1`.
- Moved `LOAD_MODELS.md` and `TELEMETRY_PLAN.md` into `archive/` — the model-loading API is documented
  in `README.md`, `REST_API.md`, and the Postman collection.
- **MCP server upgraded from Voltaic 0.7.1 to 2.0.0.** `tools/list` now returns exactly the fifteen Partio
  tools (Voltaic's `ping`/`echo`/`getTime`/`getSessions` demo tools are no longer published; `getSessions`
  disclosed other callers' session ids). The protocol `ping` returns `{}` instead of `"pong"`. Tools are
  callable only through `tools/call` (a bare tool-name method returns `-32601`), and `additionalProperties:
  false` schemas are enforced (`partio_capabilities` rejects undeclared arguments with `-32602`). Stateless
  `2026-07-28` clients such as Claude Code 2.1.x now see the tools on both `/mcp` and `/rpc`.
  `partio_capabilities` reports the negotiated default protocol version (`2025-11-25`).
- **`CreatedUtc` and `LastUpdateUtc` are server-set.** Create (`PUT`) on tenants, users, credentials, and
  embedding/completion endpoints stamps both from the server clock and ignores caller-supplied values;
  update (`PUT /{id}`) keeps the stored `CreatedUtc` and stamps `LastUpdateUtc`. Previously the caller's
  values were stored verbatim, so C# SDK callers (and MCP tools, which use the SDK) created records dated
  `0001-01-01`. Update of a nonexistent id now returns `404` instead of echoing the body. The C# SDK
  models now default both timestamps to `DateTime.UtcNow` rather than `DateTime.MinValue`.
- Dependency updates: Watson 7.2.0, PolyPrompt 2.6.0, Microsoft.NET.Test.Sdk 18.10.1, NUnit3TestAdapter 6.3.0.
- Added the `Mcp` Touchstone integration suite (20 cases, run by the console, xUnit, and NUnit runners): it
  starts Partio and `partio-mcp` and exercises the `/rpc`, handshake `/mcp`, and stateless Claude Code
  sequences, authentication, and the Voltaic 2.0 behavior changes in both directions.

## v0.4.0 - 2026-08-19

### Added
- **Standalone chunking, embedding, and summarization endpoints** so the stages can be run and timed
  independently:
  - `POST /v1.0/chunk` — chunks a semantic cell into text chunks WITHOUT embedding them. Requires no
    embedding endpoint: it uses a built-in `cl100k_base` tokenizer to honor the token budget.
  - `POST /v1.0/embed` — generates embedding vectors for one or more input strings (batch) through a
    specified embedding endpoint, without chunking.
  - `POST /v1.0/summarize` — summarizes text through a completion endpoint (same summarization engine as
    `/v1.0/process`), without chunking or embedding.
- New `ChunkRequest`/`ChunkResponse`, `EmbedRequest`/`EmbedResponse`, and `SummarizeRequest`/`SummarizeResponse` models.
- C#, JavaScript, and Python SDK methods for chunk, embed, and summarize.
- Dashboard "Process" playground gains **Process / Chunk only / Embed only** modes.
- Postman collection examples for `POST /v1.0/chunk`, `POST /v1.0/embed`, and `POST /v1.0/summarize`.
- Shared integration coverage: positive and negative cases for all three endpoints (chunk text, chunk
  empty-regex 400, embed batch, embed missing-endpoint 400, summarize text, summarize missing-endpoint 400).
- Model loading and warming API for configured embedding and inference endpoints:
  - `POST /v1.0/endpoints/embedding/{id}/load`
  - `POST /v1.0/endpoints/completion/{id}/load`
- Native Ollama preload support with `keep_alive`, plus warm-request behavior for OpenAI, Gemini, and vLLM.
- Dashboard `Load Model` row action for embedding and inference endpoints.
- C#, JavaScript, and Python SDK methods for model loading.
- Postman collection examples for embedding load, inference load, Ollama `gemma3:4b`, OpenAI-compatible warm requests, and unsupported native-load handling.
- Shared integration coverage for Ollama load, hosted-provider warm behavior, unsupported native load, and invalid unload-style keep-alive values.

### Changed
- Server, dashboard, JavaScript SDK, and C# SDK package version metadata are synchronized at `0.4.0`.

## v0.3.0 - 2026-05-18

### Added
- **Endpoint-aware tokenization refactor** - embedding chunking and token budgeting now resolve in the target model's token space instead of assuming `cl100k_base`
  - Resolution order is explicit: endpoint override -> provider probe -> provider default -> global fallback
  - Embedding endpoints now accept optional `Tokenization` override settings
  - Explorer embedding responses and request-history detail now expose the resolved `TokenizationProfile`
  - `/v1.0/process` failure history now preserves upstream embedding call detail when available
- **Tokenizer families** - local `cl100k_base` and BERT WordPiece adapters, including first-pass support for Ollama MiniLM / BERT-style embedding families
- **Operator visibility** - dashboard embedding endpoint forms can configure tokenization overrides and the explorer shows tokenizer source, kind, and effective budget
- **Dashboard view** - new top-level "Dashboard" nav item in the React dashboard with:
  - Stacked bar chart showing request counts over time, broken out by success (HTTP 1xx-3xx) and failure (HTTP 4xx-5xx)
  - Selectable request type filter: All Requests, Embeddings, or Inference
  - Selectable timeframe: Last Hour (per-minute), Last 24 Hours (15-minute), Last 7 Days (hourly), Last 30 Days (4-hour)
  - Optional endpoint URL substring filter
  - Summary cards showing total successful, failed, and total request counts
  - Quick actions section with shortcuts to Manage Embedding Endpoints, Manage Inference Endpoints, View Request History, and API Explorer
- **Request statistics API** - `POST /v1.0/requests/statistics` endpoint returning aggregated request counts grouped by time bucket with success/failure breakdown
  - Supported across all database providers: SQLite, PostgreSQL, MySQL, SQL Server
- Dashboard is now the default landing page after login
- Test runners now self-host their integration dependencies: `Test.Automated`, `Test.XUnit`, and `Test.Nunit` start a temporary Partio server and Ollama-compatible upstream stub instead of requiring external local services

### Changed
- `FixedTokenCount` now means a requested model-native token budget, clamped to the resolved embedding endpoint budget before chunking begins
- Semantic chunking strategies now descend within the strategy flow when a sentence, paragraph, list item, row group, or row exceeds budget, so emitted chunks are already in-budget
- Server, dashboard, JavaScript SDK, and C# SDK package version metadata are synchronized at `0.3.0`

## v0.2.0 - 2026-02-18

### Added
- Gemini and vLLM endpoint support as first-class API formats
- PolyPrompt NuGet integration for upstream provider calls
- **Summarization pipeline step** - optional LLM-powered summarization of semantic cells before chunking and embedding
- **Hierarchical semantic cells** - `SemanticCellRequest` now supports parent-child relationships via `GUID`, `ParentGUID`, and `Children`
- **Completion endpoints** - new CRUD resource type for managing LLM completion/inference API endpoints (Ollama, OpenAI, Gemini, vLLM), with full health check support
- **SummarizationConfiguration** - inline configuration supporting TopDown and BottomUp strategies, customizable prompts, parallel processing, and retry logic
- **Summary cell type** - new `AtomTypeEnum.Summary` for cells generated by summarization
- **Dashboard endpoints restructure** - "Endpoints" navigation split into "Embeddings" and "Inference" sub-sections, each with CRUD, health status, and health histograms
- **Dashboard summarization UI** - updated processing view with summarization configuration
- **SDK support** - all three SDKs (C#, Python, JavaScript) updated with completion endpoint methods and summarization models
- **Default inference endpoint** - new tenants are automatically provisioned with a default Ollama inference endpoint
- Chunking strategies: RegexBased, Row, RowWithHeaders, RowGroupWithHeaders, KeyValuePairs, WholeTable
- Table-type chunking strategies for structured data

### Breaking Changes
- **Route restructure:** Embedding endpoint routes moved from `/v1.0/endpoints` to `/v1.0/endpoints/embedding`
- **Route restructure:** Processing routes moved from `/v1.0/endpoints/{id}/process` to `/v1.0/process` (embedding endpoint ID now in request body via `EmbeddingConfiguration.EmbeddingEndpointId`)
- `EmbeddingConfiguration` schema changed: added `EmbeddingEndpointId` (required)
- `SemanticCellRequest` schema changed: added `GUID`, `ParentGUID`, `Children`, `SummarizationConfiguration`
- `SemanticCellResponse` schema changed: added `GUID`, `ParentGUID`, `Type`, `Children`
- `ChunkResult` schema changed: added `CellGUID`
- `AtomTypeEnum` extended with `Summary` value
- Dashboard "Endpoints" navigation restructured into "Embeddings" and "Inference" sub-sections

## v0.1.0 - 2026-02-06

### Added
- Initial release of Partio
- Multi-tenant REST API with bearer token authentication
- Semantic cell processing with chunking and embedding
- Chunking strategies: FixedTokenCount, SentenceBased, ParagraphBased, WholeList, ListEntry
- Overlap strategies: SlidingWindow, SentenceBoundaryAware, SemanticBoundaryAware
- Embedding clients: Ollama, OpenAI, Gemini, vLLM-compatible
- Database support: SQLite, PostgreSQL, MySQL, SQL Server
- Admin CRUD endpoints for tenants, users, credentials, and embedding endpoints
- Request history with filesystem body persistence and automatic cleanup
- React dashboard (Vite) with full admin UI
- SDKs: C#, Python, JavaScript
- Docker support with multi-arch builds (amd64, arm64)
- Automated test suite
