# Partio Telemetry Guide

Partio emits three correlated signals — **metrics** (aggregate counters and latency histograms),
**traces** (distributed spans of individual requests), and **logs** — across the whole product: the
HTTP surface, the chunk/embed/summarize processing pipeline, every outbound provider call, and the
background endpoint-health and cleanup workers. This guide explains what is emitted, how it is exposed
and collected, how to reach Grafana, and how to read the data to answer real operational questions.

---

## 1. At a glance

| | Metrics | Traces | Logs |
|---|---|---|---|
| **HTTP layer** | Watson built-in (`http_server_*`, `watson_*`) | Watson server span per request | — |
| **Application layer** | `PartioMetrics` (`partio_*`) | `PartioTelemetry` spans (via Radiant) | `SyslogLogging` files |
| **Exposed at** | `GET /metrics` (Watson) and `GET /v1.0/metrics` (app) | OTLP push to Tempo | rolling files under `logs/` |
| **Collected by** | Prometheus (`:9090`) | Tempo (`:3200`, OTLP `:4317`) | otel-collector → Loki (`:3100`) |
| **Visualized in** | Grafana (`:3000`) | Grafana → Explore → Tempo | Grafana → Explore → Loki |

Everything is provisioned in the Docker stack under `docker/`; a plain `docker compose up -d --build`
brings up Partio, Prometheus, Tempo, Loki, the OTLP collector, and Grafana, wired together.

Two facts shape the design. First, **Watson 7.1 measures itself** — enabling `Settings.Telemetry`
emits the entire HTTP surface as metrics and a per-request server span with no code beyond a toggle.
Second, Watson stops at HTTP; the work behind the routes — pipeline stages, provider calls, background
jobs — is instrumented by the application on its own `Partio` meter and activity source.

---

## 2. What Partio measures (metrics inventory, by domain)

Watson emits the HTTP layer under the OpenTelemetry semantic-convention names; the application prefixes
every family it owns with `partio_`. Labels are deliberately **low-cardinality** (closed enumerations,
no ids) so series stay bounded — identifiers live on spans, never on metric labels.

### HTTP (Watson, at `/metrics`)
- `http_server_request_duration_seconds{http_request_method,http_route,http_response_status_code}` —
  request-latency histogram (route is always the **template**, e.g. `/v1.0/process`).
- `http_server_active_requests`, request/response body-size histograms.
- `watson_server_up`, `watson_server_uptime_seconds`, connection/route/auth/websocket counters.

### Processing pipeline (app, at `/v1.0/metrics`)
- `partio_process_total{operation,outcome}` — top-level operations by outcome. `operation` ∈
  `chunk`, `embed`, `summarize`, `process`, `process_batch`; `outcome` ∈ `ok`, `error`, `cancelled`.
- `partio_process_duration_seconds{operation}` — total operation-latency histogram.
- `partio_process_stage_total{stage,outcome}` — per-stage counter. `stage` ∈ `tokenize`, `chunk`,
  `summarize`, `embed`.
- `partio_process_stage_duration_seconds{stage}` — per-stage latency histogram (where a slow stage shows up).

### Integrations (app)
- `partio_integration_requests_total{service,operation,outcome}` — outbound provider calls. `service` ∈
  `ollama`, `openai`, `gemini`, `vllm`; `operation` ∈ `embedding`, `completion`; `outcome` ∈ `ok`,
  `error`, `cancelled`, `rejected` (429 from the per-endpoint concurrency limiter).
- `partio_integration_request_duration_seconds{service,operation}` — per-integration latency histogram.

### Endpoint health (app)
- `partio_endpoint_health_checks_total{kind,outcome}` — health-check polls. `kind` ∈ `embedding`,
  `completion`; `outcome` ∈ `healthy`, `unhealthy`.
- `partio_endpoint_health_check_duration_seconds{kind}` — check-latency histogram.
- `partio_endpoints_healthy{kind}` / `partio_endpoints_unhealthy{kind}` — current endpoint counts (gauges).

### Model load, request history, authorization, uptime (app)
- `partio_model_load_total{kind,outcome}` + `partio_model_load_duration_seconds{kind}`.
- `partio_request_history_writes_total{outcome}`, `partio_request_history_cleanup_total{outcome}`.
- `partio_authz_decisions_total{result}` — `permit` / `deny`.
- `partio_uptime_seconds` — supplementary process-uptime gauge (the Overview dashboard uses Watson's
  `watson_server_uptime_seconds`).

---

## 3. What Partio traces (spans)

Traces are emitted over OTLP and stored in Tempo. Watson opens **one server span per request** (named
`{method} {route}`, adopting an inbound `traceparent` so a request from an upstream service joins the
same trace). Application spans nest underneath it automatically:

- **Processing** — a `process` root span (tagged `partio.operation`) with a child `stage:<name>` span
  per pipeline stage (`stage:tokenize`, `stage:chunk`, `stage:summarize`, `stage:embed`), so a slow
  `/v1.0/process` call's stage breakdown is visible in one trace.
- **Integrations** — each outbound provider call carries a client span named `<service> <operation>`
  (e.g. `ollama embedding`), tagged with the service, operation, and endpoint id — so a slow answer
  resolves to the specific provider call that caused it.
