# Changelog

## v0.5.2 - 2026-10-19

### Added
- **Every PolyPrompt provider.** `ApiFormat` accepts seven new values on embedding and completion endpoints:
  `AzureOpenAI`, `Anthropic` (completion only), `Cohere`, `Bedrock`, `VertexAI`, `TEI` (embedding only), and
  `VoyageAI` (embedding only). Each is backed by its PolyPrompt 3.4 client.
- **`ProviderSettings`** is a new optional object on both endpoint models. It holds one section per provider,
  and only the section that matches `ApiFormat` is stored:
  - `AzureOpenAI`: `ApiVersion` and `AuthMode` (API key or Entra ID bearer token).
  - `Anthropic`: `AnthropicVersion` and `WorkspaceId`.
  - `Bedrock`: `Region` plus static access keys (with an optional session token) or the server environment.
  - `VertexAI`: `ProjectId`, `Region`, and a service-account JSON, Application Default Credentials, or an access token.
  - `Cohere`: `InputType`.
  - `VoyageAI`: `InputType` and `OutputDimension`.
  Existing fields keep their names and shapes; `Model` is the Azure deployment name and the Bedrock model ID.
  Settings are stored in a new `provider_settings_json` column, added automatically on all four databases.
  `api_key` is widened so Entra ID tokens fit.
- **Validation.** Create and update return `400` when:
  - an endpoint kind is unsupported, such as TEI used for completion or Anthropic for embeddings;
  - a required API key, resource URL, region, project, or credential is missing;
  - a service-account JSON is malformed.
- **Endpoint defaults.**
  - **Hosted providers** (OpenAI, Gemini, Anthropic, Cohere, VoyageAI, xAI) default to their public API root.
  - **Bedrock and Vertex AI** derive the regional URL from `ProviderSettings`.
  - **Trimming:** trailing version segments (`/v1`, `/v2`, `/openai`) are removed.
  - **Empty endpoints:** `"Endpoint": ""` now means "use the default"; it previously caused a 500. Providers
    without a default (Ollama, vLLM, TEI, Azure OpenAI) still require an endpoint, and an empty value
    returns 400.
- **Secret masking and reveal APIs.**
  - **Masking:** every endpoint read, enumeration, create, and update response masks `ApiKey`,
    `Bedrock.SecretAccessKey`, `Bedrock.SessionToken` (`****` plus the last four characters), and
    `VertexAI.ServiceAccountJson` (`****`).
  - **Updates:** sending back a masked value keeps the stored secret, and `null` clears it.
  - **Reveal:** new `GET /v1.0/endpoints/embedding/{id}/reveal` and `GET /v1.0/endpoints/completion/{id}/reveal`
    return the unmasked endpoint. They are admin only, and each call is logged.
  - **SDKs:** the C#, JS, and Python SDKs add `RevealEndpointAsync` / `RevealCompletionEndpointAsync`
    (and the language equivalents).
  - **MCP:** the server deliberately has no reveal tool.
- **Health checks per provider.**
  - **HTTP checks:**
    - Azure OpenAI uses `/openai/models?api-version=…` with `api-key`.
    - Anthropic uses `/v1/models` with `x-api-key` and `anthropic-version`.
    - Cohere uses `/v1/models`.
    - TEI uses `/health`.
  - **Connectivity probe:** Bedrock, Vertex AI, and VoyageAI can't be checked with a plain HTTP request, so
    they use PolyPrompt's connectivity probe, with defaults of a 60 s interval and a 10 s timeout.
  - **Credential caching:** Vertex AI service-account and ADC tokens are cached and refreshed instead of being
    exchanged on every request.
- **Proxy.**
  - **New allow-lists:**
    - Azure OpenAI: `openai/deployments/{deployment}/…` and `openai/v1/…`, with the configured `api-version`
      appended when absent.
    - Anthropic: `v1/messages`, `v1/messages/count_tokens`, and `v1/models`.
    - Cohere: `v2/chat`, `v2/embed`, and `v1/models`.
  - **Signed providers:** Bedrock and Vertex AI requests are signed per request, so the proxy refuses them with
    an explanatory 404.
