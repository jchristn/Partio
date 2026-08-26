# Load Models API Plan

Last reviewed: 2026-06-05

## Purpose

Partio needs an explicit control-plane API that asks a configured model endpoint to make its model ready before a user sends real work. For a local Ollama inference endpoint such as `gemma3:4b` on `http://localhost:11434`, the API should call that configured host and request that the model be loaded. For hosted APIs and OpenAI-compatible servers that do not expose a native model-load primitive, the API should run the smallest safe warm request and report the outcome honestly.

The important product distinction is "loaded" versus "warmed" versus "validated". Ollama has native preloading semantics. OpenAI and Gemini hosted APIs do not expose a customer-facing model residency operation. vLLM is usually started with a served model already resident or loading at server startup; the OpenAI-compatible API can be warmed, but it is not a general remote model loader. The API response must preserve that distinction so the dashboard and SDKs never imply a guarantee the upstream runner cannot provide.

## Progress Convention

Use the checkboxes in this document as the working tracker. Change `[ ]` to `[x]` only after the acceptance criteria under that item have passed. Add dated notes under an item when implementation chooses a different path.

Status labels:

| Label | Meaning |
| --- | --- |
| `[ ]` | Not started |
| `[x]` | Complete and verified |
| `Blocked:` | Waiting on a product or technical decision |
| `Decision:` | Accepted design choice |

## Requirements Trace

| Requirement Area | Source | Impact |
| --- | --- | --- |
| Authentication, authorization, accounting | `C:\Code\agents\requirements\AUTHENTICATION.md` | Treat model loading as an authenticated control-plane action. Enforce tenant/resource scope server-side, log/audit outcomes, redact credentials. |
| Backend architecture | `C:\Code\agents\requirements\BACKEND_ARCHITECTURE.md` | Use typed request/response models, Watson routes, OpenAPI metadata, cancellation tokens, request history, no fixed-contract `JsonElement` routing. |
| Backend tests | `C:\Code\agents\requirements\BACKEND_TEST_ARCHITECTURE.md` | Add shared tests consumed by automated, xUnit, and NUnit runners where practical. Reuse provider-compatible test servers. |
| Code style | `C:\Code\agents\requirements\CODE_STYLE.md` | No `var`, no tuples, one public model/enum per file, XML docs on public members, `ConfigureAwait(false)`, meaningful exceptions. |
| Frontend architecture | `C:\Code\agents\requirements\FRONTEND_ARCHITECTURE.md` | Extend the fetch-based API client and dashboard endpoint views. Keep controls operator-focused, responsive, and permission-aware. |
| Internationalization | `C:\Code\agents\requirements\I18N.md` | New dashboard strings must be localizable. If Partio lacks the i18n runtime at implementation time, add it first or document the exception as release-blocking debt. |
| Repository and SDK layout | `C:\Code\agents\requirements\REPOSITORY_REQUIREMENTS.md` | Keep source under `src/`, `dashboard/`, and `sdk/`. Update SDK README files and test harnesses. |
| Documentation writing | `C:\Code\agents\requirements\WRITING_DOCUMENTS.md` | Keep public docs direct, specific, and free of generic filler. |

Provider behavior references used for this plan:

- Ollama API introduction and generate endpoint: `https://docs.ollama.com/api/introduction`, `https://docs.ollama.com/api/generate`
- Ollama preloading and keep-alive FAQ: `https://github.com/ollama/ollama/blob/main/docs/faq.mdx`
- Ollama embeddings endpoint: `https://docs.ollama.com/api/embed`
- OpenAI Chat Completions and Embeddings API references: `https://platform.openai.com/docs/api-reference/chat/create-chat-completion`, `https://platform.openai.com/docs/api-reference/embeddings`
- vLLM online serving and OpenAI-compatible server docs: `https://docs.vllm.ai/en/latest/serving/online_serving/`
- Gemini API reference and embeddings docs: `https://ai.google.dev/api`, `https://ai.google.dev/gemini-api/docs/embeddings`

## Product Decisions

- [x] Decision: ship explicit endpoint-level load routes instead of overloading health checks or explorer calls.
  - Routes:
    - `POST /v1.0/endpoints/embedding/{id}/load`
    - `POST /v1.0/endpoints/completion/{id}/load`
  - Acceptance criteria:
    - Route names follow existing endpoint resource patterns.
    - Health checks remain availability probes and do not acquire new model-loading side effects.
    - Explorer remains a diagnostic surface for arbitrary sample requests, not the dashboard's primary load action.

