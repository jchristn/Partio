# Telemetry Plan

Closing the observability gap between Partio and the family telemetry standard.

Status: **implemented** on branch `feature/v0.5.0` · Target: v0.5.0 · Owner: _TBD_

---

## 0. Implementation status (2026-08-26)

All eight phases are implemented and **verified against a running stack**. Per-project builds are clean
(0 warnings / 0 errors), **136/136** backend unit tests pass, **9/9** frontend tests pass, and the
frontend production build succeeds.

A full `docker compose up` run confirmed end-to-end (all 8 services healthy):
- **Prometheus** scrapes both jobs (`partio-server-watson`, `partio-server-app`) `up`; live series observed
  for HTTP (`http_server_request_duration_seconds`, `watson_server_up`) and every app family exercised —
  `partio_process_total{operation="chunk"}`, `partio_process_stage_total{stage="chunk"}`,
  `partio_integration_requests_total{service="ollama",operation="embedding",outcome="error"}`,
  `partio_authz_decisions_total{permit=2,deny=1}`, `partio_endpoints_healthy{embedding=1,completion=1}`.
- **Tempo** holds correctly-nested traces: a chunk request produced one trace `POST /v1.0/chunk → process
  → stage:chunk`, an embed produced `POST /v1.0/embed → ollama embedding`, and background
  `health_check:*` spans flow — all under `service.name=partio-server`.
- **Loki** ingests Partio logs (`service_name=partio`) via the OTLP collector.
- **Grafana** provisioned all three datasources (`prometheus`/`tempo`/`loki`) and all five dashboards in
  the `Partio` folder.

Live testing also **caught and fixed a real instrumentation gap**: provider calls run through PolyPrompt
SDK clients (`client.ChatAsync`/`client.EmbedAsync`), which bypassed the initially-instrumented
`PostAndRecordAsync`. Integration metrics/spans were moved to the six concrete provider methods; a
rebuilt image confirmed both `embedding` and `completion` integrations now record end-to-end.

| Phase | Status | Notes |
| --- | --- | --- |
| 0 — Decisions & scope | ✅ done | Collection spike resolved (see below); Loki included. |
| 1 — Backend foundation | ✅ done | `TelemetrySettings`, Watson telemetry on, `PartioMetrics`, `PartioTelemetry`, `TelemetryService`, `/v1.0/metrics`, authz + disposal. |
| 2 — Processing pipeline | ✅ done | Operation + per-stage scopes (`tokenize`/`chunk`/`summarize`/`embed`) in engines and all 5 handlers. |
| 3 — Integrations | ✅ done | Counter/duration/spans in both provider base clients; `ServiceName` set from `ApiFormat` (vLLM ≠ OpenAI); `rejected` on 429. |
| 4 — Background/health/authz | ✅ done | Health-check counter+duration+span, healthy/unhealthy gauges, model-load, request-history write/cleanup, authz. |
| 5 — Observability stack | ✅ done | Prometheus, Tempo (`2.6.1`), Loki, OTLP collector, Grafana in `compose.yaml` + configs; `127.0.0.1`-bound, healthchecked. Partio images pinned to `v0.5.0`; `docker/update.bat` added (pull → down → up -d → ps -a). |
| 6 — Grafana dashboards | ✅ done | Provisioning (UIDs `prometheus`/`tempo`/`loki`) + 5 domain dashboards in `assets/grafana/`. |
| 7 — Frontend card | ✅ done | `ExternalServicesCard` on the home page + vitest coverage. |
| 8 — Tests, docs, build | ✅ done | ~28 unit cases + 1 integration case, `TELEMETRY.md`, CHANGELOG, Postman folder. |

### Resolved decisions

- **Collection path (was the Phase-1 spike).** Chose a **zero-bridging hybrid** rather than routing
  metrics through Radiant: Watson serves its HTTP/`watson.*` metrics on its own in-process Prometheus
  endpoint at `/metrics`; the application serves `partio_*` at `/v1.0/metrics`; Prometheus scrapes both.
  Radiant is used for **traces only** (subscribing to the `Watson` and `Partio` activity sources →
  Tempo). Every piece is independently proven, and no metric re-bucketing/bridging is needed.