- **Endpoint health** — background probes carry `health_check:<kind>` spans.

Instrumentation is **best-effort**: spans are opened on a BCL `ActivitySource` that returns `null` when
no collector is listening (a few nanoseconds, zero allocation), and a Radiant init failure logs a
warning and runs without traces rather than crashing the server.

---

## 4. How it is exposed and collected

- **Metrics exposure.** Watson serves its HTTP/`watson.*` metrics in Prometheus text format at
  `GET /metrics` (its in-process endpoint, no extra port). The application serves the `partio_*`
  families at `GET /v1.0/metrics`. Both are anonymous by design — keep them on an internal network.
- **Metrics collection.** Prometheus (`docker/prometheus.yaml`) scrapes both endpoints on an interval
  and stores the series in its TSDB (`:9090`).
- **Trace exposure & collection.** The server pushes spans over OTLP to Tempo (`:4317` ingest) via a
  Radiant host that subscribes to the `Watson` and `Partio` activity sources; Tempo serves trace
  queries on `:3200`.
- **Logs.** The OTLP collector tails Partio's rolling log files (`docker/otel/otel-collector-config.yaml`,
  `filelog` receiver) and ships them to Loki (`:3100`), stamped with `service.name=partio`.
- **Datasources & dashboards.** Grafana is provisioned (`docker/grafana/provisioning/…`) with
  **Prometheus**, **Tempo**, and **Loki** datasources and a set of **per-domain dashboards** mounted
  from `assets/grafana/*.json` into the Grafana **Partio** folder.

Configuration lives under `Settings.Telemetry` (see `partio.json`): `Enabled`, `ServiceName`,
`OtlpEndpoint` (loopback `127.0.0.1:4317` locally, `tempo:4317` in the compose network), `OtlpProtocol`,
and `PrometheusEnabled`.

---

## 5. Accessing Grafana

1. Bring up the stack: `docker compose up -d --build` (from `docker/`).
2. Open **http://localhost:3000**.
3. Log in with the default credentials **`admin` / `admin`** (set via `GF_SECURITY_ADMIN_*`; change
   them for any non-local deployment).
4. Open **Dashboards → Browse → the Partio folder**. Five domain dashboards auto-load from provisioning:
   **Partio — Overview**, **— HTTP**, **— Processing**, **— Integrations**, and **— Endpoint Health**.

Related URLs: Prometheus **http://localhost:9090**, Tempo API **http://localhost:3200**, Loki
**http://localhost:3100**. The product dashboard's home page carries an **External Services** card that
links to all of these with their default credentials.

---

## 6. Reading the dashboards (one per domain)

- **Partio — Overview** — uptime, total request rate, error ratio, processing ok-vs-error. Start here.
- **Partio — HTTP** — request rate by route and status class, latency quantiles (p50/p95/p99), and top
  routes by p95, straight off Watson's `http_server_request_duration_seconds`. Find a slow or erroring endpoint.
- **Partio — Processing** — operation rate by outcome, per-stage throughput and failure rate, and
  **per-stage p95**. This is where a slow `chunk`, `summarize`, or `embed` stage becomes obvious.
- **Partio — Integrations** — request and error rate by provider service, plus p95 per service+operation
  and the rejected-by-concurrency rate. When processing is slow, this tells you whether a provider is the cause.
- **Partio — Endpoint Health** — healthy/unhealthy endpoint gauges by kind, health-check rate and
  failures, and check-latency p95.

## 7. Reading traces

1. In Grafana, go to **Explore** and pick the **Tempo** datasource.
2. Search by service/span name or a trace id. Processing calls appear as a `process` root with
   `stage:*` children; provider calls appear as `<service> <operation>` client spans.
3. Open the trace to see the span waterfall — the widest bar is the stage or provider call that
   dominated the time.

## 8. Typical workflows

- **"Processing feels slow."** Processing row → per-stage p95. The tallest stage is the bottleneck; if
  it's `embed`, check the Integrations row for the provider's p95, then open the request's trace.
- **"Something is erroring."** Overview error ratio → HTTP status-class panel to find the route →
  Integrations error rate to see if a provider is failing → the trace for the exact call.
- **"An endpoint keeps flapping."** Endpoint Health row → unhealthy gauge and check failure rate by kind.

## 9. Production and security notes

- Change Grafana's admin credentials for any non-local deployment (`GF_SECURITY_ADMIN_*`); sign-up is disabled.
- Do not expose Prometheus, Tempo, Loki, or the `/metrics` and `/v1.0/metrics` scrape endpoints on a
  public interface — they carry operational detail and have no auth of their own.
- Keep ids and secrets out of metric labels (they belong on spans). Leave forwarded-header trust off
  unless the listener sits behind a declared proxy.

## 10. Notes & extension

- Metrics are per-process and in-memory (reset on restart); Prometheus retains the history. There is no
  remote write by default.
- To add a metric, extend `PartioMetrics` (a `Record*` method + a family in `Render()`); to add a span,
  open one on `PartioTelemetry.ActivitySource`; to add a panel, edit the relevant dashboard under
  `assets/grafana/partio-*.json`.