- [x] Decision: default to `Strategy = "Auto"` and let Partio choose the safest provider action.
  - Provider defaults:
    - Ollama completion endpoint: native empty `/api/generate` or `/api/chat` preload with `keep_alive`.
    - Ollama embedding endpoint: `/api/embed` with a short input and `keep_alive`.
    - OpenAI completion endpoint: minimal chat completion warm request.
    - OpenAI embedding endpoint: minimal embedding warm request.
    - vLLM completion endpoint: OpenAI-compatible minimal chat completion warm request.
    - vLLM embedding endpoint: OpenAI-compatible minimal embedding warm request.
    - Gemini completion endpoint: minimal `generateContent` warm request.
    - Gemini embedding endpoint: minimal `embedContent` warm request.
  - Acceptance criteria:
    - Response distinguishes `Loaded`, `Warmed`, `Validated`, and `Failed`.
    - Hosted OpenAI/Gemini outcomes never claim native model residency.
    - vLLM outcomes explain that the model must already be served by that vLLM process.

- [x] Decision: admin-only in the first implementation.
  - Acceptance criteria:
    - Existing `RequireAdmin(req)` path protects the routes initially.
    - Any future tenant-scoped `Execute` permission is tracked separately and must still enforce endpoint tenant ownership in the backend.

- [x] Decision: no new persistent database table in v1.
  - Rationale:
    - Load state is provider-host local, not Partio-owned state.
    - In-memory state would be misleading in multi-instance deployments.
    - Request history already captures who triggered the action and what upstream call happened.
  - Acceptance criteria:
    - No schema migration is added for v1.
    - Request history detail records load attempts with upstream call details.
    - Dashboard "last result" is transient unless read from request history.

- [x] Decision: do not add unload behavior to this API.
  - Acceptance criteria:
    - `KeepAlive = "0"` or equivalent unload values are rejected by the load endpoint.
    - A future unload route, if needed, is planned separately as `POST /v1.0/endpoints/{type}/{id}/unload`.

## API Contract

### Request Model

Add `src/Partio.Core/Models/ModelLoadRequest.cs`.

```json
{
  "Strategy": "Auto",
  "TimeoutMs": 60000,
  "KeepAlive": "30m",
  "SampleInput": "Partio model load probe",
  "MaxTokens": 1,
  "RecordRequestHistory": true,
  "RequireNativeLoad": false
}
```

Fields:

| Field | Type | Default | Notes |
| --- | --- | --- | --- |
| `Strategy` | string enum | `Auto` | Values: `Auto`, `NativeProviderLoad`, `WarmRequest`. |
| `TimeoutMs` | int | endpoint `MaximumTimeoutMs` | Clamp to `1..endpoint.MaximumTimeoutMs`. |
| `KeepAlive` | string? | server default, proposed `30m` | Applies to Ollama. Reject `0` because unload is out of scope. |
| `SampleInput` | string? | `Partio model load probe` | Used for warm embedding input and warm chat prompt. Must be short. |
| `MaxTokens` | int | `1` | Completion warm request output cap. Clamp to `1..16`. |
| `RecordRequestHistory` | bool | `true` | If request history is globally disabled, this has no effect. |
| `RequireNativeLoad` | bool | `false` | If true, return an unsupported-strategy error unless provider has native load semantics. |

Implementation note: use classic properties with backing fields where validation is needed. Do not use a dictionary or `JsonElement` for this fixed shape.

### Response Model

Add `src/Partio.Core/Models/ModelLoadResponse.cs`.

```json
{
  "Success": true,
  "StatusCode": 200,
  "Outcome": "Loaded",
  "EndpointType": "Completion",
  "EndpointId": "cep_xxxx",
  "TenantId": "default",
  "ApiFormat": "Ollama",
  "Model": "gemma3:4b",
  "Strategy": "NativeProviderLoad",
  "Message": "Ollama accepted the preload request.",
  "ResponseTimeMs": 482.5,
  "StartedUtc": "2026-06-05T18:00:00Z",
  "CompletedUtc": "2026-06-05T18:00:01Z",
  "RequestHistoryId": "req_xxxx",
  "EmbeddingCalls": null,
  "CompletionCalls": []
}
```

Fields:

| Field | Type | Notes |
| --- | --- | --- |
| `Success` | bool | True only when upstream load or warm action succeeded. |
| `StatusCode` | int | Partio-mapped status for the load operation. |
| `Outcome` | string enum | Values: `Loaded`, `Warmed`, `Validated`, `Unsupported`, `Failed`. |
| `EndpointType` | string enum | `Embedding` or `Completion`. |
| `EndpointId` | string | Configured Partio endpoint ID. |
| `TenantId` | string | Endpoint tenant. |
| `ApiFormat` | string | `Ollama`, `OpenAI`, `Gemini`, or `vLLM`. |
| `Model` | string | Configured model name. |
| `Strategy` | string | Effective strategy after `Auto` resolution. |
| `Message` | string | Operator-safe message. Do not include secrets. |
| `ResponseTimeMs` | double | Total route runtime. |
| `StartedUtc`, `CompletedUtc` | DateTime | UTC timestamps. |
| `RequestHistoryId` | string? | Present when request history recorded the route. |
| `EmbeddingCalls`, `CompletionCalls` | arrays? | Upstream details following existing detail models. |

Add enums as separate files if the project owner wants strict enum models:

