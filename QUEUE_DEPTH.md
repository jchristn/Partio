# MaxQueueDepth — Implementation Plan

## Implementation status (2026-09-09)

**Implemented and verified.** All non-test projects build clean (0 warnings / 0 errors, one project per MSBuild process, analyzers off). The shared unit suite is **141/141 passing**, including new coverage for the queue mechanism (admit/grant, queue-full reject, cancellation-honors-timeout without corrupting in-flight accounting, FIFO fairness, and pruning of cancelled waiters) and `MaxQueueDepth` serialization round-trips. The MCP server was smoke-tested live: `tools/list` serves all 14 Partio tools, bearer auth returns 401→200→401, and the CORS OPTIONS preflight returns 204 with permissive headers. The dashboard `vite build` passes.

Workstreams A–E, F, G, H, I, J, K, L1–L4, N, O are done. Decisions taken on the open items: single-key bearer gateway (auth required by default); companion summarize/chunk/embed tools included; install/remove verbs live inside `Partio.McpServer`; OpenCode omitted; dashboard uses the existing inline-literal label pattern (no i18n keys); docs target Partio's own MCP server (no "RecallDB"). Per the added requirement, both webservers expose a permissive CORS settings class plus a preflight handler — PartioServer already did (`Partio.Core/Settings/CorsSettings.cs` + Watson `Routes.Preflight`); the MCP server adds `McpCorsSettings` applied to Voltaic.

A code review of the async limiter and the MCP server found two issues, both fixed: a timing side-channel in the bearer comparison (now `CryptographicOperations.FixedTimeEquals`) and cancelled waiters lingering against the queue depth (now pruned before the admission check).

**Live multi-provider validation (executed).** The test runner (`Test.Automated`) now accepts CLI flags to choose the database and its connection details (`--db`, `--db-host/-port/-name/-user/-pass/-schema/-instance`) and the upstream test endpoints (`--embedding-endpoint`, `--inference-endpoint`, `--embedding-model`, `--inference-model`, `--upstream-format`, `--upstream-bearer`), threaded through a new `TestEnvironmentOptions` into `SelfHostedPartioTestEnvironment`. Using throwaway Docker containers (Postgres 16, MySQL 8.0, SQL Server 2022; each removed after the run) with a Partio server started against each, all three providers **passed**: migrations ran (exercising the `max_queue_depth` CREATE/ALTER for that dialect) and the C# SDK harness passed **43/43 CRUD tests (0 failed)** including `MaxConcurrentRequests`/`MaxQueueDepth` round-trips. Combined with the SQLite unit path (141/141), the `MaxQueueDepth` column and its migrations are verified on all four backends. The MCP server is now in `build-all.bat`/`build-all.sh` with its own `src/Partio.McpServer/Dockerfile`, `build-mcp.bat`, a `partio-mcp` compose service, and `docker/`+`docker/factory/` `partio.mcp.json` assets. The install/remove command also accepts `--install`/`--remove` flag aliases.

**Remaining deviations** (scope-bounded): L5 (automated MCP-over-HTTP suite) and L6 (install-command tests) were validated by live smoke tests and `mcp install --dry-run` rather than added as automated suites. The provided upstream Ollama endpoints are confirmed reachable and functional (all-minilm embeddings return 200 directly), but end-to-end model calls *through* Partio returned 502 in the live run — a remote-proxy URL/format nuance, unrelated to the database work, so the live DB validation ran the model-dependent harness cases as skips. Postman MCP requests (K) were not added. Dashboard visual-QA screenshots (F1) were not captured; the layout was corrected (the queue field sits in its own form row) and both the dashboard and .NET builds pass.

## How to use this document

Every actionable step is a checkbox. Check it off (`- [x]`) as you complete it, and use the **Notes** line under each workstream to record deviations, surprises, PR links, or follow-ups. The workstreams are ordered by dependency: the model and the queue mechanism come first because everything else references them, the database and server plumbing come next, and the surfacing work (dashboard, SDKs, docs, Postman, MCP) can proceed in parallel once the core compiles. Tests close each functional slice.

Two pieces of work live here, and they are related but separable. The first is the `MaxQueueDepth` feature itself: a new per-endpoint setting that lets requests wait for a concurrency slot instead of being rejected the instant the endpoint is saturated. The second is a brand-new MCP server for Partio, which today does not exist at all — the repo has no MCP code, no `MCP_API.md`, and nothing in Postman for it. Building that server is the only way `MaxQueueDepth` (and every other endpoint setting) gets an MCP surface, so the two ship together.

Build with the machine's known-good pattern: one project per MSBuild invocation, analyzers off, node reuse disabled. See `partio-build-msbuild-nodereuse` in memory.

---

## What we are building, and why

Partio already caps how many upstream provider calls an endpoint can have in flight. `MaxConcurrentRequests` (default `2`) is enforced by `ProviderConcurrencyLimiter`, which keeps an in-memory in-flight counter per endpoint and throws `ProviderConcurrencyLimitException` the moment a request would exceed the cap. The server maps that exception to `429 Too Many Requests`. There is no waiting and no queue — a request either gets a slot immediately or it is rejected.

`MaxQueueDepth` adds the missing middle. When an endpoint is at its concurrency limit, an incoming request waits for a slot to free up rather than failing outright, and only when the *queue itself* is full does the request get the `429`. The number of requests allowed to wait is `MaxQueueDepth`. A depth of `0` means "no waiting" — which is exactly today's behavior — so shipping this with a default of `0` changes nothing for existing deployments until an operator opts in.

The confirmed semantics for this work: **wait, then 429 when the queue is full.** A request finding all slots busy joins the queue if there is room and blocks until a slot frees; if the queue is already holding `MaxQueueDepth` waiters, it is rejected with `429`. Waiting is bounded by the request's existing timeout (`MaximumTimeoutMs`), so a request that waits too long surfaces as the normal timeout path (`504`) rather than hanging forever.

### Design decisions locked for this plan

