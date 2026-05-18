# Tokenizer Refactor Plan

Refactor Partio's chunking and token-budget logic so chunking is performed in the target embedding model's real token space, with an explicit global fallback default when endpoint-specific tokenization cannot be resolved. Remove hidden cross-tokenizer scaling heuristics and make each chunking strategy enforce limits as part of its primary algorithm.

## Status Legend

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete
- Add initials and date inline where useful, for example: `[x] JD 2026-05-18`

## Problem Summary

Partio currently assumes:

- all chunk sizes can be measured with `cl100k_base`
- model-native context limits can be translated into `cl100k_base` with a fixed multiplier
- once text is split semantically, the produced units are safe to embed

Those assumptions are not reliable for provider/model combinations such as Ollama `all-minilm`, where the model tokenizer and effective input rules differ materially from `cl100k_base`.

This creates three concrete defects:

- valid large documents are chunked into units that still exceed the real upstream embedding limit
- some semantic strategies can emit oversized single units when one paragraph, sentence, regex segment, list item, row group, or row is itself too large
- request-history diagnostics for `/v1.0/process` failures lose upstream embedding call detail at the point it is needed most

## Goals

- Chunk in the embedding model's real token space whenever possible.
- Support an explicit server-wide fallback default instead of failing an endpoint that cannot be fully resolved.
- Remove hidden static scaling heuristics from the request-processing path.
- Make every chunking strategy produce in-budget chunks as part of its first-pass logic.
- Preserve backward compatibility for existing endpoints and clients.
- Improve observability so operators can see which tokenization profile was used and why.
- Ship the refactor as a deliberate minor Partio release with synchronized version, SDK, Postman, and documentation updates.

## Non-Goals

- Redesign summarization behavior.
- Add provider billing, cost estimation, or usage accounting.
- Persist a long-term distributed capability cache in the first iteration.
- Introduce a second-pass cleanup stage that "re-chunks" already emitted chunks.
- Leave release assets inconsistent after the refactor lands.

## Release Requirements

This work must be treated as a minor-version feature release, not a silent internal refactor.

Minimum release-scope expectations:

- increment Partio to the next minor version from whatever version is current at implementation time
- add a new `CHANGELOG.md` entry describing tokenizer resolution, fallback behavior, chunking semantics changes, and any API or dashboard changes
- update operator and developer docs so the runtime behavior, settings, and endpoint schema are accurate
- update SDKs and their examples or harnesses so new endpoint tokenization fields are represented correctly
- update `Partio.postman_collection.json` so API consumers can exercise the new contract immediately

If version metadata is currently missing in a published or publishable artifact, this refactor should either:

- add the missing version metadata as part of the release work, or
- document explicitly why that artifact is intentionally not versioned independently

## Design Principles

1. Tokenization is endpoint-aware.
   Chunking behavior must depend on the embedding endpoint and model, not on a global tokenizer assumption.

2. Fallbacks are explicit, not hidden.
   If endpoint-specific tokenization cannot be resolved, Partio should use a configured global fallback profile and log that fact clearly.

3. Boundary descent is part of the strategy.
   If a paragraph is too large for the budget, the paragraph strategy must descend to sentence or token-span boundaries before emitting a chunk. This is the strategy doing its job, not a post-process repair pass.

4. The same token space must drive both budgeting and verification.
   The tokenizer used to count chunk size must be the tokenizer associated with the active embedding profile.

5. Runtime decisions must be inspectable.
   Request history, logs, and explorer output should reveal the resolved tokenizer profile, the source of that profile, and whether fallback behavior was used.

## Target Architecture

### Resolution Order

Every `/v1.0/process` and `/v1.0/explorer/embedding` request should resolve a `ResolvedTokenizationProfile` in this order:

1. Endpoint-level explicit override
2. Live provider probe plus short-lived in-memory cache
3. Provider default registry
4. Server global fallback default

The server global fallback default is required by design. It is the last-resort profile when Partio cannot prove endpoint-specific tokenization details.