- `src/Partio.Core/Enums/ModelLoadStrategyEnum.cs`
- `src/Partio.Core/Enums/ModelLoadOutcomeEnum.cs`
- `src/Partio.Core/Enums/EndpointTypeEnum.cs`

### Error Mapping

| Scenario | HTTP status | Response |
| --- | --- | --- |
| Missing/invalid body | `400` | `ApiErrorResponse` |
| Invalid `KeepAlive` unload value | `400` | `ApiErrorResponse` |
| Unauthenticated | `401` | Existing auth response |
| Non-admin caller | `401` or current `RequireAdmin` behavior | Existing auth response |
| Endpoint missing | `404` | Existing not found response |
| Endpoint inactive | `400` | Existing bad request behavior |
| Explicit native load requested for unsupported provider | `409` preferred, `400` acceptable if staying with current exception mapper | `ApiErrorResponse` |
| Endpoint concurrency limit reached | `429` | Existing `ProviderConcurrencyLimitException` mapping |
| Upstream model runner error | `502` | `ApiErrorResponse` with provider-safe message |
| Upstream timeout | `504` | Existing `ProviderOperationTimeoutException` mapping |

### Headers

- [x] Add `X-Partio-Endpoint-Id` on success and mapped provider failures when the endpoint is known.
- [x] Add `X-Partio-Model` on success and mapped provider failures when the model is known.
- [x] Do not expose upstream API keys or auth headers.

## Provider Semantics

| Provider | Completion endpoint behavior | Embedding endpoint behavior | Reported outcome |
| --- | --- | --- | --- |
| Ollama | Empty `/api/generate` or `/api/chat` preload with `model` and `keep_alive`. | `/api/embed` with short input and `keep_alive`. | `Loaded` when upstream succeeds. |
| OpenAI | Minimal `/v1/chat/completions` request through existing OpenAI client path. | Minimal `/v1/embeddings` request through existing OpenAI client path. | `Warmed`, never `Loaded`. |
| vLLM | Minimal OpenAI-compatible `/v1/chat/completions` request. | Minimal OpenAI-compatible `/v1/embeddings` request. | `Warmed`; note that vLLM must already be serving the model. |
| Gemini | Minimal `generateContent` request through existing Gemini client path. | Minimal `embedContent` request through existing Gemini client path. | `Warmed`, never `Loaded`. |

Implementation guardrails:

- [x] Do not send empty prompts to providers that reject them. Use `SampleInput` when native empty preload is not documented.
- [x] Keep warm inputs short and non-sensitive.
- [x] Route all upstream calls through the existing provider client/concurrency/timeout path unless native Ollama load needs a small provider-specific helper.
- [x] For `RequireNativeLoad = true`, accept only Ollama in v1.
- [x] Include provider status and response body in request history detail under existing truncation/redaction rules, not in normal operator messages.

## Backend Plan

### Models and Enums

- [x] Add `ModelLoadRequest`.
  - Files:
    - `src/Partio.Core/Models/ModelLoadRequest.cs`
  - Acceptance criteria:
    - XML docs on public class and public members.
    - Validating setters for `Strategy`, `TimeoutMs`, `KeepAlive`, `SampleInput`, `MaxTokens`.
    - Defaults produce a valid `Auto` load request when the body is `{}`.
    - No `var`, no tuples, using directives inside namespace.

- [x] Add `ModelLoadResponse`.
  - Files:
    - `src/Partio.Core/Models/ModelLoadResponse.cs`
  - Acceptance criteria:
    - Holds endpoint identity, effective strategy, outcome, timing, and upstream call details.
    - Does not expose API keys or raw auth headers outside the existing redacted call-detail path.

- [x] Add optional enum files if the implementation prefers strict enum properties over string-compatible SDK models.
  - Files:
    - `src/Partio.Core/Enums/ModelLoadStrategyEnum.cs`
    - `src/Partio.Core/Enums/ModelLoadOutcomeEnum.cs`
    - `src/Partio.Core/Enums/EndpointTypeEnum.cs`
  - Acceptance criteria:
    - One enum per file.
    - XML docs on every enum and value.

### Provider Runtime

- [x] Add provider-neutral load or warm methods.
  - Candidate API:
    - `EmbeddingClientBase.LoadModelAsync(string model, ModelLoadRequest request, CancellationToken token = default)`
    - `CompletionClientBase.LoadModelAsync(string model, ModelLoadRequest request, CancellationToken token = default)`
  - Acceptance criteria:
    - Virtual default implementation performs a warm request where safe.
    - Overrides exist where provider behavior differs.
    - Calls record `EmbeddingCallDetail` or `CompletionCallDetail`.
    - Timeout and concurrency limiting reuse existing `ProviderConcurrencyLimiter`.