- **Tokenization defaults** for `AzureOpenAI` (8192), `Cohere` (512), `Bedrock` (8192), `VertexAI` (2048), `TEI`
  (512), and `VoyageAI` (32000). Telemetry `service` labels add `azureopenai`, `anthropic`, `cohere`,
  `bedrock`, `vertexai`, `tei`, and `voyageai`.
- **Selectable authentication** for `Ollama`, `vLLM`, `TEI`, and `OpenAI` endpoints, which are often deployed
  behind gateways. A new `ProviderSettings.Auth` section selects how `ApiKey` is sent:
  - `None` (no credential; `ApiKey` is cleared on save), `Bearer` (`Authorization: Bearer`), `Header` (a custom
    header named by `HeaderName`), `QueryParameter` (appended to every upstream URL as `QueryParameterName`), or
    `Basic` (`Authorization: Basic` with `Username` and `ApiKey` as the password).
  - **Backward compatible:** without `Auth`, a configured `ApiKey` is still sent as a bearer token.
  - **Everywhere:** the mode applies to provider calls, health checks, and the proxy. Custom-header and query
    credentials are applied by a Partio transport around PolyPrompt's client, so they never appear in recorded
    call details, health-check logs, or the recorded upstream URL.
  - **Validation:** `400` for a missing credential, a missing or invalid header name, headers the HTTP client
    manages (such as `Host` or `Content-Length`), a missing or malformed query parameter name, or a missing
    basic user name.
  - Providers with one documented scheme ignore `Auth`: Anthropic (`x-api-key`), Gemini (`x-goog-api-key`), Azure
    OpenAI (`api-key` or an Entra bearer token), Ollama Cloud, xAI, Cohere, and VoyageAI (bearer), Bedrock
    (SigV4), and Vertex AI (OAuth).