- **App spans use a BCL `ActivitySource`** (`PartioTelemetry`, dependency-free in Core) rather than a
  Radiant span wrapper — `StartActivity` returns `null` when nothing listens, giving the required
  best-effort behavior natively and keeping `Partio.Core` free of a telemetry dependency.
- **Loki is in for v1**, via the Conductor pattern (OTLP collector `filelog` receiver tailing Partio's
  rolling logs → Loki) — no application logging changes.
- **HTTP metrics are Watson's, not re-implemented** — the HTTP dashboard reads `http_server_*`; there is
  deliberately no `partio_http_*` family.

### Minor deviations from the plan as written

- The embedding integration `operation` label is always `embedding` (the plan's speculative `probe`
  value was dropped to avoid free-form/high-cardinality `purpose` values leaking into a metric label).
- The explorer endpoints (`/v1.0/explorer/*`) get integration metrics for their downstream calls but no
  top-level `partio_process_*` operation metric — they are diagnostic tools, not processing operations.
- `partio_uptime_seconds` counts from first metrics access, not process start; it is supplementary, and
  the Overview dashboard uses Watson's accurate `watson_server_uptime_seconds`.

### What's left / recommended follow-up

None of these block the feature; they are verification and polish steps.

1. **Run the full stack once** (`cd docker && docker compose up -d --build`) and confirm, against real
   traffic: Prometheus shows both scrape targets `up`, the five dashboards populate in the `Partio`
   folder, a `/v1.0/process` call produces a `process`→`stage:*`→provider trace in Tempo, and Partio log
   lines appear in Loki. The dashboards and Loki pipeline are wired and JSON-valid but have not yet been
   eyeballed against live data.
2. **Run the integration suite end-to-end** (self-hosted or against a running server) so the new
   "Metrics endpoints expose partio_ and watson_ families" case executes in the harness — only the unit
   suites and a manual smoke test have run here. Also run the **NUnit** and **Test.Automated** runners
   (only **xUnit** was exercised).
3. **Optional: a Playwright e2e** asserting the External Services card renders and links open, to match
   the existing `dashboard/tests/` coverage.
4. **Optional polish**: the Postman collection re-serialized with a large (functional, importable) diff —
   re-generate or hand-trim if a minimal diff is wanted. Consider eager-initializing
   `partio_uptime_seconds` at process start if process-accurate app uptime is ever needed.
5. **Publish the `v0.5.0` images**: `compose.yaml` now pins `jchristn77/partio-server:v0.5.0` and
   `jchristn77/partio-dashboard:v0.5.0`, and `docker/update.bat` runs `docker compose pull` — both fail
   until those two tags are pushed to Docker Hub (the observability images pull fine). Build and push the
   images as part of the release, or use a local `build`/tag for pre-release testing.
6. **Before any shared deployment**: change Grafana's admin credentials out of band and keep
   Prometheus/Tempo/Loki and the two `/metrics` endpoints off public interfaces (see §Production notes).
7. **Commit / PR**: changes are staged on `feature/v0.5.0` but **not committed** — squash-or-commit and
   open a PR when ready.

---

## 1. Why this document exists

`C:\Code\agents\requirements\TELEMETRY_REQUIREMENTS.md` is normative for every service
in this family: a finished service emits **metrics and traces from the first commit**,
exposes them over standard endpoints, collects them into a local observability stack that
comes up with `docker compose up`, renders them in per-domain Grafana dashboards, and links
operators to those tools from the product's own home page.

Partio today has **none of that**. This plan inventories the gap concretely and lays out a
phased path to close it, leaning on the two reference implementations the requirements point
to:

- **`C:\Code\Pneuma`** — the named reference for the stack, dashboards, and app-level
  instrumentation. Partio mirrors its `*Metrics` registry, `TelemetryService`/Radiant span
  wrapper, `TelemetrySettings`, docker stack, Grafana provisioning, and external-services card.