- [x] Implement Ollama completion native load.
  - Files:
    - `src/Partio.Core/ThirdParty/OllamaCompletionClient.cs`
  - Acceptance criteria:
    - `Auto` resolves to native load.
    - Sends `model`, `stream = false`, and positive or negative non-zero `keep_alive`.
    - Uses empty/native preload only for Ollama, where documented.
    - Captures `load_duration` when present in the response.

- [x] Implement Ollama embedding load/warm.
  - Files:
    - `src/Partio.Core/ThirdParty/OllamaEmbeddingClient.cs`
  - Acceptance criteria:
    - Sends `/api/embed` with configured model, short `SampleInput`, and `keep_alive`.
    - Returns `Loaded` on success because the endpoint supports keep-alive semantics.
    - Captures upstream call details.

- [x] Implement OpenAI and vLLM warm behavior.
  - Files:
    - `src/Partio.Core/ThirdParty/OpenAiCompletionClient.cs`
    - `src/Partio.Core/ThirdParty/OpenAiEmbeddingClient.cs`
  - Acceptance criteria:
    - Completion warm request uses configured model, short prompt, and `MaxTokens`.
    - Embedding warm request uses configured model and short input.
    - Outcome is `Warmed`.
    - Explicit `NativeProviderLoad` with `RequireNativeLoad = true` fails with unsupported provider.

- [x] Implement Gemini warm behavior.
  - Files:
    - `src/Partio.Core/ThirdParty/GeminiCompletionClient.cs`
    - `src/Partio.Core/ThirdParty/GeminiEmbeddingClient.cs`
  - Acceptance criteria:
    - Completion warm request uses `generateContent` with a short prompt and minimal output.
    - Embedding warm request uses `embedContent` with a short input.
    - Outcome is `Warmed`.

### Route and Service Layer

- [x] Add a model-load service.
  - Completed: model-load orchestration now lives in `src/Partio.Server/Services/ModelLoadService.cs`.
  - Current status:
    - Route-local handlers `LoadEmbeddingEndpointModel` and `LoadCompletionEndpointModel` now handle admin authorization, request binding, validation, endpoint resolution, response headers, status assignment, and request-history persistence.
    - `ModelLoadService` creates responses, resolves initial strategy, invokes providers, maps provider results and exceptions, captures call details, completes timing, and logs outcomes.
  - Goal:
    - Move provider orchestration into a focused service without changing the public API, request/response contracts, authorization behavior, or request-history output.
  - Files:
    - `src/Partio.Server/Services/ModelLoadService.cs`
    - `src/Partio.Server/PartioServer.cs`
  - Suggested implementation tasks:
    - [x] Create `ModelLoadService` with one method for embedding endpoints and one method for completion endpoints.
    - [x] Move effective strategy resolution, provider invocation, exception mapping, call-detail capture, duration calculation, and log metadata into the service.
    - [x] Keep route handlers responsible for admin authorization, request binding, request validation, endpoint resolution, response headers, HTTP status assignment, and request-history persistence.
    - [x] Pass `CancellationToken`, endpoint metadata, request history ID, and client factory delegates into the service.
    - [x] Re-run the existing shared automated load tests to confirm no behavior changed.
    - [x] Dependency shape recorded: the service is constructed in `PartioServer.Main` with `CreateEmbeddingClient` and `CreateCompletionClient` delegates, avoiding direct database writes and keeping static server state out of provider orchestration.
  - Acceptance criteria:
    - Resolves effective strategy from endpoint type and `ApiFormat`.
    - Accepts `CancellationToken`.
    - Does not perform database writes.
    - Logs endpoint ID, tenant ID, provider, model, strategy, outcome, duration, and mapped status.
    - Never logs secrets.
    - Existing load endpoint tests still pass without changing expected JSON, status codes, headers, or request-history detail.
    - `PartioServer.cs` load route handlers are small enough to review as route glue rather than provider orchestration.

- [x] Register load routes.
  - Preferred files:
    - `src/Partio.Server/Routes/EndpointModelLoadRoutes.cs`
    - `src/Partio.Server/PartioServer.cs` for route registrar wiring
  - Minimum-change fallback:
    - Register in the existing endpoint route region in `src/Partio.Server/PartioServer.cs`, with a note explaining the project currently keeps routes in that file.
  - Acceptance criteria:
    - Both routes require auth.
    - Both routes use typed request binding.
    - Both routes appear in `/openapi.json`.
    - Both routes set response status codes explicitly.
    - Both routes pass `GetRequestCancellationToken(req)`.

- [x] Add endpoint resolution helpers that can bypass health gating for load.
  - Files:
    - `src/Partio.Server/PartioServer.cs`
  - Acceptance criteria:
    - Endpoint must exist, be active, and match the caller tenant if/when tenant-scoped execution is enabled.
    - Load route does not fail only because the health service currently marks the endpoint unhealthy. A cold or unhealthy runner may be exactly why the operator is loading it.
    - Load route still fails when the host cannot be reached or the provider returns an error.