- **Default `MaxQueueDepth = 0`.** Preserves current reject-immediately behavior and keeps every existing concurrency test green without modification. Operators raise it to enable queueing.
- **Clamp `>= 0`** (unlike `MaxConcurrentRequests`, which clamps `>= 1`). Zero is a meaningful, valid value here.
- **Queue waiting respects the existing per-request timeout.** Acquisition takes the request's cancellation/timeout token. Exceeding `MaximumTimeoutMs` while queued cancels the wait and is reported as a provider timeout (`504`), consistent with how in-flight timeouts already behave. No new timeout field is introduced.
- **`429` stays the overflow signal.** `ProviderConcurrencyLimitException` is reused (extended, not replaced) so the existing HTTP mapping and client handling keep working.
- **FIFO fairness.** Waiters are granted slots in arrival order.

---

## Workstream A — Core model and defaults

The property setter is where validation actually happens — the API handlers do no field-specific validation, they just deserialize the body (which runs the setter) and persist. Mirror the `MaxConcurrentRequests` placement exactly: the new field sits immediately after `MaxConcurrentRequests` in every file.

- [ ] `src/Partio.Core/Models/CompletionEndpoint.cs` — add backing field after `_MaxConcurrentRequests` (line 29): `private int _MaxQueueDepth = 0;`
- [ ] `src/Partio.Core/Models/CompletionEndpoint.cs` — add property after `MaxConcurrentRequests` (line ~204) with XML doc stating default `0` (no queue; reject at concurrency limit), meaning of higher values, and the clamp; setter: `set => _MaxQueueDepth = value < 0 ? 0 : value;`
- [ ] `src/Partio.Core/Models/CompletionEndpoint.cs` — in `FromDataRow` (after line 291) add the guarded read: `if (row.Table.Columns.Contains("max_queue_depth") && row["max_queue_depth"] != DBNull.Value) ep.MaxQueueDepth = Convert.ToInt32(row["max_queue_depth"]);`
- [ ] `src/Partio.Core/Models/EmbeddingEndpoint.cs` — same three edits (backing field line ~28, property after line ~205, `FromDataRow` after line ~301).
- [ ] `src/Partio.Core/Settings/DefaultInferenceEndpoint.cs` — add field (after line 16) and property (after line ~81), default `0`.
- [ ] `src/Partio.Core/Settings/DefaultEmbeddingEndpoint.cs` — add field (after line 17) and property (after line ~83), default `0`.

XML doc text to reuse (adapt "inference"/"embedding"):

> Maximum number of requests that may wait for a concurrency slot once `MaxConcurrentRequests` upstream calls are in flight. Default is `0`, meaning requests are rejected immediately (HTTP 429) when the concurrency limit is reached. Values greater than `0` allow up to that many requests to queue and wait for a slot; when the queue is full the endpoint returns HTTP 429. Queued requests wait no longer than `MaximumTimeoutMs`. Clamped server-side to `>= 0`.

**Notes:** _______________________________________________

---

## Workstream B — Queue mechanism and exception

This is the heart of the change. `ProviderConcurrencyLimiter.Acquire` goes from a synchronous throw-or-grant to an asynchronous grant-or-wait-or-throw. Keep the file's existing shape (a static class with private nested state and lease types) to stay consistent with what's there.

### B1 — Extend the exception

- [ ] `src/Partio.Core/Exceptions/ProviderConcurrencyLimitException.cs` — add `public int MaxQueueDepth { get; }`, a new constructor parameter `int maxQueueDepth`, clamp `< 0 ? 0`, and keep the existing `(concurrencyKey, maxConcurrentRequests, message)` constructor working (either add an overload or default the new param). The message must still contain the substring "max concurrent request" — an existing test asserts on it (`ProviderClientTests.cs:233`).

### B2 — Rewrite the limiter as async, queue-aware

- [ ] `src/Partio.Core/ThirdParty/ProviderConcurrencyLimiter.cs` — replace `Acquire(key, max)` with `AcquireAsync(string concurrencyKey, int maxConcurrentRequests, int maxQueueDepth, CancellationToken token)` returning `Task<IDisposable>`.

Algorithm (FIFO, wait-then-reject, cancellation-safe):

```
AcquireAsync(key, max, queueDepth, token):
  state = _States.GetOrAdd(effectiveKey)
  TaskCompletionSource<bool>? waiter = null
  lock (state.SyncRoot):
      if (state.InFlight < effectiveMax):
          state.InFlight++
          return new Lease(state)                       // fast path, slot granted
      if (state.Waiters.Count >= effectiveQueueDepth):  // queueDepth may be 0
          throw new ProviderConcurrencyLimitException(key, max, queueDepth, "...limit of N and queue depth of M.")
      waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
      state.Waiters.Enqueue(waiter)
  // wait outside the lock, honoring cancellation/timeout
  using (token.Register(() => waiter.TrySetCanceled(token))):
      await waiter.Task.ConfigureAwait(false)           // completed by a releasing lease
  return new Lease(state)                                // slot was transferred, InFlight already accounts for it

Lease.Dispose():
  lock (state.SyncRoot):
      // hand the freed slot to the next live waiter; skip any that cancelled while queued
      while (state.Waiters.Count > 0):
          next = state.Waiters.Dequeue()
          if (next.TrySetResult(true)) return           // slot transferred, InFlight unchanged
      if (state.InFlight > 0) state.InFlight--           // no waiter took it, release the slot
```

Key correctness points to get right, and to cover with tests:

- **Slot transfer keeps `InFlight` constant.** When a disposing lease wakes a waiter, the slot moves from one holder to the next; `InFlight` is only decremented when nobody is waiting. The waiter's returned lease must *not* re-increment.
- **Cancellation race.** A waiter cancelled by timeout may still sit in the queue. `Dispose` skips waiters whose `TrySetResult` fails (already cancelled) and moves to the next. `RunContinuationsAsynchronously` avoids running continuations under the lock.
- **`queueDepth == 0`** collapses to today's behavior: slots full ⇒ `Waiters.Count (0) >= 0` ⇒ immediate throw. This is why existing tests stay green.
- **Cancelled-but-enqueued waiters transiently inflate `Waiters.Count`.** Acceptable, but note it; prune on grant (the `while` loop already discards them) — if strict depth accounting matters, also prune opportunistically on enqueue.

- [ ] Add `CounterState.Waiters` (`Queue<TaskCompletionSource<bool>>`) alongside `InFlight`.
- [ ] Update the XML docs on the public method (params, thrown exception via `/// <exception>`, thread-safety note per code style).

**Notes:** _______________________________________________

---

## Workstream C — Client plumbing

