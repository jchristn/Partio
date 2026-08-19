# Changelog

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