- [x] Add OpenAPI metadata.
  - Files:
    - `src/Partio.Server/PartioServer.cs`
  - Acceptance criteria:
    - New tag: `Model Loading` or endpoint-specific tags.
    - Request and response examples are visible to dashboard API explorer and external users.
    - Error responses list `400`, `401`, `404`, `409` or documented fallback, `429`, `502`, and `504`.

### Request History and Observability

- [x] Reuse detailed request-history recording.
  - Files:
    - `src/Partio.Server/PartioServer.cs`
    - `src/Partio.Server/Services/RequestHistoryService.cs` only if existing detail shape needs a small extension
  - Acceptance criteria:
    - Detail payload includes `ModelLoad` metadata: endpoint type, endpoint ID, model, provider, strategy, outcome, timing.
    - Detail payload includes existing `EmbeddingCalls` or `CompletionCalls`.
    - `RequestHistoryId` appears in successful `ModelLoadResponse` when request history is enabled.
    - Detail recording respects existing truncation settings.

- [x] Add structured log events.
  - Acceptance criteria:
    - One info log on success.
    - One warning log on provider failure or timeout.
    - Logs include request ID if available.
    - Logs redact credentials.

- [x] Avoid false health coupling.
  - Acceptance criteria:
    - Existing health state remains independent.
    - Dashboard can refresh health after a load attempt, but the load response is not treated as a health-state write.

## Dashboard Plan

### API Client

- [x] Extend `dashboard/src/utils/api.js`.
  - Methods:
    - `loadEndpoint(id, request = {})`
    - `loadCompletionEndpoint(id, request = {})`
  - Acceptance criteria:
    - Uses existing `PartioApi.request`.
    - Preserves error handling shape for dashboard modals.
    - Does not require users to paste credentials again.

### Shared UI

- [x] Add a reusable load-model modal.
  - Candidate files:
    - `dashboard/src/components/modals/LoadModelModal.jsx`
    - `dashboard/src/components/modals/LoadModelModal.css`
  - Acceptance criteria:
    - Works for embedding and completion endpoints.
    - Shows provider, endpoint URL, model, and effective default strategy before execution.
    - Lets admin choose `Auto`, `NativeProviderLoad`, or `WarmRequest`.
    - Lets admin set `KeepAlive`, `TimeoutMs`, and `SampleInput` with sensible defaults.
    - Warns that loading can consume GPU/system memory on local runners.
    - Disables submit while request is in flight.
    - Shows result outcome, duration, status code, request-history link/id, and upstream call count.
    - Handles unsupported native load without crashing the view.

- [x] Add a compact result modal or reuse `JsonViewModal` for details.
  - Acceptance criteria:
    - Success and failure states are visually distinct.
    - Operators can copy endpoint ID, model name, and request-history ID.
    - Long upstream error messages wrap without layout breakage.

### Endpoint Views

- [x] Add row action to `EmbeddingEndpointsView.jsx`.
  - Files:
    - `dashboard/src/components/EmbeddingEndpointsView.jsx`
  - Acceptance criteria:
    - `ActionMenu` includes `Load Model`.
    - Action is hidden or disabled while another load action is running for that row.
    - Table remains usable at desktop, tablet, and mobile widths.
    - Health refresh can be triggered after completion.

- [x] Add row action to `CompletionEndpointsView.jsx`.
  - Files:
    - `dashboard/src/components/CompletionEndpointsView.jsx`
  - Acceptance criteria:
    - `ActionMenu` includes `Load Model`.
    - The `gemma3:4b` on Ollama case is the primary happy path.
    - Result labels use `Loaded` for Ollama native success and `Warmed` for non-native providers.

### Internationalization and Accessibility

- [x] Add or use the i18n foundation for new strings.
  - Files if foundation is missing:
    - `dashboard/src/i18n/index.js`
    - `dashboard/src/i18n/localeRegistry.js`
    - `dashboard/src/i18n/resources.js`
    - `dashboard/src/i18n/formatters.js`
  - Acceptance criteria:
    - New button labels, modal titles, tooltips, warnings, statuses, and errors come from translation resources.
    - `aria-label` and `title` strings are localizable.
    - Numeric durations use explicit locale-aware formatting if the formatter layer exists.

- [x] Validate responsive layout.
  - Completed: Playwright responsive smoke coverage was added and run successfully on June 5, 2026.
  - Current status:
    - `LoadModelModal` is wired into both endpoint views.
    - `dashboard/tests/model-load-responsive.spec.js` covers embedding and inference endpoint pages at 1280x800, 768x1024, and 390x844.
    - Screenshots are written to `dashboard/test-results/model-load-responsive`.
    - Mobile shell layout was updated so endpoint pages remain usable at narrow widths.
  - Manual validation path:
    - Automated Playwright validation replaced the manual path for this implementation.
  - Preferred automated validation path:
    - [x] Add a headless browser smoke test with mocked API responses.
    - [x] Capture screenshots for endpoint tables and the load modal at approximately 1280x800, 768x1024, and 390x844.
    - [x] Store screenshots under `dashboard/test-results/model-load-responsive`.
  - Acceptance criteria:
    - Manual or Playwright checks at approximately 1280px, 768px, and 390px.
    - Modal controls remain reachable.
    - No horizontal page scroll from long provider/model names.
    - Error text wraps and does not overlap buttons.
    - Row action menus and modal buttons remain tappable at narrow mobile width.
    - The modal can close, retry, and preserve useful error text without layout shift that hides controls.