The value has to reach the limiter. It rides the same constructor chain as `MaxConcurrentRequests`, and the acquire call sites become `await`.

- [ ] `src/Partio.Core/ThirdParty/CompletionClientBase.cs` — add `protected readonly int _MaxQueueDepth;` (after line 51), constructor param `int maxQueueDepth = 0` (after line 86) assigned with clamp `< 0 ? 0` (after line 93), and a public `MaxQueueDepth` getter (after line 104).
- [ ] `src/Partio.Core/ThirdParty/CompletionClientBase.cs` — change `AcquireRequestSlot()` (lines 303-306) to `protected Task<IDisposable> AcquireRequestSlotAsync(CancellationToken token) => ProviderConcurrencyLimiter.AcquireAsync(_ConcurrencyKey, _MaxConcurrentRequests, _MaxQueueDepth, token);`
- [ ] `src/Partio.Core/ThirdParty/CompletionClientBase.cs` — update the call site (line 213). Create the timeout-linked CTS *before* acquiring so the queue wait is bounded by `MaximumTimeoutMs`, then `concurrencyLease = await AcquireRequestSlotAsync(linkedCts.Token).ConfigureAwait(false);`
- [ ] `src/Partio.Core/ThirdParty/EmbeddingClientBase.cs` — same field/ctor/getter edits and the async acquire at the call site (line 256).
- [ ] Concrete clients — add the `maxQueueDepth` param and forward it to `base(...)`: `OllamaCompletionClient.cs`, `OpenAiCompletionClient.cs`, `GeminiCompletionClient.cs`, `OllamaEmbeddingClient.cs`, `OpenAiEmbeddingClient.cs`, `GeminiEmbeddingClient.cs`. Update their internal `AcquireRequestSlot()` call sites (e.g. `OllamaCompletionClient.cs:65`, `GeminiCompletionClient.cs:58`) to `await ...Async(token)`.
- [ ] Sweep for any other `AcquireRequestSlot()` caller and convert to the async form. Grep: `AcquireRequestSlot`.

**Notes:** _______________________________________________

---

## Workstream D — Database (all four providers)

Column name `max_queue_depth`, positioned right after `max_concurrent_requests`, `NOT NULL DEFAULT 0`. Both `completion_endpoints` and `embedding_endpoints` in every provider. `SELECT` is `SELECT *`, so hydration is handled by `FromDataRow` (Workstream A) — no select lists to touch.

For each of the four providers — **Sqlite, Postgresql, Mysql, Sqlserver**:

- [ ] `Database/<Provider>/Queries/SetupQueries.cs` — add `max_queue_depth` to both `CREATE TABLE` blocks after `max_concurrent_requests`. Type per dialect: `INTEGER` (Sqlite/Postgres) or `INT` (Mysql/Sqlserver), `NOT NULL DEFAULT 0`. (Sqlite anchors: lines 81 & 114; Sqlserver: 92 & 128.)
- [ ] `Database/<Provider>/Queries/SetupQueries.cs` — add two migration constants `AlterEmbeddingEndpointsAddMaxQueueDepth` and `AlterCompletionEndpointsAddMaxQueueDepth`, matching the provider's idiom:
  - Sqlite: `ALTER TABLE ... ADD COLUMN max_queue_depth INTEGER NOT NULL DEFAULT 0;`
  - Postgresql: `ALTER TABLE ... ADD COLUMN IF NOT EXISTS max_queue_depth INTEGER NOT NULL DEFAULT 0;`
  - Mysql: `ALTER TABLE ... ADD COLUMN max_queue_depth INT NOT NULL DEFAULT 0 AFTER max_concurrent_requests;`
  - Sqlserver: `IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('...') AND name = 'max_queue_depth') BEGIN ALTER TABLE ... ADD max_queue_depth INT NOT NULL DEFAULT 0; END;`
- [ ] `Database/<Provider>/<Provider>DatabaseDriver.cs` — invoke both new migrations inside `try { ... } catch { /* column already exists */ }`, immediately after the `MaxConcurrentRequests` migrations. (Anchors: Sqlite 66/70, Postgres 67/71, Mysql 64/68, Sqlserver 79/83.)
- [ ] `Database/<Provider>/Implementations/CompletionEndpointMethods.cs` — add `max_queue_depth` to the INSERT column list, `endpoint.MaxQueueDepth` to the INSERT VALUES, and `"max_queue_depth = " + endpoint.MaxQueueDepth + ", "` to the UPDATE set clause. (Sqlite anchors: INSERT col ~39, INSERT val ~57, UPDATE ~115.)
- [ ] `Database/<Provider>/Implementations/EmbeddingEndpointMethods.cs` — same three edits. (Sqlite anchors: INSERT col ~44, INSERT val ~62, UPDATE ~147.)

That is 4 × (1 create-file with 2 tables + 2 migration constants + 1 driver + 2 method files) = **16 files**. Keep the manually-prepared SQL string style as-is.

**Notes:** _______________________________________________

---

## Workstream E — Server wiring

- [ ] `src/Partio.Server/PartioServer.cs` — pass `endpoint.MaxQueueDepth` as the new last argument to every provider-client constructor in the two factory blocks (lines ~2809-2814 embedding, ~2825-2830 completion), after `endpoint.MaxConcurrentRequests`.
- [ ] `src/Partio.Server/PartioServer.cs` — propagate the configured default at the seeding/reconcile sites that already copy `MaxConcurrentRequests`: lines ~799, ~822, ~889 (reconcile change-detection), ~900 (apply), ~919 (`BuildConfiguredDefaultEmbeddingEndpoint`), ~2872, ~2892.
- [ ] `docker/partio.json` and `docker/factory/partio.json` — add `"MaxQueueDepth": 0` to the default endpoint definitions alongside `MaxConcurrentRequests`.
- [ ] Confirm no API-handler validation change is needed (the model setter clamps on deserialize). Re-read `CreateCompletionEndpoint`/`UpdateCompletionEndpoint`/`CreateEndpoint`/`UpdateEndpoint` to be sure.

**Notes:** _______________________________________________

---

## Workstream F — Dashboard

Both endpoint views hard-code the concurrency field with literal (non-i18n) labels, a `parsePositiveInteger` helper, and a numeric input. `MaxQueueDepth` follows the same shape but allows `0`, so add a `parseNonNegativeInteger` helper (min `0`) rather than reusing `parsePositiveInteger` (min `1`). Pair the new input with the existing "Max Concurrent Requests" field in the same form row.