### Changed
- **Dashboard endpoint forms redesigned.** Creating and editing embedding and inference endpoints now starts with
  a provider picker: grouped cards showing what each provider serves, how it authenticates, and whether it is
  health-checked by probe. Only providers that can serve the endpoint kind are offered.
  - **Connection section:** built from the chosen provider, so each one shows only what it needs. For example,
    Azure shows a deployment name, resource URL, API version, and API key or Entra token; Bedrock shows a
    region, access keys or the server environment, and the derived endpoint with an override; Vertex AI shows
    a project, region, and service-account JSON (paste or load a file), ADC, or an access token.
  - **Validation and secrets:** fields are validated inline before saving. Stored secrets appear masked, with
    Reveal and Replace actions.
  - **Provider changes:** switching providers on an existing endpoint asks for confirmation.
  - **Health Checks section:** for probe providers, it explains the probe instead of asking for a URL.
  - **Authentication:** providers with a fixed scheme show exactly what is sent (for example "Bearer token,
    `Authorization: Bearer …`" or "`x-api-key` header") with a correctly labelled credential field. Ollama, vLLM,
    TEI, and OpenAI get an Authentication selector (None, Bearer token, Custom header, Query parameter, Basic
    auth) with the fields each mode needs.
  - **Chat and API Explorer:** both support the Azure OpenAI, Anthropic, and Cohere dialects. Endpoints that
    can't be proxied are hidden from Chat and from the proxy explorer.
- **Duplicate action.** The row action menus for tenants, users, credentials, embedding endpoints, and inference
  endpoints have a Duplicate action. It opens the create form prefilled from the selected entry, with "(copy)"
  added to the name.
  - **Endpoints:** duplicating reads the source's real secrets through the reveal API, so the copy never stores
    masked placeholders.
  - **Users:** the email (which must be unique) and password start blank.
  - **Credentials:** the duplicate gets a new bearer token from the server.
- **No tuples or `var` in the C# codebase.** Tuple usages were replaced with explicit types: the proxy route
  table is two string lists, the context-length batch split returns a list of halves, provider credential
  resolution returns a `ClientCredentials` class, and test helpers use small named classes. The Postman
  collection's test scripts use `const` instead of `var`.

### Tests
- **Test.Automated:** 341 passed, up from 282.
  - New `ProviderRules` and `ProviderWire` suites cover validation, normalization, auth headers, proxy
    policy, masking, and wire-level tests of every new client against a scripted fake provider, including
    SigV4 signing and the service-account token exchange.
  - New integration cases cover validation, normalization, masking and reveal, admin-only reveal,
    Anthropic, Azure, and Bedrock traffic, TEI and Cohere embeddings, and probe-based health checks.
  - Selectable authentication is covered by rule tests, a wire test of every mode against Ollama and OpenAI
    clients (including a check that custom credentials stay out of call details), and integration tests of a
    header-authenticated Ollama endpoint (health check, completion, proxy) plus query-parameter, basic, and
    `None` endpoints.
- **SDK harnesses:** C#, JS, and Python each pass 69 tests, adding provider, masking, reveal, validation, and selectable-auth tests.
- **xUnit and NUnit:** 213 passed each.
- **Dashboard:** 55 tests, up from 19.

## v0.5.1 - 2026-10-18

### Added
- **Ollama Cloud and xAI (Grok) providers.** `ApiFormat` accepts two new values on embedding and completion
  endpoints, backed by PolyPrompt 3.4's `OllamaCloud*` and `XAi*` clients:
  - `OllamaCloud` speaks the native Ollama API at the fixed host `https://ollama.com`. The server stores that
    host on every OllamaCloud endpoint regardless of the `Endpoint` value sent. Health checks default to
    `/api/tags` with bearer auth, the proxy relays the Ollama `api/...` allow-list, and model loading warms
    with a minimal request because hosted models are already resident.
  - `xAI` defaults to `https://api.x.ai` (a trailing `/v1` is accepted and dropped). Completions use xAI's
    native Responses API (`/v1/responses`), and embeddings use `/v1/embeddings`. Health checks default to
    `/v1/models` with bearer auth. The proxy relays `v1/responses`, `v1/chat/completions`, `v1/embeddings`,
    `v1/models`, `v1/models/{model}`, `v1/language-models`, and `v1/embedding-models`. A new
    `TokenizationDefaults.xAI` profile (cl100k_base, 8192 tokens, `PerInput`) is used for xAI embeddings.
  - Both formats require `ApiKey`: create and update return `400` without one.
  - Telemetry `service` labels add `ollamacloud` and `xai`.
  - Surfaced across the dashboard (API Format selectors, provider defaults, a read-only endpoint URL for Ollama
    Cloud, required-key hints, Chat and API Explorer proxy defaults), the MCP tool schemas, the C#/JS/Python
    SDK docs, REST_API.md, the Postman collection, and the Docker configs.

### Changed
- **Dependency updates:** PolyPrompt 2.6.0 → 3.4.0, Voltaic 2.0.0 → 2.4.0, Watson 7.2.0 → 7.3.1,
  Radiant 0.1.2 → 0.1.3, Microsoft.ML.Tokenizers 2.0.0 → 2.0.1, SyslogLogging 2.2.2 → 2.4.1 (floating),
  NUnit 4.6.1 → 5.0.0, NUnit3TestAdapter 6.3.0 → 6.3.1, Microsoft.NET.Test.Sdk 18.10.1 → 18.11.0,
  xunit.runner.visualstudio 4.0.0 → 4.0.1, and Touchstone (Core, Cli, XunitAdapter, NunitAdapter)
  0.1.12 → 0.2.1. Dashboard npm dependencies refreshed within their semver ranges, and `npm audit` is clean.
- **Security:** `System.Security.Cryptography.Pkcs` is pinned to 10.0.13 because Microsoft.Data.SqlClient's
  transitive versions were affected by GHSA-846f-f764-76mx (high) and GHSA-2fmv-rv2c-59x7 (moderate).
- **PolyPrompt 3.x migration.** PolyPrompt 3 splits each provider client into one client per capability, so
  Partio's provider wrappers now use `OpenAiCompletionClient` / `OpenAiEmbeddingClient`,
  `OllamaCompletionClient` / `OllamaEmbeddingClient`, and `GeminiCompletionClient` / `GeminiEmbeddingClient`
  (replacing `OpenAiClient`, `OllamaClient`, and `GeminiClient`), `CompletionOptions` (replacing
  `ChatCompletionOptions`), and `CallDetail` (replacing `CompletionCallDetail`). No change to Partio's REST
  API or request history shape. PolyPrompt 3 sends the Gemini API key only in the `x-goog-api-key` header.
- **MCP server on Voltaic 2.2.1.** Voltaic now follows the MCP specification more strictly, which changes what
  raw JSON-RPC callers see (MCP clients such as Claude Code, Codex, Cursor, Gemini, and Mux already behave this
  way and are unaffected):
  - `ping` requires authentication like every other request (a missing or invalid bearer is `401`); use
    `GET /` to probe liveness without a credential.
  - On the handshake era, every request other than `initialize` and `ping` needs an initialized session,
    on `/rpc` as well as `/mcp`. A sessionless `/rpc` call runs on a fresh connection, so `tools/list` or
    `tools/call` without the `MCP-Session-Id` from `initialize` gets `-32600`. Stateless `2026-07-28`
    requests are unaffected.
  - Tool argument validation failures (missing required argument, wrong type, undeclared argument on an
    `additionalProperties: false` schema) are tool results with `isError: true` and a message naming the
    problem, rather than JSON-RPC `-32602` errors.
  - Tool failures keep Partio's error message (for example `Admin access required`): the server sets
    `IncludeToolExceptionMessages`, since Voltaic 2.2 otherwise replaces it with a generic internal error.

- **C# SDK published to NuGet** as [`Partio.Sdk`](https://www.nuget.org/packages/Partio.Sdk/) (0.5.1, with
  symbols). The package now carries authors, description, MIT license, repository URL, tags, README, and icon.