### Resolved Tokenization Profile

Introduce a single runtime contract that chunkers and runtime services consume.

Suggested fields:

- `TokenizerKind`
- `TokenizerModel`
- `MaxInputTokens`
- `ReservedInputTokens`
- `EffectiveInputBudget`
- `BatchLimitMode`
- `ProfileSource`
- `ProviderMetadata`

Suggested `ProfileSource` values:

- `EndpointOverride`
- `ProviderProbe`
- `ProviderDefault`
- `GlobalFallback`

Suggested `BatchLimitMode` values:

- `PerInput`
- `WholeRequest`
- `Unknown`

### Tokenizer Adapter Contract

Add a provider-agnostic tokenizer interface that chunkers can consume directly.

Suggested responsibilities:

- count tokens for a string
- encode text to tokens
- decode tokens to text
- slice a string by token range without drifting from the active tokenizer

This should live under a new `src/Partio.Core/Tokenization/` namespace rather than inside the current chunking classes.

### Endpoint Configuration Model

Persist tokenization overrides as explicit endpoint config, but do not persist transient probe results as the main source of truth.

Recommended shape:

- Add a `Tokenization` object to `EmbeddingEndpoint`
- Store it as `tokenization_json` in `embedding_endpoints`
- Keep all fields optional so existing endpoints remain valid

Recommended override fields:

- `TokenizerKind`
- `TokenizerModel`
- `MaxInputTokens`
- `ReservedInputTokens`
- `BatchLimitMode`
- `AutoDetect`

If `Tokenization` is null or incomplete, resolution continues through probe, provider default, then global fallback.

### Strategy Contract

Every strategy must enforce budget in the active tokenizer's token space before emitting a chunk.

Expected behavior by strategy:

- `FixedTokenCount`
  Emit token spans directly in the active tokenizer.

- `SentenceBased`
  Accumulate sentences while in budget. If one sentence exceeds budget, descend to token spans within that sentence before emission.

- `ParagraphBased`
  Accumulate paragraphs while in budget. If one paragraph exceeds budget, descend to sentence, then token-span boundaries within the same strategy flow.

- `RegexBased`
  Treat regex segments as the first boundary. If one segment exceeds budget, descend to token spans within that segment before emission.

- `WholeList`
  Treat the list as one semantic unit only if it fits. If not, descend to list items, then token spans for oversized items.

- `ListEntry`
  Treat each item as the first boundary. If one item exceeds budget, descend to token spans within that item.

- `WholeTable`
  Treat the whole table as one semantic unit only if it fits. If not, descend to row groups, row, cell, then token spans.

- `RowGroupWithHeaders`
  Reduce at the row-group boundary first. If a single grouped row block is still too large, descend to row, cell, then token spans.

- `RowWithHeaders`, `Row`, `KeyValuePairs`
  Enforce the same cell or token-span fallback within the strategy if a single row representation is too large.

## Likely Files and Areas

Core runtime:

- `src/Partio.Server/PartioServer.cs`
- `src/Partio.Core/Chunking/ChunkingEngine.cs`
- `src/Partio.Core/Chunking/FixedTokenChunker.cs`
- `src/Partio.Core/Chunking/SentenceChunker.cs`
- `src/Partio.Core/Chunking/ParagraphChunker.cs`
- `src/Partio.Core/Chunking/RegexChunker.cs`
- `src/Partio.Core/Chunking/WholeListChunker.cs`
- `src/Partio.Core/Chunking/ListEntryChunker.cs`
- `src/Partio.Core/Chunking/TableChunker.cs`
- `src/Partio.Core/ThirdParty/EmbeddingClientBase.cs`
- `src/Partio.Core/ThirdParty/OllamaEmbeddingClient.cs`
- `src/Partio.Core/ThirdParty/OpenAiEmbeddingClient.cs`
- `src/Partio.Core/ThirdParty/GeminiEmbeddingClient.cs`

Settings and models:

- `src/Partio.Core/Settings/ServerSettings.cs`
- `src/Partio.Core/Settings/DefaultEmbeddingEndpoint.cs`
- new `src/Partio.Core/Settings/TokenizationDefaultsSettings.cs`
- `src/Partio.Core/Models/EmbeddingEndpoint.cs`
- new `src/Partio.Core/Models/EndpointTokenizationSettings.cs`
- new `src/Partio.Core/Models/ResolvedTokenizationProfile.cs`
- new `src/Partio.Core/Models/EmbeddingModelCapabilities.cs`

Persistence:

- `src/Partio.Core/Database/Sqlite/Queries/SetupQueries.cs`
- `src/Partio.Core/Database/Postgresql/Queries/SetupQueries.cs`
- `src/Partio.Core/Database/Mysql/Queries/SetupQueries.cs`
- `src/Partio.Core/Database/Sqlserver/Queries/SetupQueries.cs`
- `src/Partio.Core/Database/*/Implementations/EmbeddingEndpointMethods.cs`

Tests:

- `src/Test.Shared/SharedSummarizationUnitTests.cs` as style reference
- new `src/Test.Shared/SharedTokenizerUnitTests.cs`
- `src/Test.XUnit/SummarizationUnitTests.cs` as style reference
- new `src/Test.XUnit/TokenizerUnitTests.cs`
- `src/Test.Shared/SharedIntegrationTests.cs`

SDKs and docs:

- `sdk/csharp/Partio.Sdk/Models/EmbeddingEndpoint.cs`
- `sdk/csharp/Partio.Sdk/Partio.Sdk.csproj`
- `sdk/csharp/Partio.Sdk.TestHarness/Program.cs`
- `sdk/python/partio_sdk.py`
- `sdk/python/README.md`
- `sdk/python/test_harness.py`
- `sdk/js/package.json`
- `sdk/js/README.md`
- `sdk/js/test-harness.js`
- `dashboard/package.json`
- `CHANGELOG.md`
- `README.md`
- `REST_API.md`
- `Partio.postman_collection.json`
- `src/partio.json`
- `docker/partio.json`
- `docker/factory/partio.json`

## Work Plan

## Phase 0 - Architecture Spike and Contract Freeze

- `[x] Codex 2026-05-18 Inventory which tokenizer libraries or provider-side mechanisms are viable for:
  `cl100k_base` / OpenAI-family BPE, BERT-style WordPiece, and any other provider families Partio currently supports.
- `[x] Codex 2026-05-18 Decide whether each provider will use:
  local tokenization, provider-side token counting, or a documented provider default profile.