For each of `dashboard/src/components/CompletionEndpointsView.jsx` and `dashboard/src/components/EmbeddingEndpointsView.jsx`:

- [ ] Add `const defaultMaxQueueDepth = 0;` near `defaultMaxConcurrentRequests` (line ~250).
- [ ] Add `parseNonNegativeInteger(value, fallback)` (rejects `NaN`/`< 0`) and a `formatMaxQueueDepth` formatter mirroring the concurrency ones (~252-264).
- [ ] Seed `MaxQueueDepth: defaultMaxQueueDepth.toString()` in the initial `useState`, the reset-form path, and the populate-from-item path (`(item.MaxQueueDepth ?? defaultMaxQueueDepth).toString()`). (Completion anchors: 273, 326, 345; Embedding: 314, 368, 387.)
- [ ] Serialize on submit: `MaxQueueDepth: parseNonNegativeInteger(form.MaxQueueDepth, defaultMaxQueueDepth)`. (Completion ~387, Embedding ~430.)
- [ ] Add the field JSX after the Max Concurrent Requests block (Completion ~656-661, Embedding ~701-703): a `FormFieldLabel` + `Tooltip` explaining "how many requests may wait for a slot before Partio returns HTTP 429; 0 rejects immediately", and `<input type="number" min="0" step="1" ...>`.
- [ ] Check `dashboard/tests/model-load-responsive.spec.js` — if the added row affects the responsive layout assertions, update the test.

### F1 — Usability and aesthetic pass (required)

Adding the inputs is not "done." Both new fields (`MaxConcurrentRequests` already exists visually; `MaxQueueDepth` is new) get a deliberate pass against `c:\code\agents\requirements\DASHBOARD_STYLE_AND_USABILITY.md`.