## SDK Plan

### C# SDK

- [x] Add model classes.
  - Files:
    - `sdk/csharp/Partio.Sdk/Models/ModelLoadRequest.cs`
    - `sdk/csharp/Partio.Sdk/Models/ModelLoadResponse.cs`
  - Acceptance criteria:
    - XML docs on public classes and properties.
    - Nullable reference types are respected.
    - No `var`, no tuples.

- [x] Add client methods.
  - Files:
    - `sdk/csharp/Partio.Sdk/PartioClient.cs`
  - Methods:
    - `Task<ModelLoadResponse?> LoadEndpointAsync(string id, ModelLoadRequest? request = null)`
    - `Task<ModelLoadResponse?> LoadCompletionEndpointAsync(string id, ModelLoadRequest? request = null)`
  - Acceptance criteria:
    - Methods call the new REST routes.
    - CancellationToken support should be considered as a follow-up because the current SDK helper does not expose one.

- [x] Extend C# SDK test harness.
  - Files:
    - `sdk/csharp/Partio.Sdk.TestHarness/Program.cs`
  - Acceptance criteria:
    - Creates an Ollama completion endpoint and calls load.
    - Verifies response endpoint ID, model, outcome, and success.
    - Adds at least one unsupported native-load test against OpenAI/vLLM or documents why the harness cannot run it without a live compatible server.

### JavaScript SDK

- [x] Add client methods.
  - Files:
    - `sdk/js/partio-sdk.js`
  - Methods:
    - `loadEndpoint(id, request = {})`
    - `loadCompletionEndpoint(id, request = {})`
  - Acceptance criteria:
    - Uses native `fetch` through existing `_request`.
    - Throws `PartioError` on non-2xx responses.

- [x] Extend JS test harness.
  - Files:
    - `sdk/js/test-harness.js`
  - Acceptance criteria:
    - Covers completion endpoint load.
    - Covers embedding endpoint load if a configured active embedding endpoint exists.

### Python SDK

- [x] Add client methods.
  - Files:
    - `sdk/python/partio_sdk.py`
  - Methods:
    - `load_endpoint(endpoint_id, request=None)`
    - `load_completion_endpoint(endpoint_id, request=None)`
  - Acceptance criteria:
    - Uses existing `_request`.
    - Raises `PartioError` on non-2xx responses.

- [x] Extend Python test harness.
  - Files:
    - `sdk/python/test_harness.py`
  - Acceptance criteria:
    - Covers completion endpoint load.
    - Verifies response fields and cleanup still runs.

### SDK Documentation

- [x] Update SDK READMEs.
  - Files:
    - `sdk/csharp/README.md`
    - `sdk/js/README.md`
    - `sdk/python/README.md`
  - Acceptance criteria:
    - Each README includes a short `Load model` example.
    - Docs state that `Loaded` is only guaranteed for native providers such as Ollama.

## Documentation Plan

- [x] Update `REST_API.md`.
  - Acceptance criteria:
    - New `Model Loading` section.
    - Both routes documented with request and response examples.
    - Provider semantics table included.
    - Error mapping documented.
    - Example for `gemma3:4b` Ollama inference endpoint included.

- [x] Update `README.md`.
  - Acceptance criteria:
    - Feature list mentions model loading or warming.
    - API summary table includes both load routes.
    - Dashboard section mentions row-level load action.
    - Troubleshooting explains `Loaded` vs `Warmed` and vLLM limitations.

- [x] Update `CHANGELOG.md`.
  - Acceptance criteria:
    - Entry under unreleased or next version.
    - Notes API, dashboard, SDKs, and Postman collection.

- [x] Update any dashboard tour or wizard text if relevant.
  - No tour or wizard change was needed for this row-level operator action.
  - Files:
    - `dashboard/src/components/tour/tourSteps.js`
    - `dashboard/src/components/wizard/wizardSteps.jsx`
  - Acceptance criteria:
    - Endpoint setup flow can mention loading only if it is not noisy or distracting.
    - New text follows i18n approach if foundation exists.

## Postman Plan

- [x] Update `Partio.postman_collection.json`.
  - Folder:
    - `Model Loading`
  - Requests:
    - `Load Embedding Endpoint`
    - `Load Completion Endpoint`
    - `Load Completion Endpoint - Ollama gemma3:4b`
    - `Warm Completion Endpoint - OpenAI-compatible`
    - `Unsupported Native Load - Hosted Provider`
  - Variables:
    - `baseUrl`
    - `bearerToken`
    - `endpointId`
    - `completionEndpointId`
    - `keepAlive`
  - Acceptance criteria:
    - Requests include bearer auth.
    - Examples include expected `Loaded` and `Warmed` responses.
    - Collection remains valid JSON.