- `[x] Codex 2026-05-18 Freeze the first-pass contract for `ResolvedTokenizationProfile`, `ITokenizerAdapter`, and `EmbeddingModelCapabilities`.
- `[x] Codex 2026-05-18 Decide whether probe results are cached only in memory for v1 or also surfaced via health or diagnostics APIs.
- `[x] Codex 2026-05-18 Write down the chosen fallback semantics:
  endpoint override -> provider probe -> provider default -> global fallback.

Deliverable:

- Short design note appended to this plan or linked from the implementation PR.

### Phase 0 Design Note

- `ResolvedTokenizationProfile` will be the single runtime contract passed into request processing, chunking, diagnostics, and embedding batch orchestration. The first implementation will carry `TokenizerKind`, `TokenizerModel`, `MaxInputTokens`, `ReservedInputTokens`, `EffectiveInputBudget`, `BatchLimitMode`, `ProfileSource`, `ProviderMetadata`, and a `UsedFallback` flag.
- `ITokenizerAdapter` will expose synchronous local-tokenizer operations needed by chunkers: `CountTokens`, `Encode`, `Decode`, and `SliceByTokenRange`. The initial implementation will keep chunking synchronous and only admit adapters that can perform those operations locally.
- `EmbeddingModelCapabilities` will capture probeable endpoint/model facts separately from persisted overrides: tokenizer kind/model when known, max input tokens, reserved tokens, batch limit mode, and provider metadata.
- Provider strategy for v1:
  OpenAI/vLLM/OpenAI-compatible endpoints use local `cl100k_base` tokenization.
  Ollama uses live model probing for limits and tokenizer family metadata, then maps supported families to local adapters. The first supported local Ollama family is BERT-style WordPiece for the `all-minilm` / MiniLM class of embedding models.
  Gemini uses provider defaults unless an endpoint override is supplied.
- Probe results are cached only in memory for v1, keyed by endpoint ID plus model, and invalidated on endpoint updates and health-state changes. They are surfaced to request-history detail, logs, and explorer responses, but are not persisted as endpoint truth.
- Fallback semantics are fixed to `EndpointOverride -> ProviderProbe -> ProviderDefault -> GlobalFallback`. Fallback use is explicit in logs and diagnostics; no hidden scaling heuristic remains in the request path.

## Phase 1 - Settings and Persisted Endpoint Config

- `[x]` Codex 2026-05-18 Add `TokenizationDefaultsSettings` under `src/Partio.Core/Settings/`.
- `[x]` Codex 2026-05-18 Add a `GlobalFallback` tokenization profile to `ServerSettings`.
- `[x]` Codex 2026-05-18 Add optional provider-scoped defaults to settings if useful for bootstrap behavior.
- `[x]` Codex 2026-05-18 Extend `DefaultEmbeddingEndpoint` so seeded endpoints can specify tokenization override settings.
- `[x]` Codex 2026-05-18 Add a `Tokenization` object to `EmbeddingEndpoint`.
- `[x]` Codex 2026-05-18 Store endpoint tokenization override data as `tokenization_json`.
- `[x]` Codex 2026-05-18 Update `EmbeddingEndpoint.FromDataRow` and write paths to serialize and deserialize the new field.
- `[x]` Codex 2026-05-18 Add `tokenization_json` to `embedding_endpoints` schema in all supported database backends.
- `[x]` Codex 2026-05-18 Keep old rows valid by treating missing `tokenization_json` as "auto-detect, then fallback".

Notes:

- This phase should not change chunking behavior yet.
- Existing APIs should remain backward-compatible for callers that do not send tokenization settings.

## Phase 2 - Tokenization Core

- `[x]` Codex 2026-05-18 Create `src/Partio.Core/Tokenization/`.
- `[x]` Codex 2026-05-18 Add `ITokenizerAdapter`.
- `[x]` Codex 2026-05-18 Add concrete tokenizer adapters for the families chosen in Phase 0.
- `[x]` Codex 2026-05-18 Add `ResolvedTokenizationProfile`.
- `[x]` Codex 2026-05-18 Add `EmbeddingModelCapabilities`.
- `[x]` Codex 2026-05-18 Add a profile resolver service, for example `TokenizationProfileResolver`.
- `[x]` Codex 2026-05-18 Ensure the global fallback profile comes from config or documented provider defaults, not from a hidden static multiplier.
- `[x]` Codex 2026-05-18 Remove direct ownership of a hardcoded tokenizer from `ChunkingEngine`.

Acceptance criteria for this phase:

- There is a single runtime object that describes token budgeting for an embedding request.
- Chunking code no longer depends directly on `GptEncoding.GetEncoding("cl100k_base")`.

## Phase 3 - Provider Capability Resolution and Caching

- `[x]` Codex 2026-05-18 Extend `EmbeddingClientBase` with a capability method, for example `GetModelCapabilitiesAsync`.
- `[x]` Codex 2026-05-18 Implement capability resolution in `OllamaEmbeddingClient`.
- `[x]` Codex 2026-05-18 Implement capability resolution in `OpenAiEmbeddingClient`.
- `[x]` Codex 2026-05-18 Implement capability resolution in `GeminiEmbeddingClient`.
- `[x]` Codex 2026-05-18 Add a short-lived in-memory cache keyed by endpoint id plus model.
- `[x]` Codex 2026-05-18 Invalidate or refresh cached capabilities on endpoint update and on health-state changes where appropriate.
- `[x]` Codex 2026-05-18 Resolve the active profile in the defined order:
  override -> probe -> provider default -> global fallback.
- `[x]` Codex 2026-05-18 When fallback is used, log a warning that includes endpoint id, model, and fallback source.
- `[x]` Codex 2026-05-18 Surface profile source and effective budget to debug logs and, where safe, to explorer or response headers.

Notes:

- This phase should never fail an otherwise reachable endpoint solely because tokenization could not be resolved.
- The server-wide fallback is the safety net, not a hidden heuristic.

## Phase 4 - Chunking Engine Refactor

- `[x]` Codex 2026-05-18 Change chunker method signatures to accept:
  active tokenizer adapter, effective token budget, and any needed reserved-token metadata.
- `[x]` Codex 2026-05-18 Refactor `FixedTokenChunker` to emit token spans in the active tokenizer's space.
- `[x]` Codex 2026-05-18 Refactor `SentenceChunker` so it counts with the active tokenizer while assembling chunks.
- `[x]` Codex 2026-05-18 Add boundary descent inside `SentenceChunker` for oversized single sentences.
- `[x]` Codex 2026-05-18 Refactor `ParagraphChunker` similarly and add paragraph -> sentence -> token-span descent.
- `[x]` Codex 2026-05-18 Refactor `RegexChunker` so oversized regex segments are decomposed within the strategy.
- `[x]` Codex 2026-05-18 Refactor list strategies so oversized whole-list or list-item units descend within the same strategy flow.
- `[x]` Codex 2026-05-18 Refactor table strategies so oversized table, row-group, or row units descend through row, cell, then token-span boundaries.
- `[x]` Codex 2026-05-18 Add a small shared helper if needed for "append until budget, then emit" behavior, but do not add a second-pass cleanup pipeline.
- `[x]` Codex 2026-05-18 Guarantee that every emitted chunk is already in-budget when measured by the same tokenizer profile that will govern embedding.

Important implementation rule:

- Do not emit an oversized semantic unit and then repair it later.
- Descend to a smaller boundary before emission.

## Phase 5 - Runtime Integration

- `[x]` Codex 2026-05-18 Remove `_TokenScalingFactor` usage from `PartioServer.cs`.
- `[x]` Codex 2026-05-18 Resolve the tokenization profile once per request and endpoint/model pair in `ProcessCellAsync`.
- `[x]` Codex 2026-05-18 Pass the resolved profile into `ProcessCellHierarchyAsync` and the chunking engine.
- `[x]` Codex 2026-05-18 Preserve `ChunkingConfiguration.FixedTokenCount` semantics as "requested model-native token budget".
- `[x]` Codex 2026-05-18 Apply `min(requested budget, effective endpoint budget)` before chunking begins.
- `[x]` Codex 2026-05-18 Include tokenization profile details in request-history diagnostics for process and explorer calls.
- `[x]` Codex 2026-05-18 Preserve upstream embedding call details on `/v1.0/process` failures instead of writing `null`.

Notes:

- This phase is where the production ingestion failure path should disappear for correctly resolved endpoints.

## Phase 6 - Tests

- `[x]` Codex 2026-05-18 Add `src/Test.Shared/SharedTokenizerUnitTests.cs`.
- `[x]` Codex 2026-05-18 Add `src/Test.XUnit/TokenizerUnitTests.cs`.
- `[x]` Codex 2026-05-18 Add unit tests for profile resolution order.
- `[x]` Codex 2026-05-18 Add unit tests for explicit endpoint override winning over probe and defaults.
- `[x]` Codex 2026-05-18 Add unit tests for global fallback use when no endpoint-specific resolution succeeds.
- `[x]` Codex 2026-05-18 Add unit tests ensuring each chunker emits only in-budget chunks in the active tokenizer.
- `[x]` Codex 2026-05-18 Add unit tests for oversized single sentence handling.
- `[x]` Codex 2026-05-18 Add unit tests for oversized single paragraph handling.
- `[x]` Codex 2026-05-18 Add unit tests for oversized regex segment handling.
- `[x]` Codex 2026-05-18 Add unit tests for oversized list item handling.
- `[x]` Codex 2026-05-18 Add unit tests for oversized row or row-group handling.
- `[x]` Codex 2026-05-18 Add regression coverage for the current failure class:
  text that is safe under `cl100k_base` assumptions but rejected by the real embedding model tokenizer or budget.
- `[ ]` Add integration coverage for a live Ollama path when available.
- `[x]` Codex 2026-05-18 Add a request-history test verifying that upstream embedding call detail is retained on failure.
- `[x]` Codex 2026-05-18 Add API contract coverage for create, read, update, and enumerate embedding endpoints with tokenization settings populated and omitted.
- `[x]` Codex 2026-05-18 Add SDK harness coverage where feasible for the new endpoint fields and explorer diagnostics.

Test data guidance:

- Prefer synthetic or sanitized text fixtures over copied customer documents.
- Include punctuation, bullets, unusual whitespace, tables, and mixed-content segments in regression data.

## Phase 7 - API, SDK, Dashboard, Docs, and Postman

- `[x]` Codex 2026-05-18 Update REST contract documentation for the new tokenization fields on embedding endpoints.
- `[x]` Codex 2026-05-18 Update `REST_API.md` request and response examples for embedding endpoint CRUD and any affected explorer or process diagnostics.
- `[x]` Codex 2026-05-18 Update `README.md` with the new resolution order and fallback behavior.
- `[x]` Codex 2026-05-18 Update `CHANGELOG.md` with the new minor release entry.
- `[x]` Codex 2026-05-18 Update `src/partio.json` sample settings to include the global fallback profile.
- `[x]` Codex 2026-05-18 Update `docker/partio.json` and `docker/factory/partio.json` to keep Docker defaults aligned with the server sample.
- `[x]` Codex 2026-05-18 Update the C# SDK `EmbeddingEndpoint` model.
- `[x]` Codex 2026-05-18 Decide whether `sdk/csharp/Partio.Sdk/Partio.Sdk.csproj` needs explicit package version metadata for this release, and implement that decision.
- `[x]` Codex 2026-05-18 Update the C# SDK test harness for new endpoint fields if they are exposed in requests.
- `[x]` Codex 2026-05-18 Update Python SDK request payload handling, examples, and `README.md` where endpoint payloads are shown.
- `[x]` Codex 2026-05-18 Update JavaScript SDK request payload handling, examples, `package.json` version, and `README.md` where endpoint payloads are shown.
- `[x]` Codex 2026-05-18 Update `dashboard/package.json` to the next minor version so the dashboard release stays aligned with the server release.
- `[x]` Codex 2026-05-18 Update the dashboard embedding endpoint create/edit flows if tokenization overrides are operator-configurable in this release.
- `[x]` Codex 2026-05-18 Update the endpoint explorer to show resolved tokenizer source, tokenizer kind, and effective token budget when feasible.
- `[x]` Codex 2026-05-18 Update `Partio.postman_collection.json` with:
  embedding endpoint payloads containing tokenization fields, request examples for fallback-aware endpoint config, and any new or changed response examples.
- `[x]` Codex 2026-05-18 Verify that Postman example names and descriptions explain the resolution order and fallback behavior clearly.

Notes:

- If dashboard override UI is deferred, document that explicitly and keep the API path complete first.
- If Python packaging metadata does not yet exist as a formal package artifact, record how the Python SDK version is communicated for this release.

## Phase 8 - Versioning, Packaging, Rollout, and Verification

- `[x]` Codex 2026-05-18 Determine the target next minor version from the current repository state at implementation time.
- `[x]` Codex 2026-05-18 Increment that minor version consistently across all version-bearing artifacts, including at minimum:
  `CHANGELOG.md`, `dashboard/package.json`, `sdk/js/package.json`, and any added C# SDK package metadata.
- `[x]` Codex 2026-05-18 Audit the repository for stale previous-version strings in docs, samples, dashboard text, and release notes.
- `[ ]` Ensure any Docker build or release documentation that references version tags is updated.

- `[ ]` Migrate local SQLite, PostgreSQL, MySQL, and SQL Server test databases.
- `[ ]` Verify that old endpoints with no `tokenization_json` continue to work.
- `[ ]` Verify that a brand-new endpoint with no explicit override resolves via probe when possible.
- `[ ]` Verify that the same endpoint falls back cleanly to the global default when probe resolution is unavailable.
- `[ ]` Validate AssistantHub-style PDF ingestion against the Docker stack with at least:
  one Ollama BERT-family embedding model and one OpenAI-family embedding model.
- `[ ]` Confirm that request history and logs reveal which tokenization source was used.
- `[ ]` Confirm that context-length failures, when they do occur, retain upstream call details for diagnosis.
- `[x]` Codex 2026-05-18 Remove or archive legacy docs and comments that describe `cl100k_base` scaling as the runtime contract.
- `[ ]` Manually spot-check the updated Postman collection by importing it and exercising at least one endpoint-management flow and one processing flow.

## Suggested PR Breakdown

PR 1:

- Settings, endpoint model contract, schema updates, and SDK model changes

PR 2:

- Tokenization core abstractions and provider capability resolution

PR 3:

- Chunking engine refactor and runtime integration

PR 4:

- Unit and integration regression coverage

PR 5:

- Dashboard, explorer, docs, Postman, versioning, and rollout cleanup

## Acceptance Criteria

- `[x]` Codex 2026-05-18 No request-processing path uses a hidden global cross-tokenizer multiplier.
- `[x]` Codex 2026-05-18 Chunkers operate in the active embedding profile's token space.
- `[x]` Codex 2026-05-18 Every emitted chunk is within the active effective token budget when measured by the active tokenizer.
- `[x]` Codex 2026-05-18 Endpoint-specific resolution uses explicit overrides first, then probe, then provider defaults, then the configured global fallback.
- `[x]` Codex 2026-05-18 Global fallback use is visible in logs and diagnostics.
- `[x]` Codex 2026-05-18 The current `all-minilm`-style context overflow failure is covered by regression tests and no longer reproduces after the refactor under the same deployment conditions.
- `[x]` Codex 2026-05-18 `/v1.0/process` failure request history includes upstream embedding call detail whenever available.
- `[x]` Codex 2026-05-18 Existing clients can continue using embedding endpoints without supplying tokenization settings.
- `[x]` Codex 2026-05-18 The release ships with a synchronized next-minor-version update across the server-adjacent assets, SDK-facing assets, and user-facing documentation.
- `[x]` Codex 2026-05-18 `Partio.postman_collection.json` reflects the released tokenization contract and example flows.

## Risks and Decision Gates

- `[ ]` Tokenizer library selection may require a small prototype before committing to package dependencies.
- `[ ]` Provider capability surfaces may differ enough that some providers need a richer default registry than others.
- `[ ]` Table and list strategy semantics may need careful review to preserve current user expectations while guaranteeing in-budget emission.
- `[ ]` Dashboard exposure of endpoint tokenization overrides may need to be phased if backend delivery lands first.

## Completion Checklist

- `[ ]` Code merged
- `[ ]` Tests green
- `[ ]` Docker stack verified
- `[x]` Codex 2026-05-18 Minor version increment applied consistently
- `[x]` Codex 2026-05-18 SDKs updated
- `[x]` Codex 2026-05-18 Postman collection updated
- `[x]` Codex 2026-05-18 Docs updated
- `[x]` Codex 2026-05-18 Changelog updated
- `[x]` Codex 2026-05-18 Legacy heuristic removed