- **`C:\Code\Conductor`** — a second reference that adds what Pneuma omits: a **logs/Loki**
  pipeline (via an OTLP collector's `filelog` receiver), **background-worker gauges**
  (healthy/unhealthy endpoint counts), and a browser-host-relative external-services card.

Where the requirements and Pneuma diverge, this plan calls it out and picks a lane (see §3).

---

## 2. Current state (the gap)

A repo-wide search for `Meter`, `ActivitySource`, `Telemetry`, `OpenTelemetry`, `Radiant`,
`Prometheus`, `/metrics`, `StartActivity`, `Activity.Current` returns **zero matches** in
`src/`. The observability primitives that exist are text logging (`SyslogLogging`), JSON
request-history persistence, and health endpoints — none of them metrics or traces.

| Area | Requirement | Partio today | Gap |
|---|---|---|---|
| Watson built-in telemetry | `Settings.Telemetry.Enable` on, collector subscribed | Watson **7.1.0** present, `WebserverSettings.Telemetry` **never touched** | **Total** |
| App metrics registry | Product-prefixed `Meter`/registry with typed `Record*` methods | none | **Total** |
| App traces | `ActivitySource`/Radiant spans, nullable + best-effort | none | **Total** |
| `/metrics` endpoint | Prometheus exposition | absent | **Total** |
| `TelemetrySettings` | POCO on root settings + JSON section | no telemetry keys in `ServerSettings.cs` / `docker/partio.json` | **Total** |
| Pipeline instrumentation | per-stage counter + duration histogram + span | chunk/embed/summarize timed only ad-hoc via `Stopwatch` into request history | **Total** |
| Integration instrumentation | per-call counter + duration + span by service/operation | provider calls capture "call detail" but no metrics/traces | **Total** |
| Background-work signals | job/health/cleanup counters + gauges | background services run uninstrumented | **Total** |
| Observability stack | Prometheus + Tempo + Grafana (+ Loki) in `compose.yaml`, healthchecked, one command | `compose.yaml` has only `partio-server`, `partio-dashboard`, `ollama`; no healthchecks; `latest` tags | **Total** |
| Grafana provisioning | datasources + per-domain dashboards as code | no `assets/grafana/`, no provisioning | **Total** |
| Home-page external-services card | links to Grafana/Prometheus/Tempo with URLs + creds | absent from the React dashboard | **Total** |

The gap is effectively "add the entire telemetry subsystem." The work below is sequenced so
each phase is independently shippable and testable.

### 2.1 What Partio actually does (so the domains are right)

Partio is a multi-tenant chunking + embedding gateway. Its instrumentable surface:

- **HTTP** — Watson routes: `/v1.0/process`, `/process/batch`, `/chunk`, `/embed`,
  `/summarize`, `/explorer/*`, tenant/user/credential/endpoint CRUD, request-history,
  health. (Registered in `src/Partio.Server/PartioServer.cs`.)
- **Processing pipeline** — the core workflow: resolve tokenization → chunk → (optional)
  summarize → embed. Engines in `src/Partio.Core/Chunking/ChunkingEngine.cs`,
  `Summarization/SummarizationEngine.cs`, `Tokenization/`. Partio **already persists
  per-stage timing** into request history — the same stage boundaries become metric labels.
- **Outbound integrations** — provider calls to **Ollama, OpenAI, Gemini, vLLM/OpenAI-compatible**
  via `src/Partio.Core/ThirdParty/CompletionClientBase.cs` and `EmbeddingClientBase.cs`
  (central call site `PostAndRecordAsync`), gated by `ProviderConcurrencyLimiter.cs` (429 on
  saturation → a **`queued`/`rejected`** state to record).
- **Background work** — `RequestHistoryCleanupService` (scheduled), `EmbeddingHealthCheckService`
  / `CompletionHealthCheckService` / `SharedHealthCheckCoordinator` (long-running pollers),
  `ModelLoadService`. Background work is the requirements' trigger for shipping **logs to Loki**.
- **Authorization** — bearer-token auth in `AuthenticationService`.

---

## 3. Target architecture and the one real decision

The requirements say two things that Pneuma does **not** both do:

1. **"Confirm Watson's built-in telemetry is enabled and subscribe a collector to the
   `Watson` meter and activity source."** Watson 7.1 emits the whole HTTP surface —
   `http.server.request.duration`, `watson.*` connection/route/auth/websocket metrics, and one
   server span per request that adopts an inbound `traceparent` — for free.
2. **"Match Pneuma"** for app-level instrumentation. But Pneuma **bypasses** Watson's built-in
   telemetry: it hand-rolls `pneuma_http_*` in a `PostRouting` hook and a dependency-free
   Prometheus-text registry, using Radiant only for app traces.

**Decision: take the requirements-literal path for HTTP, Pneuma's structure for the app layer.**
Partio is greenfield on telemetry and already on Watson 7.1 — hand-rolling HTTP metrics would
throw away the `watson.*` connection/auth/route/websocket surface the requirements explicitly
list as delivered out of the box. So:

- **HTTP layer** → **enable Watson's built-in telemetry** (`Settings.Telemetry.Enable/EnableMetrics/EnableTraces/PropagateContext`).
  HTTP metrics use the OTel semantic-convention names (`http_server_request_duration_seconds`,
  `watson_server_up`) — dashboards read those, **not** a `partio_http_*` re-implementation.
- **Application layer** → a **`PartioMetrics`** registry and a **`PartioTelemetry`/`TelemetryService`**
  span wrapper, modeled directly on Pneuma's `PneumaMetrics` + `TelemetryService`. All app
  families are prefixed **`partio_`**. App spans nest under Watson's server span automatically
  because Watson has already set `Activity.Current`.
- **Collection** → a **Radiant** host (`RadiantHost.Start`) subscribed to **both** the `Watson`
  and `Partio` meters/activity-sources, exporting **traces → Tempo (OTLP)** and **metrics → Prometheus**.

> **Phase 1 checkpoint — collection mechanism.** Pneuma uses Radiant for traces only and
> hand-serves app metrics at `/metrics`; it does not route metrics through a collector. Before
> building, confirm which the installed **Radiant** version supports:
>
> - **Path A (preferred, requirements-canonical):** Radiant subscribes `AddMeter("Watson")` +
>   `AddMeter("Partio")` and exports metrics to Prometheus (scrape or push). One collector, both
>   layers. This is the exact wiring shown in `TELEMETRY_REQUIREMENTS.md` §"Enabling and collecting".
> - **Path B (Pneuma-proven fallback):** enable **Watson's in-process Prometheus endpoint**
>   (`Settings.Telemetry.Prometheus.Enable = true`, `Path = "/metrics"`) for the `watson.*` + HTTP
>   metrics, **and** serve a Pneuma-style `PartioMetrics.Render()` text registry for `partio_*`
>   families (either merged into the same `/metrics` output or scraped as a second target). Radiant
>   still handles all traces → Tempo.
>
> Both paths yield the same metric families and identical dashboards; only the plumbing differs.
> Default to Path A; fall back to Path B if Radiant's metric export is unavailable in the pinned
> version. Resolve this spike first — everything downstream is agnostic to the outcome.

Loopback defaults are **`127.0.0.1`**, never `localhost` (per `REPOSITORY_REQUIREMENTS.md`: on
Windows `localhost` resolves `::1` first and stalls). In `docker/partio.json` the OTLP endpoint
points at the `tempo` service by container name.

---

## 4. Metric and span inventory (the contract dashboards depend on)

Low-cardinality labels only — **no ids, no free-form input, no raw paths** on metric labels
(those belong on spans). Derive p50/p95/p99 in Grafana from `_bucket` series. Enforce the
route-template rule on any HTTP-adjacent app label yourself.

### 4.1 HTTP — from Watson (do not re-implement)

`http_server_request_duration_seconds{http_request_method,http_route,http_response_status_code}`,
`watson_server_up`, `watson_server_uptime_seconds`, `watson.connections.*`, `watson.route.match|unmatched`,
`watson.auth.requests`, plus request/response body-size histograms — all emitted by Watson once
telemetry is enabled and a collector subscribes.

### 4.2 Application families (`partio_*`, defined in `PartioMetrics`)

| Domain | Family | Type | Labels |
|---|---|---|---|
| Processing | `partio_process_total` | counter | `operation` (`chunk`/`embed`/`summarize`/`process`/`process_batch`), `outcome` (`ok`/`error`/`cancelled`) |
| Processing | `partio_process_duration_seconds` | histogram | `operation` |
| Processing | `partio_process_stage_total` | counter | `stage` (`tokenize`/`chunk`/`summarize`/`embed`/`queued`), `outcome` |
| Processing | `partio_process_stage_duration_seconds` | histogram | `stage` |
| Integrations | `partio_integration_requests_total` | counter | `service` (`ollama`/`openai`/`gemini`/`vllm`), `operation` (`embedding`/`completion`/`probe`), `outcome` (`ok`/`error`/`cancelled`/`rejected`) |
| Integrations | `partio_integration_request_duration_seconds` | histogram | `service`, `operation` |
| Endpoint health | `partio_endpoint_health_checks_total` | counter | `kind` (`embedding`/`completion`), `outcome` (`healthy`/`unhealthy`) |
| Endpoint health | `partio_endpoints_healthy` / `partio_endpoints_unhealthy` | gauge (observable) | `kind` |
| Endpoint health | `partio_endpoint_health_check_duration_seconds` | histogram | `kind` |
| Model load | `partio_model_load_total` | counter | `kind`, `outcome` |
| Model load | `partio_model_load_duration_seconds` | histogram | `kind` |
| Request history | `partio_request_history_writes_total` | counter | `outcome` |
| Request history | `partio_request_history_cleanup_total` | counter | `outcome` |
| Authz | `partio_authz_decisions_total` | counter | `result` (`permit`/`deny`) |
| Uptime | `partio_uptime_seconds` | gauge | — (or lean on `watson_server_uptime_seconds`) |

`operation`/`stage`/`service`/`kind`/`outcome`/`result` are all closed enumerations — cardinality
stays bounded. Tenant/endpoint ids are **span attributes, never metric labels**.

### 4.3 Spans (`PartioTelemetry` ActivitySource, nested under Watson's server span)

- **Processing root** — `process` (kind Server/Internal) per `/process*` call; tags
  `partio.operation`, `partio.tenant.id`, `partio.embedding.endpoint`, `partio.completion.endpoint`.
  Child spans `stage:tokenize`, `stage:chunk`, `stage:summarize`, `stage:embed`.
- **Integration** — `"<service> <operation>"` (kind Client), tags `partio.service`,
  `partio.operation`, `partio.endpoint.id` (high-cardinality is fine on a span). This is what
  resolves a slow embed to the exact Ollama/OpenAI call — and it nests in the request's trace.
- **Background** — health-check and cleanup spans (kind Consumer/Internal).

Set span status explicitly (`SetError`/`SetOk`) so Tempo's error filtering works; attach
exceptions as span events. Instrumentation is **best-effort**: `StartSpan` returns a nullable span,
callers use `using (_Telemetry?.StartSpan(...))`, and a Radiant init failure logs a warning and
runs without traces — it never affects request handling.

---

## 5. Phased plan

> Legend: `[x]` implemented and verified · `[x] *` implemented, live-stack verification pending
> (see §0 follow-up 1) · `[ ]` not done (annotated).

### Phase 0 — Decisions & scope
- [x] Resolve the **Path A vs Path B** collection spike (§3) → **zero-bridging hybrid** (Watson `/metrics`
      + app `/v1.0/metrics` scraped; Radiant traces-only). Radiant 0.1.2 confirmed present.
- [x] Confirm **Loki is in scope** for v1 → yes (background work); implemented via the OTLP collector.
- [x] Confirm free host ports 3000/9090/3200/3100 — no conflict; all observability ports bound to `127.0.0.1`.

### Phase 1 — Backend foundation
Closes: Watson telemetry, `TelemetrySettings`, collector, `/metrics`, `PartioMetrics`/`TelemetryService` skeletons.

- [x] Add `TelemetrySettings` POCO under `src/Partio.Core/Settings/`, mounted on `ServerSettings.cs` as
      `Telemetry`; `Telemetry` section added to `docker/partio.json` and `docker/factory/partio.json`
      (`OtlpEndpoint: http://tempo:4317`). _(Test harness telemetry is not forced off — with no reachable
      collector the Radiant host fails gracefully; the telemetry unit tests set `Enabled=false` explicitly.)_
- [x] `PartioServer.cs` **enables Watson built-in telemetry** (`Enable`, `EnableMetrics`, `EnableTraces`,
      `PropagateContext`) and Watson's in-process Prometheus endpoint at `/metrics`.
- [x] `PartioMetrics` (static, dependency-free registry) under `src/Partio.Core/Observability/` with typed
      `Record*` methods for every family in §4.2, `Render()`, escaped labels, and hard-coded `# HELP`/`# TYPE`.
- [x] `TelemetryService` (Radiant host) under `src/Partio.Server/Services/`, subscribing the `Watson` +
      `Partio` **activity sources**, best-effort, `IDisposable`, disposed on shutdown. _(Design change from
      the plan: app spans use a BCL `ActivitySource` (`PartioTelemetry`) rather than a `StartSpan`/`RecordRequest`
      wrapper — see §0.)_
- [x] Expose metrics (Path B): anonymous `GET /v1.0/metrics` returning `PartioMetrics.Render()` as
      `text/plain; version=0.0.4`, registered pre-authentication; Watson serves its own at `/metrics`.
- [x] **Acceptance (verified live):** `GET /v1.0/metrics` returns `partio_*`, `GET /metrics` returns
      `http_server_*`/`watson_*`, and requests produce Watson server spans in Tempo under `service.name=partio-server`.

### Phase 2 — Instrument the processing pipeline
Closes: `partio_process_*` families + processing spans.

- [x] `ProcessOperationScope` opens a `process` root span + records `partio_process_*` across all five
      handlers (`ChunkOnly`, `EmbedTexts`, `SummarizeText`, `ProcessSingle`, `ProcessBatch`).
- [x] `ProcessStageScope` wraps each stage (`tokenize` in the resolver, `chunk` in `ChunkingEngine`,
      `summarize` in `SummarizationEngine`, `embed` at the two orchestration sites). _(The `queued`
      concurrency-wait state is surfaced instead as the integration `rejected` outcome on 429; a distinct
      `queued` stage sample was not added.)_
- [x] **Acceptance (verified live):** a `/v1.0/chunk` request produced one Tempo trace
      `POST /v1.0/chunk → process → stage:chunk`, and `partio_process_total`/`partio_process_stage_total`
      appeared in Prometheus.

### Phase 3 — Instrument outbound integrations
Closes: `partio_integration_*` families + integration spans.

- [x] `IntegrationScope` wraps the actual provider call in the **six concrete SDK-path methods**
      (`GenerateCompletionAsync` ×3, `EmbedBatchAsync` ×3 — the `client.ChatAsync`/`client.EmbedAsync`
      calls): `<service> <operation>` client span + `RecordIntegration(...)`, with `rejected` on the
      concurrency exception. _(Live testing revealed the initial instrumentation on
      `PostAndRecordAsync` only caught the secondary probe/model-load path — the PolyPrompt SDK clients
      bypass it — so it was moved to the real call sites. `PostAndRecordAsync` is no longer scoped;
      model-load has its own `partio_model_load_*` family.)_
- [x] `service` set by the client factories from `ApiFormat` (`ollama`/`openai`/`gemini`/`vllm`, vLLM
      distinguished from OpenAI); endpoint id lives on the span only.
- [x] **Acceptance (verified live):** an embed produced `partio_integration_requests_total{service="ollama",
      operation="embedding",outcome="ok"}` + an `ollama embedding` Tempo span; a completion produced the
      `operation="completion"` series + an `ollama completion` span (recorded `error` on a real upstream
      timeout). Integrations dashboard p95-by-service+operation confirmed populating.

### Phase 4a — Background work, health, authz
Closes: health/model-load/cleanup/authz families + gauges.

- [x] `partio_endpoints_healthy`/`_unhealthy{kind}` gauges published from both health-check services;
      `partio_endpoint_health_checks_total` + duration + a `health_check:<kind>` span recorded per poll in
      `SharedHealthCheckCoordinator` (subscription carries a `Kind`).
- [x] `partio_model_load_total`/duration in `ModelLoadService`; `partio_request_history_writes_total` in
      `RequestHistoryService`; `partio_request_history_cleanup_total` in the cleanup loop.
- [x] `partio_authz_decisions_total{result}` recorded at the Watson `AuthenticateApiRequest` hook (permit/deny).
- [x] * **Acceptance:** gauge/counter logic unit-testable and covered; live gauge-movement pending stack run.

### Phase 4b — Logs to Loki (conditional; recommended)
Closes: the third signal for background work.

- [x] `docker/otel/otel-collector-config.yaml` tails `./logs` via a `filelog` receiver, stamps
      `service.name=partio`, and ships to Loki; Loki datasource provisioned in Grafana.
- [x] * **Acceptance:** pipeline wired and compose-validated; live log-in-Loki + logs↔traces link pending
      stack run.

### Phase 5 — Observability stack in `compose.yaml`
Closes: the one-command stack. Reuse Pneuma's wiring near-verbatim.

- [x] `docker/compose.yaml` adds **prometheus** (`v3.5.4`), **tempo** (`2.6.1`), **grafana-oss** (`13.0.2`),
      **loki** (`3.2.1`), and **otel-collector** (`0.109.0`) — every image pinned. Partio images pinned to `v0.5.0`.
- [x] Healthchecks on prometheus/tempo/loki/grafana; Grafana gated on prometheus + tempo `service_healthy`;
      collector gated on loki `service_healthy`.
- [x] Configs mounted **read-only**; `../assets/grafana` → `/var/lib/grafana/dashboards/partio`. All
      observability ports bound to `127.0.0.1`.
- [x] Grafana env `GF_SECURITY_ADMIN_USER/PASSWORD=admin`, `GF_USERS_ALLOW_SIGN_UP=false`.
- [x] `docker/prometheus.yaml` (two scrape jobs: Watson `/metrics` + app `/v1.0/metrics` on
      `partio-server:8400`), `docker/tempo.yaml` (OTLP 4317/4318, local storage); app OTLP → `http://tempo:4317`.
- [x] `docker/update.bat` added (`pull` → `down` → `up -d` → `ps -a`).
- [ ] Mirror services into `docker/factory/` — **n/a as written**: Partio's `docker/factory/` is a
      seed-data + `partio.json` variant used with the main `docker/compose.yaml` (no separate factory
      compose), so there are no factory services to mirror. The factory `partio.json` telemetry section is
      in place; verify factory data/reset scripts stay consistent.
- [ ] * **Acceptance:** `docker compose config` validates; full `docker compose up -d --build` end-to-end
      not yet run (needs the published/ built `partio-server:v0.5.0` image) — §0 follow-up 1 & 5.

### Phase 6 — Grafana provisioning + dashboards
Closes: per-domain dashboards as code.

- [x] `docker/grafana/provisioning/datasources/partio-datasources.yml` — fixed UIDs `prometheus` (default),
      `tempo`, `loki`, pointing at the container names (with Tempo→Loki/Prometheus correlation).
- [x] `docker/grafana/provisioning/dashboards/partio-dashboards.yml` — file provider, folder `Partio`,
      path `/var/lib/grafana/dashboards/partio`.
- [x] Authored `assets/grafana/*.json`, **one dashboard per domain** (all JSON-validated):
      - **Overview** — `watson_server_up`, uptime, total request rate + error ratio (off
        `http_server_request_duration_*`), process completed vs failed.
      - **HTTP** — request rate by route/status, p50/p95/p99, top routes by p95 (Watson families).
      - **Processing** — process rate by operation+outcome, per-stage throughput/failure, per-stage p95
        (`partio_process_stage_duration_seconds_bucket`), queued waits.
      - **Integrations** — request+error rate by service, p95 by service+operation (Ollama/OpenAI/
        Gemini/vLLM).
      - **Endpoint Health** — healthy/unhealthy gauges by kind, check rate/failures, check-duration p95.
      - _(optional 6th: Background/Jobs — model-load + request-history cleanup.)_
- [x] Standard PromQL shapes used throughout; every target pinned to `datasource {type: prometheus, uid: prometheus}`.
- [ ] * **Acceptance:** dashboards are provisioned and JSON-valid; live population under load pending stack run.

### Phase 7 — Frontend external-services card
Closes: the home-page card linking operators to the tools.

- [x] `ExternalServicesCard` added to `DashboardView.jsx`, placed **below** the `RequestHistoryChart` KPIs
      and Quick Actions.
- [x] Lists Grafana (`:3000`, `admin / admin`), Prometheus (`:9090`), Tempo (`:3200`), Loki (`:3100`), and
      Ollama (`:11434`); URLs built from `window.location.hostname`; host-published ports shown.
- [x] Reuses the `.action-card` grid pattern, the HTTP-safe `copyToClipboard` util, and a monospace
      credential token with copy. Card + own CSS with light/dark theme variables.
- [x] Strings kept inline, consistent with the existing views (the app's i18n is minimally adopted).
- [x] **Acceptance:** vitest (3 cases) confirms the card renders, host-based links, credential display, and
      copy-with-confirmation; frontend suite (9/9) and production build pass.

### Phase 8 — Docs, tests, production notes
- [x] `TELEMETRY.md` written (families, endpoints, Grafana access, reading workflows). `CHANGELOG.md`
      updated with the v0.5.0 section. **`README.md` updated** with an observability/telemetry section.
- [x] `Partio.postman_collection.json` gains an **Observability** folder with the two `/metrics` requests.
- [x] Backend tests added (`src/Test.Shared/TelemetryUnitTests.cs`, ~28 positive+negative cases: `Render()`
      families, escaping, histogram buckets, thread-safety, scopes, settings defaults, and the
      `Enabled=false`/best-effort trace-host lifecycle) + an integration case hitting both `/metrics` endpoints.
- [x] Production/security notes in `TELEMETRY.md` §9 (credential rotation, no public scrape endpoints, no
      ids/secrets in labels, forwarded-header trust off by default).

---

## 6. Reference map (source → Partio target)

| Concern | Pneuma / Conductor reference | Partio target |
|---|---|---|
| Metrics registry | `pneuma/src/Pneuma.Core/Observability/PneumaMetrics.cs` | `src/Partio.Core/Observability/PartioMetrics.cs` |
| Trace wrapper | `pneuma/src/Pneuma.Server/Services/TelemetryService.cs` | `src/Partio.Server/Services/TelemetryService.cs` |
| Settings | `pneuma/src/Pneuma.Server/Settings/TelemetrySettings.cs` | `src/Partio.Core/Settings/TelemetrySettings.cs` |
| HTTP capture | Pneuma `PostRoutingAsync` **/** Watson built-in (chosen) | `PartioServer.cs` — enable `Settings.Telemetry` |
| Pipeline spans | Pneuma `IngestionProcessor.cs` (`stage:<Name>`) | Chunking/Summarization/Tokenization engines |
| Integration record | Pneuma `IntegrationClientBase` `finally` | `ThirdParty/CompletionClientBase.cs` + `EmbeddingClientBase.cs` |
| Health gauges | Conductor `HealthCheckService.RegisterTelemetry()` | `EmbeddingHealthCheckService` / `CompletionHealthCheckService` |
| Logs → Loki | Conductor `otel-collector-config.yaml` filelog receiver | `docker/otel/` + `compose.yaml` (Phase 4b) |
| Docker stack | `pneuma/docker/compose.yaml`, `prometheus.yaml`, `tempo.yaml` | `docker/compose.yaml` + configs |
| Grafana provisioning | `pneuma/docker/grafana/provisioning/` | `docker/grafana/provisioning/` |
| Dashboards | `pneuma/assets/grafana/pneuma-*.json` | `assets/grafana/partio-*.json` |
| External-services card | `pneuma/admin-dashboard/src/views/HomeView.jsx`; Conductor `dashboard/src/views/Dashboard.jsx` | `dashboard/src/components/DashboardView.jsx` |

---

## 7. Open decisions — all resolved (see §0)

These were the pre-implementation decisions; their outcomes are recorded in §0 "Resolved decisions".

1. **Collection path A vs B** (§3) — **resolved to the zero-bridging hybrid** (Watson's own `/metrics`
   for HTTP/`watson.*`, app `/v1.0/metrics` for `partio_*`, Radiant for traces only).
2. **Loki in v1?** — **yes**, via the OTLP collector `filelog` → Loki pattern.
3. **5 vs 6 dashboards** — **five**; model-load/cleanup are exposed as metrics but not given their own board.
4. **App HTTP metrics** — confirmed **not** re-implemented; the HTTP dashboard reads Watson's
   `http_server_*` and there is no `partio_http_*` family.

---

## 8. Definition of done

A reviewer can clone the repo, run `docker compose up -d --build`, generate traffic, and:

- see the `Partio` Grafana folder populate across Overview / HTTP / Processing / Integrations /
  Endpoint Health;
- resolve a slow `/process` call to its slowest stage and the exact provider call in one Tempo trace;
- reach every tool from the dashboard's External Services card;

…without reading source, attaching a debugger, or wiring anything by hand. That is the requirements'
operational test, and it is the bar this plan is built to clear.