## Test Plan

### Shared and Integration Tests

- [x] Extend provider-compatible test servers.
  - Files:
    - `src/Test.Shared/SlowOllamaCompatibleServer.cs`
    - `src/Test.Shared/SlowOpenAiCompatibleServer.cs`
    - `src/Test.Shared/SlowGeminiCompatibleServer.cs`
  - Acceptance criteria:
    - Ollama stub supports `/api/generate` empty/native preload and records request count plus `keep_alive`.
    - OpenAI stub verifies `/v1/chat/completions` and `/v1/embeddings` warm requests.
    - Gemini stub verifies `:generateContent` and `:embedContent` warm requests.

- [x] Add shared integration tests.
  - Files:
    - `src/Test.Shared/SharedIntegrationTests.cs`
  - Cases:
    - Load Ollama completion endpoint returns `Loaded`.
    - Load Ollama embedding endpoint returns `Loaded` or documented `Warmed` if implementation uses a normal embed path.
    - Warm OpenAI completion endpoint returns `Warmed`.
    - Warm OpenAI embedding endpoint returns `Warmed`.
    - Warm Gemini completion endpoint returns `Warmed`.
    - Warm Gemini embedding endpoint returns `Warmed`.
    - Warm vLLM endpoint uses OpenAI-compatible behavior.
    - Explicit native load on OpenAI/Gemini/vLLM returns unsupported.
    - Inactive endpoint returns `400`.
    - Missing endpoint returns `404`.
    - Upstream timeout maps to `504`.
    - Concurrency limit maps to `429`.
    - Request history detail includes model-load metadata.

- [x] Wire tests into all runners.
  - Files:
    - `src/Test.XUnit/*`
    - `src/Test.Nunit/*`
    - `src/Test.Automated/*`
  - Acceptance criteria:
    - `dotnet test src/Partio.sln` passes.
    - `src/Test.Automated` includes the new shared cases.

### Dashboard Tests

- [x] Add or update frontend test coverage if the project has a test runner.
  - Completed: dashboard now has Vitest component/unit tests and Playwright responsive smoke tests.
  - Current status:
    - `dashboard/package.json` includes `test` and `test:e2e` scripts.
    - `dashboard/vite.config.js` scopes Vitest to `src/**/*.test.{js,jsx}` with `jsdom`.
    - `dashboard/playwright.config.js` starts Vite on isolated port 8411 for browser smoke tests.
  - Recommended minimum if a unit/component runner is added:
    - [x] Add `vitest`, `@testing-library/react`, and `@testing-library/user-event` with a `test` script.
    - [x] Test `PartioApi.loadEndpoint` sends `POST /v1.0/endpoints/embedding/{id}/load`.
    - [x] Test `PartioApi.loadCompletionEndpoint` sends `POST /v1.0/endpoints/completion/{id}/load`.
    - [x] Test `LoadModelModal` builds request payloads for `strategy`, `timeoutMs`, `keepAlive`, and `sampleInput`.
    - [x] Test success rendering for `Loaded` outcomes.
    - [x] Test failure rendering uses provider-safe messages and keeps retry/close controls visible.
  - Recommended minimum if an end-to-end runner is added instead:
    - [x] Add Playwright with mocked network responses for endpoint list and load calls.
    - [x] Verify both endpoint pages expose `Load Model` row actions.
    - [x] Verify modal submit, loading, success, and close flows without requiring a real model runner.
  - Deferral criteria:
    - Not deferred.
  - Acceptance criteria:
    - API client methods compose the correct paths.
    - Load modal validates timeout, keep-alive, and strategy.
    - Failure state displays provider-safe message.
    - Tests do not require OpenAI, Gemini, Ollama, or vLLM credentials.
    - The chosen script is runnable from `dashboard/` and suitable for CI.

- [x] Run build and responsive checks.
  - `npm.cmd run build` passed on June 5, 2026. The existing Vite chunk-size warning remains advisory.
  - `npm.cmd run test:e2e` passed on June 5, 2026 with six responsive model-load smoke cases.
  - Commands:
    - `npm run build` from `dashboard/`
    - `npm run test` from `dashboard/`
    - `npm run test:e2e` from `dashboard/`
  - Remaining validation tasks:
    - [x] Re-run `npm.cmd run build` after frontend test and layout edits.
    - [x] Complete the responsive layout checklist above.
    - [x] Note whether the existing Vite chunk-size warning is still only advisory or needs a separate follow-up.
  - Acceptance criteria:
    - Build passes.
    - Manual or automated viewport checks cover endpoint views and load modal.
    - Any viewport issue is linked to a specific browser width and a follow-up task or fix.

### SDK Harnesses