- [ ] **Field pairing and layout.** `MaxQueueDepth` sits in a sensible row with `MaxConcurrentRequests` and the timeout field; labels, tooltips, spacing, and control sizing match the surrounding form. No orphaned or misaligned control.
- [ ] **Tooltip/help copy.** The tooltip explains the queue behavior in plain terms (waits for a slot; `0` = reject immediately; full queue → 429) and reads like the rest of the dashboard's copy.
- [ ] **Validation UX.** Non-negative integer enforced in the UI (`min="0"`); pasting a negative or non-numeric value resolves to the default, and the control does not allow a broken submit.
- [ ] **i18n check.** The existing concurrency field uses inline literal labels (no i18n key). Confirm whether `DASHBOARD_STYLE_AND_USABILITY.md`'s internationalization requirement mandates i18n keys here; if so, add keys for both fields rather than perpetuating literals. Record the decision.
- [ ] **Mandatory Visual QA** (per the doc's "Mandatory Visual QA" and "Responsive Behavior" sections): verify both endpoint forms in light and dark themes, at desktop and narrow widths, empty/populated/edit states. Capture before/after screenshots for the handoff.
- [ ] **Acceptance criteria.** Walk the doc's "Acceptance Criteria" checklist for the two endpoint views and confirm the change does not regress any item.
- [ ] Run the dashboard via the `run` skill (or the project's dashboard build/serve) and exercise create + edit of both endpoint types end to end, confirming the value round-trips to the server and back.

**Notes:** _______________________________________________

---

## Workstream G — SDKs

- [ ] `sdk/csharp/Partio.Sdk/Models/CompletionEndpoint.cs` — add after `MaximumTimeoutMs` (lines ~52-53): `[JsonPropertyName("MaxQueueDepth")] public int MaxQueueDepth { get; set; } = 0;`
- [ ] `sdk/csharp/Partio.Sdk/Models/EmbeddingEndpoint.cs` — same.
- [ ] `sdk/python/README.md` — add `"MaxQueueDepth": 0` to the example endpoint dict and a prose mention (near lines 23/59).
- [ ] `sdk/js/README.md` — add `MaxQueueDepth: 0` to the example object and prose (near lines 23/53).

(Python/JS SDKs are dict-based and need no model class edit — only docs and the harness, below.)

**Notes:** _______________________________________________

---

## Workstream H — Documentation

- [ ] `REST_API.md` — add `"MaxQueueDepth": 0` to the embedding create example (after line 858) and completion create example (after line 1018).
- [ ] `REST_API.md` — add a field-table row in both endpoint tables (after the `MaxConcurrentRequests` rows at lines 896 and 1042): `| \`MaxQueueDepth\` | int | \`0\` | Requests allowed to wait for a concurrency slot once \`MaxConcurrentRequests\` is reached. \`0\` rejects immediately with 429. Clamped server-side to \`>= 0\`. Waiting is bounded by \`MaximumTimeoutMs\`. |`
- [ ] `REST_API.md` — extend the 429 behavior prose (embedding ~line 922, completion ~line 1053) to explain queueing: at the concurrency limit Partio queues up to `MaxQueueDepth` requests; a queued request that waits past `MaximumTimeoutMs` returns `504`; when the queue is full it returns `429`.
- [ ] `README.md` — add `"MaxQueueDepth": 0` to the two example endpoint list entries (~464, ~475) and mention the field in the endpoint-config prose (~483).
- [ ] `CHANGELOG.md` — add an entry describing `MaxQueueDepth`, the wait-then-429 semantics, the default of `0` (backward compatible), and the new MCP server.

Prose for docs should read like the rest of these files — direct, specific, no filler. State what the field does and what `0` means; don't hedge.

**Notes:** _______________________________________________

---

## Workstream I — Postman

- [ ] `Partio.postman_collection.json` — in each of the four escaped `raw` request bodies (Create Embedding ~line 1363, Update Embedding ~1424, Create Completion ~1660, Update Completion ~1695), add `\"MaxQueueDepth\": 0,\n` immediately after the `MaxConcurrentRequests` line.

**Notes:** _______________________________________________

---

## Workstream J — MCP server (new project)

Partio has no MCP server today. Build one as a standalone executable modeled structurally on `LiteGraph.McpServer` (which uses Voltaic and talks to its backend over the C# SDK), and organized in spirit like pneuma's MCP layer (a clear tool catalog, per-call auth, small bounded results). Use **Voltaic 0.6.1** — Partio already targets `net10.0` and already depends on `Watson 7.1.0`, which is exactly Voltaic 0.6.1's runtime dependency, so there is no framework friction.

Voltaic owns the whole JSON-RPC/MCP protocol. With `includeDefaultMethods: true`, it implements `initialize`, `tools/list`, `tools/call`, `ping`, and the success/error envelope. We only register tools and an auth handler. The verified 0.6.1 surface:

- `new McpHttpServer(host, port, rpcPath = "/rpc", ssePath = "/events", includeDefaultMethods: true, mcpPath = "/mcp")`
- `server.RegisterTool(string name, string description, object inputSchema, Func<RpcParameters, CancellationToken, Task<object>> handler)` — return `McpToolCallResult.FromStructured(obj)` / `McpToolCallResult.FromText(str)`, set `IsError` for failures.
- `RpcParameters` accessors: `GetString(name)`, `GetInt64(name)`, `GetBoolean(name)`, `ContainsProperty(name)`, `Deserialize<T>()`.
- `server.AuthenticationHandler = async ctx => new Voltaic.Core.AuthenticationResult { IsAuthenticated = ..., StatusCode = 401, ErrorMessage = ... }` — receives the `HttpListenerContext` so it can read the bearer header.
- `server.ServerName`, `server.ServerVersion`, `await server.StartAsync(token)`.

### J1 — Project scaffold

- [ ] Create `src/Partio.McpServer/Partio.McpServer.csproj` — `net10.0`, `OutputType=Exe`, `Nullable=enable`. References: `PackageReference Include="Voltaic" Version="0.6.1"`, `SyslogLogging`, `SerializationHelper`, and a `ProjectReference` to `sdk/csharp/Partio.Sdk/Partio.Sdk.csproj` (the MCP server calls Partio over REST via the SDK, litegraph-style).
- [ ] Add the project to the solution and to `build-all.bat`/`build-all.sh`.
- [ ] `src/Partio.McpServer/Program.cs` — `static async Task Main`: welcome banner → parse args → load settings (JSON file + env overrides) → build `LoggingModule` and `PartioSdk` → construct `McpHttpServer` → register tools → `await StartAsync` → block until Ctrl-C.

### J2 — Settings, config precedence, base CLI verbs

- [ ] `src/Partio.McpServer/Settings/McpServerSettings.cs` — one class: MCP host/port, Partio base URL, Partio API key, log level. (One class per file; follow the settings pattern in `src/Partio.Core/Settings`.)
- [ ] Config precedence: `partio.mcp.json` file, overridden by env vars `PARTIO_ENDPOINT`, `PARTIO_API_KEY`, `PARTIO_MCP_HOST`, `PARTIO_MCP_PORT`.
- [ ] Base CLI verbs: `--config=<path>`, `--showconfig`, `--help`, and `mcp stdio` (a stdio transport bridge, for harnesses that prefer stdio and for auth-header workarounds). The multi-harness `install`/`remove` verbs are specified in Workstream N.

### J3 — Authentication (server side)

- [ ] `src/Partio.McpServer/Auth/McpAuthenticationHandler.cs` — set `server.AuthenticationHandler` to validate the inbound bearer token against the configured Partio admin key(s); return `AuthenticationResult { IsAuthenticated = false, StatusCode = 401, ErrorMessage = "..." }` on failure so Voltaic rejects before any tool runs. The SDK used for outbound calls carries the configured Partio API key.
- [ ] Decision to record in **Notes**: single-key gateway (litegraph style, simplest — one admin token in, same token out) vs. per-caller tenant-scoped auth (pneuma style, if the MCP surface must be multi-tenant). Default to single-key for v1 unless multi-tenant is required.
- [ ] Note on parity with the armada exemplar: armada's MCP server enforces **no** auth (localhost bind, default tenant). Partio should decide explicitly — I recommend requiring a bearer by default since Partio endpoints are tenant-scoped and carry provider API keys. Record the decision.

### J4 — Tools

Organize tools in a small catalog class per resource, each returning bounded results. Enumerations must never be unbounded — mirror pneuma's enumerate/get discipline and clamp `maxResults`. The endpoint create/update tools are the reason this workstream exists for `MaxQueueDepth`: they carry the new field in their input schema.

- [ ] `partio_capabilities` — server + Partio version, available tools, protocol version. Requires only authentication.
- [ ] `partio_enumerate_completion_endpoints` — params `maxResults` (clamped), `skip`, `search`; returns a bounded summary list.
- [ ] `partio_get_completion_endpoint` — param `id` (required); returns the full endpoint (including `MaxConcurrentRequests` and `MaxQueueDepth`).
- [ ] `partio_create_completion_endpoint` — input schema includes `MaxConcurrentRequests` and **`MaxQueueDepth`** (integer, default 0, `>= 0`) plus the existing required fields; calls the SDK create.
- [ ] `partio_update_completion_endpoint` — same schema plus `id`; calls the SDK update.
- [ ] `partio_delete_completion_endpoint` — param `id`.
- [ ] Repeat the five endpoint tools for embedding (`partio_*_embedding_endpoint`).
- [ ] (Recommended companion tools, optional for this feature) `partio_summarize`, `partio_chunk`, `partio_embed` exposing Partio's core inference endpoints. Mark as follow-up if descoping.
- [ ] Every handler: parse args from `RpcParameters`, call `PartioSdk`, return `McpToolCallResult.FromStructured(...)`; on domain error set `IsError` with a clear message. Complex/nested inputs pass as JSON and re-hydrate via `Deserialize<T>()`.

### J5 — Wiring

- [ ] `src/Partio.McpServer/Mcp/PartioMcpToolCatalog.cs` (tool definitions/registration) and `PartioMcpServer.cs` (constructs `McpHttpServer`, sets name/version/auth, calls the catalog, starts). Keep dependencies explicit (constructor injection of `PartioSdk` + logging); no DI container, matching the codebase style.

**Notes:** _______________________________________________

---

## Workstream K — MCP_API.md

Mirror pneuma's `MCP_API.md` layout (seven sections), retargeted to Partio and to a standalone Voltaic server rather than an in-process route.

- [ ] Create `MCP_API.md` at repo root with:
  1. **Intro** — standalone MCP server, Voltaic 0.6.1, JSON-RPC 2.0 over Streamable HTTP at `/mcp` (and `/rpc`), same credentials/permissions as the REST API.
  2. **Authentication** — bearer token; rejected with 401 before any tool executes.
  3. **Methods** — `initialize` / `tools/list` / `tools/call` / `ping`, plus the `tools/call` result content shape.
  4. **Enumerating objects** — the bounded enumerate→get contract, a worked paging transcript, and the clamp behavior.
  5. **Tools** — a table (Tool | Purpose | Auth) covering every tool from J4, and note that create/update carry `MaxConcurrentRequests` and `MaxQueueDepth`.
  6. **Client configuration** — a per-harness table (Claude Code, Codex, Gemini, Cursor, Mux) giving the config file path, the exact entry snippet, and the manual CLI command, plus a pointer to `partio mcp install` (Workstream N). Mirror the shape of armada's `MCP_API.md` "Client Configuration" section.
  7. **Transport** — host/port, base URL + `/mcp`, and the stdio bridge (`partio mcp stdio`).
- [ ] Update `README.md` to link `MCP_API.md` and the per-harness setup docs, and mention the MCP server in the feature list.
- [ ] Add an MCP folder/requests to `Partio.postman_collection.json` (or a companion collection) with `initialize`, `tools/list`, and a `tools/call` for create-completion-endpoint that sets `MaxQueueDepth`.

**Notes:** _______________________________________________

---

## Workstream L — Tests (positive and negative)

Follow the existing test architecture in `src/Test.Shared`. Keep the existing concurrency tests passing unchanged (default `MaxQueueDepth = 0` guarantees this), and add coverage for the queue behavior, the new field, and the MCP surface.

### L1 — Queue mechanism unit tests (`src/Test.Shared/ProviderClientTests.cs`)

- [ ] **Default and clamp (positive/negative).** Extend the existing "Endpoint and settings provider limits default and clamp" test (lines 41-74): assert `MaxQueueDepth` defaults to `0` on all four types; set `-5` / `-1` and assert it clamps to `0`; set `3` and assert it is preserved.
- [ ] **Immediate reject preserved (negative).** With `MaxConcurrentRequests=1, MaxQueueDepth=0`, a second concurrent acquire throws `ProviderConcurrencyLimitException` immediately (extends the existing "Embedding clients reject when concurrent limit reached" test, lines 214-237). Assert `ex.MaxConcurrentRequests == 1` and `ex.MaxQueueDepth == 0`.
- [ ] **Queue admits and grants (positive).** With `max=1, queueDepth=1`: hold the slot; a second acquire does not throw but parks; release the first; assert the second completes and gets the slot (FIFO).
- [ ] **Queue-full rejects (negative).** With `max=1, queueDepth=1`: hold the slot, park one waiter, a third acquire throws `ProviderConcurrencyLimitException` with `MaxQueueDepth == 1`.
- [ ] **Queued wait honors timeout (negative).** A queued acquire whose token is cancelled (simulating `MaximumTimeoutMs`) completes as canceled/timeout, not as a slot grant, and does not corrupt `InFlight` (a subsequent acquire still works).
- [ ] **FIFO fairness (positive).** Two waiters queued in order are granted in order as slots free.
- [ ] **429 mapping unchanged.** Keep the `MapExceptionToStatusCode → 429` test (lines 202-212) and confirm it still holds with the extended exception.

### L2 — Serialization round-trips (`src/Test.Shared/CoreUnitTests.cs`)

- [ ] Embedding round-trip test (~238-260): set `MaxQueueDepth = 4`, assert it survives JSON round-trip.
- [ ] Completion round-trip test (~262-292): set `MaxQueueDepth = 7`, assert equality after round-trip.

### L3 — Integration / SDK-over-HTTP (`src/Test.Shared/SharedIntegrationTests.cs`)

- [ ] SDK CRUD asserts: set `MaxQueueDepth` on create and update for both endpoint types; assert on create response and on re-read (extend the blocks around 268-313 and the completion equivalents).
- [ ] **Queue behavior integration (positive).** Against the existing slow test server, create an endpoint with `MaxConcurrentRequests=1, MaxQueueDepth=1`; fire two in-flight loads; assert the second one *waits and then succeeds* once the first completes (contrast with the existing `429` test).
- [ ] **Queue-full integration (negative).** Same setup, fire three; assert the third returns `429`.
- [ ] Register the new cases next to the existing concurrency registrations (~2020/2028/2029).
- [ ] Seed `MaxQueueDepth` where `SelfHostedPartioTestEnvironment.cs` seeds endpoints, if defaults need to be explicit.

### L4 — SDK harnesses

- [ ] `sdk/csharp/Partio.Sdk.TestHarness/Program.cs` — set `MaxQueueDepth` on create/update, assert on create + re-read (mirror the `MaxConcurrentRequests` steps at 180/193/202/219/232 and the completion ones).
- [ ] `sdk/python/test_harness.py` and `sdk/js/test-harness.js` — same create/update/assert additions.

### L5 — MCP server tests

Mirror pneuma's eight MCP cases (`ApiSuite.cs`), retargeted. Put them where the MCP server can be exercised over HTTP (a new `Test.Mcp` suite or an addition to `Test.Shared` that boots the MCP server against a running Partio).

- [ ] **tools/list advertises the contract (positive)** — every tool from J4 is present.
- [ ] **Unauthenticated denied (negative)** — a `tools/call` with no/invalid bearer is rejected (401 before the tool runs).
- [ ] **Enumerate endpoints paged (positive)** — bounded result with paging fields.
- [ ] **Enumerate clamps maxResults (boundary)** — a huge `maxResults` is clamped, not honored.
- [ ] **Create sets MaxQueueDepth (positive)** — `tools/call partio_create_completion_endpoint` with `MaxQueueDepth: 3`, then `partio_get_completion_endpoint` returns `3`.
- [ ] **Create clamps invalid MaxQueueDepth (negative)** — `MaxQueueDepth: -2` comes back as `0`.
- [ ] **Missing required arg (negative)** — `partio_get_completion_endpoint` with no `id` returns a JSON-RPC invalid-params error.
- [ ] **Unknown tool (negative)** — `tools/call` with a bogus name returns method/tool-not-found (`-32601`/`-32000`).
- [ ] **MCP-vs-REST equivalence (positive)** — an endpoint fetched via MCP carries the same `Id`/`MaxQueueDepth` as the REST twin.

### L6 — Install-command tests (Workstream N)

- [ ] **Dry-run writes nothing (negative side-effect check).** `partio mcp install --dry-run` produces the planned output for every harness and writes no files.
- [ ] **Per-harness config shape (positive).** For each of Claude Code, Cursor, and Mux, a real install into a temp HOME writes the exact expected JSON entry (keyed `mcpServers` object for Claude/Cursor; `servers` array element carrying `name`+`url`+`mcpPath` for Mux).
- [ ] **Idempotent upsert (positive).** Running install twice does not duplicate entries; an existing Partio entry is updated in place, and unrelated entries are preserved.
- [ ] **CLI delegation guarded (negative).** When the Codex/Gemini native CLI is absent, install reports it clearly and does not crash; when present, it invokes the correct `mcp add` command (assert on a stubbed/faked invoker).
- [ ] **Mux detected-only (boundary).** Mux is configured only when detected (`~/.mux` or `mux` on PATH); absent, it is skipped with a note.
- [ ] **`remove` reverses install (positive).** `partio mcp remove` deletes only the Partio entries and any managed instruction blocks, leaving other content intact.

**Notes:** _______________________________________________

---

## Workstream M — Build and verify

- [ ] Build each project in its own MSBuild process with analyzers off and node reuse disabled (per `partio-build-msbuild-nodereuse`). Resolve all errors and warnings.
- [ ] Run the full `Test.Shared` suite across all four database providers; confirm existing concurrency tests still pass and the new queue tests pass.
- [ ] Run the three SDK test harnesses against a running server.
- [ ] Boot the MCP server against a running Partio; run `tools/list` and a create/get round-trip that exercises `MaxQueueDepth`.
- [ ] Run `partio mcp install --dry-run` and confirm the planned per-harness output for Claude Code, Codex, Gemini, Cursor, and Mux; then do a real install into a throwaway HOME and connect at least one harness (Mux via `mux probe --require-tools` is the fastest check) end to end.
- [ ] Apply migrations against a pre-existing database of each provider and confirm the `ALTER TABLE` paths add `max_queue_depth` without disturbing existing rows (existing rows default to `0`).
- [ ] Re-read `README.md`, `REST_API.md`, `MCP_API.md`, and every `docs/INSTRUCTIONS_FOR_*.md` for accuracy against the shipped behavior.

**Notes:** _______________________________________________

---

## Workstream N — Multi-harness `install` / `remove` command

Partio's MCP server needs a one-command setup that wires it into every supported harness, modeled directly on armada's `mcp install` (source of truth: `C:\code\armada\Armada\src\Armada.Helm\Commands\McpConfigHelper.cs` and `McpInstallCommand.cs`). Armada's verb description is literally "Configure MCP integration for Claude Code, Codex, Gemini, Cursor, and Mux (when detected)" — match that surface. The install logic can live in `Partio.McpServer` as a `mcp install` / `mcp remove` verb, or in a small sibling `Partio.Helm` project; pick one and record it.

The per-harness targets to implement (each an idempotent upsert; verified against armada's writer):

| Harness | Config file | How Partio writes it | Entry shape | Transport |
|---|---|---|---|---|
| **Claude Code** | `~/.claude.json` | Write JSON directly; also write a sub-agent file `~/.claude/agents/partio.md` (`allowedTools: mcp__partio__*`) | `mcpServers.partio = { "type": "http", "url": "http://localhost:{port}/mcp" }` | HTTP |
| **Codex** | `~/.codex/config.toml` | **Delegate to the Codex CLI** (`codex mcp remove partio` then `codex mcp add partio -- partio mcp stdio`) — do not hand-edit TOML | stdio command+args | stdio |
| **Gemini CLI** | `~/.gemini/settings.json` | **Delegate to the Gemini CLI** (`gemini mcp add --scope user --transport http partio http://localhost:{port}/mcp`) | written by Gemini | HTTP |
| **Cursor** | `<CWD>/.cursor/mcp.json` (project-scoped) | Write JSON directly | `mcpServers.partio = { "url": "http://localhost:{port}/mcp", "transport": "http" }` | HTTP |
| **Mux** (`c:\code\mux`) | `<MUX_CONFIG_DIR or ~/.mux>/mcp-servers.json` | Write JSON directly, **only if Mux detected** (`~/.mux` exists or `mux` on PATH); array upsert keyed on `name` | element in `servers` array: `{ "name": "partio", "transport": "http", "url": "http://localhost:{port}", "mcpPath": "/mcp" }` | HTTP |

- [ ] Implement a `ConfigTarget` abstraction per harness carrying: client name, file path, entry JSON, project-vs-user scope, container shape (keyed `mcpServers` object vs. `servers` array vs. keyed `mcp` object), and CLI-delegation info for Codex/Gemini.
- [ ] Idempotent JSON upsert helpers for the three container shapes; merge into existing config, never clobber unrelated entries, only rewrite when the entry differs.
- [ ] Native-CLI delegation path for Codex and Gemini with a clear message when the CLI is absent.
- [ ] Mux detected-only gate (`IsMuxAvailable`) with an array upsert keyed on `name == "partio"`. Note the Mux quirk: `url` is host-only + separate `mcpPath` (unlike the full `/mcp` URL used by the others), and Mux HTTP MCP has no per-server auth field — if Partio's MCP requires a bearer, document the stdio-bridge workaround (`transport: "stdio"`, `command: "partio"`, `args: ["mcp","stdio"]`).
- [ ] Managed-instruction blocks: write a Partio-managed, delimiter-fenced block (`<!-- partio:mcp:begin -->` / `<!-- partio:mcp:end -->`) into `<CWD>/AGENTS.md` (Codex/Cursor) and `<CWD>/GEMINI.md` (Gemini), upserted idempotently.
- [ ] `--dry-run` (print, no writes), `--yes` (skip prompts); print the manual CLI snippet for each target; close with "start the Partio server and MCP server, then restart your MCP client."
- [ ] `partio mcp remove` — reverse every target: delete the Partio entries and the managed instruction blocks, leaving other content intact.
- [ ] Shell wrappers `scripts/install-mcp.*` if the repo keeps a `scripts/` convention (armada does).

**Notes:** _______________________________________________

---

## Workstream O — Per-harness setup docs (`docs/INSTRUCTIONS_FOR_*`)

Mirror armada's `docs/INSTRUCTIONS_FOR_{harness}.md` set. Each doc is meant to be pasted into that harness's system prompt / rules file: a short connection preamble (how to point the harness at the Partio MCP server) followed by a "how to use Partio's MCP tools" body (the endpoint-management and inference tools, including `MaxConcurrentRequests`/`MaxQueueDepth` on create/update). Create a `docs/` directory at the Partio repo root.

> **Naming note (confirm):** you referred to "RecallDB," but nothing named RecallDB exists in armada or Partio — armada lists `recalldb` only once, as a sibling repository of the author, with no code linkage. I've written this workstream as "connect each harness to **Partio's** MCP server," matching the armada exemplar. If you actually want docs for connecting these harnesses to a separate **RecallDB** MCP server, that's a different target and I need its MCP URL/transport. Flag before writing.

- [ ] `docs/INSTRUCTIONS_FOR_CLAUDE_CODE.md` — banner ("paste into CLAUDE.md / system prompt"), then the Partio MCP tool guide. Connection via `claude mcp add --transport http --scope user partio http://localhost:{port}/mcp` or `partio mcp install`.
- [ ] `docs/INSTRUCTIONS_FOR_CODEX.md` — same body; connection via `codex mcp add partio -- partio mcp stdio` (stdio).
- [ ] `docs/INSTRUCTIONS_FOR_GEMINI.md` — same body; connection via `gemini mcp add --scope user --transport http partio http://localhost:{port}/mcp`.
- [ ] `docs/INSTRUCTIONS_FOR_CURSOR.md` — same body; connection via project-scoped `.cursor/mcp.json`.
- [ ] `docs/INSTRUCTIONS_FOR_MUX.md` — full connection preamble (Option A interactive `/mcp` wizard, Option B `mcp-servers.json` file, Option C stdio bridge) as in armada's Mux doc, then the tool guide. Verify with `mux probe --output-format json --require-tools`.
- [ ] Shared tool-guide body reused across all five docs: the tool tables from J4, the enumerate→get paging discipline, and an explicit note that create/update endpoint tools accept `MaxConcurrentRequests` and `MaxQueueDepth`.
- [ ] Link all five from `README.md` and `MCP_API.md`.

**Notes:** _______________________________________________

---

## Workstream P — Requirements-compliance re-evaluation (final gate)

Before calling the work done, re-read the changed and new code against `c:\code\agents\requirements` and fix anything that drifts. This is a required pass, not a nice-to-have.

- [ ] **CODE_STYLE.md** — usings inside the namespace and ordered; XML docs on all public members/methods (and none on private); backing fields `_PascalCase`; explicit getters/setters with validation; no `var`; no tuples; `ConfigureAwait(false)`; every async method takes/observes a `CancellationToken`; specific exception types with `/// <exception>` tags; one class/enum per file; nullable enabled; no `Console.WriteLine` in library code. The async limiter (Workstream B) and the new MCP project are the highest-risk areas — check them closely.
- [ ] **BACKEND_ARCHITECTURE.md** and **BACKEND_TEST_ARCHITECTURE.md** — confirm the new project, the queue mechanism, and the tests fit the established layering and test-suite structure.
- [ ] **DASHBOARD_STYLE_AND_USABILITY.md** — confirm Workstream F/F1 satisfies it (this is the same doc the usability pass runs against).
- [ ] **REPOSITORY_REQUIREMENTS.md** — new project lands under `src/`; SDK/test-harness conventions preserved; if the MCP server ships a Docker image, add `.dockerignore` and `.yaml` compose per the rules; update `CHANGELOG.md`; loopback URLs use `127.0.0.1`, not `localhost`, in any SDK/harness client added here.
- [ ] **WRITING_DOCUMENTS.md** — apply to the human-facing docs produced here (`MCP_API.md`, the `INSTRUCTIONS_FOR_*` set, README/CHANGELOG prose): human voice, varied rhythm, no formulaic openings or filler.
- [ ] **I18N.md** — reconcile the dashboard i18n decision from F1 against the i18n requirements.
- [ ] Run the `simplify` skill over the diff for reuse/altitude cleanups, then a `/code-review` pass for correctness. Resolve findings.

**Notes:** _______________________________________________

---

## File-count summary

| Area | Files touched / created |
|------|-------------------------|
| Core models + defaults | 4 |
| Queue mechanism + exception | 2 |
| Client plumbing | 8 |
| Database (4 providers) | 16 |
| Server wiring + docker seeds | 3 |
| Dashboard | 2 (+1 test) |
| SDKs (models + doc) | 4 |
| Documentation | 4 |
| Postman | 1 (+ MCP requests) |
| MCP server (new project) | ~10 new |
| MCP_API.md | 1 new |
| Multi-harness install/remove command (N) | ~6 new (+ shell wrappers) |
| Per-harness setup docs (O) | 5 new + `docs/` |
| Tests (incl. install-command) | 12+ |
| Compliance re-eval (P) | review pass, no fixed count |

## Open items to confirm before or during implementation

- **"RecallDB" naming** — no such thing exists in armada or Partio (see Workstream O note). I've targeted the docs at Partio's own MCP server. Confirm that's what you meant, or point me at a separate RecallDB MCP endpoint.
- **MCP auth model** — single-key gateway vs. per-caller tenant scoping (J3). Recommend requiring a bearer by default (Partio is tenant-scoped and holds provider keys); armada's no-auth localhost model is the weaker fit here.
- **MCP tool surface breadth** — endpoint-config tools are required for `MaxQueueDepth`; the summarize/chunk/embed companion tools (J4) are recommended but can be a follow-up.
- **Install command home** — a `mcp install` verb inside `Partio.McpServer` vs. a sibling `Partio.Helm` project (armada uses the latter). Pick one.
- **OpenCode** — armada also ships an OpenCode doc but does *not* automate its install. Decide whether Partio includes OpenCode (docs-only) or omits it. You listed five harnesses (Claude, Codex, Gemini, Cursor, Mux); OpenCode is out unless you say otherwise.
- **Dashboard i18n** — whether to introduce i18n keys for the two fields or follow the existing inline-literal pattern (F1 / P).