### Fixed
- **`partio-mcp mcp stdio` bridge works with Voltaic 2.2.** The bridge (used by Codex) now carries the
  `MCP-Session-Id` that `initialize` returns on every later request, and drops it on a `404` so the harness
  can re-initialize. Without this, every request after the handshake was rejected with `-32600`.

### Tests
- `Mcp` suite updated for the Voltaic 2.2 lifecycle (`/rpc` calls run on an initialized session; validation
  failures assert `isError` results and their messages) and extended with: authenticated `ping` on `/rpc`,
  `ping` within an `/mcp` session, `ping` without a bearer is `401` on both endpoints, `/rpc` before
  `initialize` is `-32600`, `/mcp` `ping` without a session is `400`, and an end-to-end stdio bridge test
  (initialize → tools/list → tools/call through `partio-mcp mcp stdio`).
- Ollama Cloud and xAI coverage. `ProviderClients`: the Ollama Cloud clients target the fixed host with a
  bearer token (embeddings, chat, and the `/api/show` capability probe, through a redirecting test
  transport); the xAI completion client uses `/v1/responses`; the xAI embedding client accepts a `/v1` base;
  both providers reject a missing API key. `Core`: proxy allow-lists and auth headers, endpoint normalization,
  and health-check defaults. `Integration`: `400` without an `ApiKey`, the Ollama Cloud host pinned on create
  and update, xAI warm requests (completion and embedding), and an xAI `v1/responses` proxy passthrough.
  The dashboard adds a vitest suite for the shared ApiFormat helpers.
- `ProviderClients` suite extended with PolyPrompt 3 coverage: OpenAI completion call details map the
  request/response fields (URL, method, status, model, prompt, system prompt, max tokens, response body), and
  Gemini completion/embedding clients never place the API key in the request URL.

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