- [x] Run SDK harnesses against a local Partio server.
  - Completed: C#, JavaScript, and Python SDK harnesses passed against a self-hosted Partio server and local Ollama-compatible stub on June 5, 2026.
  - Current status:
    - `dotnet run --project src\Test.Automated\Test.Automated.csproj -- --sdk-harnesses` starts an isolated Partio server and upstream stub, then runs all three SDK harnesses.
    - C# SDK harness: 56 passed, 0 failed, 0 skipped.
    - JavaScript SDK harness: 56 passed, 0 failed, 0 skipped.
    - Python SDK harness: 56 passed, 0 failed, 0 skipped.
  - Prerequisites:
    - [x] Start a local Partio server with an admin credential available to the harnesses.
    - [x] Confirm the harnesses can create endpoints that point at a local stub.
    - [x] Add self-hosted stub mode so JS and Python can exercise load methods without external credentials.
  - Commands:
    - `dotnet run --project src\Test.Automated\Test.Automated.csproj -- --sdk-harnesses`
    - `sdk/csharp/go.bat` or `dotnet run --project sdk/csharp/Partio.Sdk.TestHarness -- http://localhost:8400 partioadmin`
    - `node sdk/js/test-harness.js http://localhost:8400 partioadmin`
    - `python sdk/python/test_harness.py http://localhost:8400 partioadmin`
  - Recommended hardening tasks:
    - [x] Make base URL, admin credential, provider URL, and provider model configurable in every harness.
    - [x] Add explicit skip output for provider-dependent load checks when the configured provider is unavailable.
    - [x] Prefer a local stub or test fixture for JS/Python harness load coverage so CI can run without GPU hardware or vendor credentials.
    - [x] Capture command output and update this section with the exact date, environment, and result.
  - Acceptance criteria:
    - Harnesses pass or skip only provider-realism cases with explicit skip messages.
    - At least one load call is executed through each SDK client method, not only through raw HTTP.
    - Any skip states whether it is due to missing Partio server, missing upstream provider, missing model, or credentials.

## Security and Operations

- [x] Threat-model the route before merge.
  - Acceptance criteria:
    - Admin-only authorization confirmed.
    - Provider API keys are never returned in normal responses.
    - Request history redacts auth headers.
    - Load attempts are traceable by request history.
    - Docs warn that model loading can consume GPU/system memory.

- [x] Rate limiting decision.
  - Acceptance criteria:
    - If Partio still relies on upstream reverse proxy rate limiting, docs say so.
    - Dashboard disables duplicate in-flight load attempts per row.
    - Backend concurrency limiter protects upstream calls.

- [x] Multi-instance behavior documented.
  - Acceptance criteria:
    - Docs explain that the Partio instance handling the API call loads or warms the configured upstream host, not every Partio server replica.
    - If multiple upstream model runners sit behind one load-balanced endpoint URL, load/warm reaches whichever upstream the provider load balancer selects.

## Release Checklist

- [x] Backend compiles without warnings introduced by this work.
- [x] `dotnet test src/Partio.sln` passes.
- [x] Dashboard build passes.
- [x] C#, JS, and Python SDK harnesses pass or document environment-dependent skips.
  - Completed on June 5, 2026 with `dotnet run --project src\Test.Automated\Test.Automated.csproj -- --sdk-harnesses`.
  - Results:
    - C# SDK harness: 56 passed, 0 failed, 0 skipped.
    - JavaScript SDK harness: 56 passed, 0 failed, 0 skipped.
    - Python SDK harness: 56 passed, 0 failed, 0 skipped.
  - Completion path:
    - [x] Finish the SDK harness item above.
    - [x] Record commands, server URL, provider mode, date, and pass/skip result.
    - [x] Mark this release checklist item complete only when all three SDK harnesses either pass or emit intentional, documented skips for environment-dependent provider checks.
- [x] `REST_API.md`, `README.md`, `CHANGELOG.md`, SDK READMEs, and Postman collection are updated.
- [x] `/openapi.json` includes both load routes.
- [x] Dashboard shows `Load Model` action for embedding and inference endpoint rows.
- [x] Request history detail captures a load attempt with upstream call details.
- [x] Docs clearly state provider-specific semantics:
  - Ollama: native load/preload.
  - OpenAI/Gemini: warm/validate only.
  - vLLM: warm request to an already-served model.

## First Implementation Slice

Start with the narrow path that proves the design end to end:

1. Add request/response models and `POST /v1.0/endpoints/completion/{id}/load`.
2. Implement Ollama completion native load for `gemma3:4b` with `KeepAlive = "30m"`.
3. Record request history detail and expose the dashboard row action for inference endpoints.
4. Add C#/JS/Python SDK methods for completion endpoint load.
5. Add tests against `SlowOllamaCompatibleServer`.

After that slice is stable, add embedding endpoints and non-Ollama warm behavior. That sequencing keeps the first PR focused on the exact `gemma3:4b` use case while preserving the provider-neutral contract.
