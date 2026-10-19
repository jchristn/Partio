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
{ "Status": "Healthy", "Version": "0.5.2" }
```

### GET /v1.0/health
Health status JSON (no auth required).

**Response**: `200 OK`
```json
{ "Status": "Healthy", "Version": "0.5.2" }
```

---

## Identity

### GET /v1.0/whoami
Returns the role and tenant of the authenticated caller.

**Response**: `200 OK`
```json
{ "Role": "Admin", "TenantName": "Admin" }
```
- `Role` â€” `"Admin"` or `"User"`
- `TenantName` â€” `"Admin"` for global admins, or the tenant's name

---

## Process (Chunk & Embed)

### POST /v1.0/process
Process a single semantic cell. Requires bearer token authentication.

**Request Body**: `SemanticCellRequest`

`FixedTokenCount` is interpreted in the selected embedding endpoint's active token space. Partio resolves that tokenization profile in this order: endpoint override, provider probe, provider default, global fallback.

The `Type` field determines which content field is used. Supported types: `Text`, `List`, `Table`, `Code`, `Hyperlink`, `Meta`.

#### Text
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
        "EmbeddingEndpointId": "eep_xxxx",
        "L2Normalization": true
    },
    "Labels": ["label1"],
    "Tags": { "key": "value" }
}
```

#### Text with Summarization
```json
{
    "Type": "Text",
    "Text": "Your long text content here...",
    "ChunkingConfiguration": {
        "Strategy": "SentenceBased"
    },
    "EmbeddingConfiguration": {
        "EmbeddingEndpointId": "eep_xxxx"
    },
    "SummarizationConfiguration": {
        "CompletionEndpointId": "cep_xxxx",
        "Order": "TopDown",
        "MaxSummaryTokens": 1024,
        "MinCellLength": 100,
        "MaxParallelTasks": 4,
        "MaxRetries": 10,
        "MaxRetriesPerSummary": 2,
        "TimeoutMs": 30000
    }
}
```

When `SummarizationConfiguration` is present, Partio generates summary child cells using the specified completion endpoint before chunking and embedding. The `CompletionEndpointId` is required; all other fields have defaults.

#### Unordered List
```json
{
    "Type": "List",
    "UnorderedList": ["First item", "Second item", "Third item"],
    "ChunkingConfiguration": {
        "Strategy": "WholeList"
    },
    "EmbeddingConfiguration": {
        "EmbeddingEndpointId": "eep_xxxx",
        "L2Normalization": false
    }
}
```

#### Ordered List
```json
{
    "Type": "List",
    "OrderedList": ["Step one", "Step two", "Step three"],
    "ChunkingConfiguration": {
        "Strategy": "ListEntry"
    },
    "EmbeddingConfiguration": {
        "EmbeddingEndpointId": "eep_xxxx",
        "L2Normalization": false
    }
}
```

#### Table (RowWithHeaders)
```json
{
    "Type": "Table",
    "Table": [
        ["Name", "Age", "City"],
        ["Alice", "30", "New York"],
        ["Bob", "25", "London"]
    ],
    "ChunkingConfiguration": {
        "Strategy": "RowWithHeaders"
    },
    "EmbeddingConfiguration": {
        "EmbeddingEndpointId": "eep_xxxx",
        "L2Normalization": false
    }
}
```

#### Code
```json
{
    "Type": "Code",
    "Text": "function hello() {\n  return 'world';\n}",
    "ChunkingConfiguration": {
        "Strategy": "FixedTokenCount",
        "FixedTokenCount": 256
    },
    "EmbeddingConfiguration": {
        "EmbeddingEndpointId": "eep_xxxx",
        "L2Normalization": false
    }
}
```

#### Hyperlink
```json
{
    "Type": "Hyperlink",
    "Text": "https://example.com - Example website description",
    "ChunkingConfiguration": {
        "Strategy": "FixedTokenCount",
        "FixedTokenCount": 256
    },
    "EmbeddingConfiguration": {
        "EmbeddingEndpointId": "eep_xxxx",
        "L2Normalization": false
    }
}
```

#### Meta
```json
{
    "Type": "Meta",
    "Text": "Author: John Doe | Created: 2026-01-15 | Version: 2.1",
    "ChunkingConfiguration": {
        "Strategy": "FixedTokenCount",
        "FixedTokenCount": 256
    },
    "EmbeddingConfiguration": {
        "EmbeddingEndpointId": "eep_xxxx",
        "L2Normalization": false
    }
}
```

#### Table (Row)
```json
{
    "Type": "Table",
    "Table": [
        ["Name", "Age", "City"],
        ["Alice", "30", "New York"],
        ["Bob", "25", "London"]
    ],
    "ChunkingConfiguration": {
        "Strategy": "Row"
    },
    "EmbeddingConfiguration": {
        "EmbeddingEndpointId": "eep_xxxx",
        "L2Normalization": false
    }
}
```

Each data row becomes a chunk of space-separated values: `"Alice 30 New York"`.

#### Table (RowGroupWithHeaders)
```json
{
    "Type": "Table",
    "Table": [
        ["Name", "Age", "City"],
        ["Alice", "30", "New York"],
        ["Bob", "25", "London"],
        ["Carol", "35", "Paris"]
    ],
    "ChunkingConfiguration": {
        "Strategy": "RowGroupWithHeaders",
        "RowGroupSize": 2
    },
    "EmbeddingConfiguration": {
        "EmbeddingEndpointId": "eep_xxxx",
        "L2Normalization": false
    }
}
```

Groups of `RowGroupSize` rows with headers prepended as a markdown table. Default `RowGroupSize` is 5.

#### Table (KeyValuePairs)
```json
{
    "Type": "Table",
    "Table": [
        ["Name", "Age", "City"],
        ["Alice", "30", "New York"],
        ["Bob", "25", "London"]
    ],
    "ChunkingConfiguration": {
        "Strategy": "KeyValuePairs"
    },
    "EmbeddingConfiguration": {
        "EmbeddingEndpointId": "eep_xxxx",
        "L2Normalization": false
    }
}
```

Each data row becomes: `"Name: Alice, Age: 30, City: New York"`.

#### Table (WholeTable)
```json
{
    "Type": "Table",
    "Table": [
        ["Name", "Age", "City"],
        ["Alice", "30", "New York"],
        ["Bob", "25", "London"]
    ],
    "ChunkingConfiguration": {
        "Strategy": "WholeTable"
    },
    "EmbeddingConfiguration": {
        "EmbeddingEndpointId": "eep_xxxx",
        "L2Normalization": false
    }
}
```

Entire table serialized as a single markdown table chunk.

#### Text with RegexBased (Markdown headings)
```json
{
    "Type": "Text",
    "Text": "# Introduction\nPartio is a chunking platform.\n\n# Architecture\nPartio uses a ...\n\n# Deployment\nUse Docker Compose to ...",
    "ChunkingConfiguration": {
        "Strategy": "RegexBased",
        "RegexPattern": "(?=^#{1,3}\\s)",
        "FixedTokenCount": 512
    },
    "EmbeddingConfiguration": {
        "EmbeddingEndpointId": "eep_xxxx",
        "L2Normalization": false
    }
}
```

Split at boundaries defined by the `RegexPattern`. Text is split using `Regex.Split` at every match. Useful for Markdown headings, log timestamps, LaTeX sections, function definitions, and more.

**Response**: `200 OK` â€” `SemanticCellResponse`
```json
{
    "GUID": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
    "ParentGUID": null,
    "Type": "Text",
    "Text": "Your text content here...",
    "Children": [],
    "Chunks": [
        {
            "CellGUID": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            "Text": "Your text content here...",
            "Labels": ["label1"],
            "Tags": { "key": "value" },
            "Embeddings": [-0.4418, 0.1234, ...]
        }
    ]
}
```

Successful embedding responses also include these headers when tokenization resolution is applicable:

| Header | Description |
|--------|-------------|
| `X-Partio-Tokenizer-Kind` | Resolved tokenizer family used for chunk counting and slicing |
| `X-Partio-Tokenizer-Model` | Resolved tokenizer model or vocabulary identifier |
| `X-Partio-Tokenizer-Source` | Resolution source: `EndpointOverride`, `ProviderProbe`, `ProviderDefault`, or `GlobalFallback` |
| `X-Partio-Effective-Input-Budget` | Effective per-input token budget after reserved tokens are removed |

**Errors**:
- `404 Not Found` - EmbeddingEndpointId not found or does not belong to the caller's tenant
- `400 Bad Request` - Endpoint is inactive, request body is missing/invalid, or strategy is incompatible with atom type
- `502 Bad Gateway` - The selected endpoint is currently unhealthy
- `504 Gateway Timeout` - The upstream embedding or inference provider exceeded the configured endpoint timeout

#### Strategy-to-Type Validation

The API validates that the chunking strategy is compatible with the atom type. Incompatible combinations return `400 Bad Request`.

- **Generic strategies** (`FixedTokenCount`, `SentenceBased`, `ParagraphBased`, `RegexBased`) work with all types
- **List strategies** (`WholeList`, `ListEntry`) only work with `List`
- **Table strategies** (`Row`, `RowWithHeaders`, `RowGroupWithHeaders`, `KeyValuePairs`, `WholeTable`) only work with `Table`

Example error response for missing `RegexPattern`:
```json
{
    "Error": "BadRequest",
    "Message": "RegexPattern is required when using the RegexBased strategy.",
    "StatusCode": 400
}
```

Example error response for invalid `RegexPattern`:
```json
{
    "Error": "BadRequest",
    "Message": "RegexPattern is not a valid regular expression: parsing '([' - Unterminated [] set.",
    "StatusCode": 400
}
```

Example error response for using `Row` strategy on a `Text` type:
```json
{
    "Error": "BadRequest",
    "Message": "Strategy 'Row' is only compatible with atom type 'Table', but got 'Text'.",
    "StatusCode": 400
}
```

#### ChunkingConfiguration Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Strategy` | string | `FixedTokenCount` | Chunking strategy to use |
| `FixedTokenCount` | int | `256` | Requested chunk budget in the active embedding endpoint's token space |
| `OverlapCount` | int | `0` | Overlap tokens between chunks |
| `OverlapPercentage` | float? | `null` | Overlap as percentage (0.0-1.0) |
| `OverlapStrategy` | string | `SlidingWindow` | Overlap boundary strategy |
| `ContextPrefix` | string? | `null` | Prefix prepended to each chunk |
| `RowGroupSize` | int | `5` | Rows per group (for RowGroupWithHeaders). Minimum: 1 |
| `RegexPattern` | string? | `null` | Regular expression split pattern (required for `RegexBased` strategy). Text is split at every match of this pattern. |

#### EmbeddingConfiguration Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EmbeddingEndpointId` | string | *(required)* | The embedding endpoint ID to use for generating embeddings (e.g. `eep_xxxx`). The endpoint must belong to the caller's tenant (non-admin) and be active. |
| `L2Normalization` | bool | `false` | Whether to L2-normalize the embedding vectors |

### POST /v1.0/process/batch
Process multiple semantic cells.

**Request Body**: `List<SemanticCellRequest>`

**Response**: `200 OK` — `List<SemanticCellResponse>`

---

### POST /v1.0/chunk
Chunk a single semantic cell into text chunks **without embedding them**. Unlike `/v1.0/process`, this
requires no embedding endpoint: chunking uses a built-in `cl100k_base` tokenizer to honor the token budget,
so the token budget comes straight from `ChunkingConfiguration.FixedTokenCount`. Useful for running (and
timing) chunking independently of embedding.

**Request Body**: `ChunkRequest`

```json
{
  "Type": "Text",
  "Text": "Long text to split into chunks ...",
  "ChunkingConfiguration": { "Strategy": "FixedTokenCount", "FixedTokenCount": 256, "OverlapCount": 0 },
  "Labels": [],
  "Tags": {}
}
```

`ChunkRequest` mirrors the content and `ChunkingConfiguration` fields of `SemanticCellRequest` but omits
`EmbeddingConfiguration` and `SummarizationConfiguration`. See [ChunkingConfiguration Properties](#chunkingconfiguration-properties).

**Response**: `200 OK` — `ChunkResponse`

```json
{
  "GUID": "…",
  "Type": "Text",
  "Text": "Long text to split into chunks ...",
  "Chunks": [ { "CellGUID": "…", "Text": "chunk 1 text", "Labels": [], "Tags": {}, "Embeddings": [] } ],
  "Count": 1
}
```

Each returned chunk has an empty `Embeddings` array (this endpoint never embeds).

**Errors**: `400` (e.g. a `RegexBased` strategy with a missing/invalid `RegexPattern`, or a `ContextPrefix`
that consumes the entire token budget), `401`.

---

### POST /v1.0/embed
Generate embedding vectors for one or more input strings using a configured embedding endpoint, **without
chunking**. Each input is embedded as-is and returned in order.

**Request Body**: `EmbedRequest`

```json
{
  "EndpointId": "eep_...",
  "Input": [ "first input", "second input" ],
  "L2Normalization": false
}
```

**Response**: `200 OK` — `EmbedResponse`

```json
{
  "Success": true,
  "StatusCode": 200,
  "EndpointId": "eep_...",
  "Model": "text-embedding-3-small",
  "Embeddings": [ [0.1, 0.2, 0.3], [0.4, 0.5, 0.6] ],
  "Count": 2,
  "Dimensions": 3,
  "L2Normalization": false,
  "ResponseTimeMs": 31.0,
  "EmbeddingCalls": [ ],
  "TokenizationProfile": { }
}
```

**Errors**: `400` (missing `EndpointId` or empty `Input`), `401`, `404` (unknown endpoint).

---

### POST /v1.0/completion
Generate a completion for a prompt using a configured completion endpoint. This is the production inference
path — a **single upstream call** — suitable for validating that an endpoint is reachable and working. Unlike
`/v1.0/explorer/completion`, it performs no additional diagnostics.

**Request Body**: `CompletionRequest`

```json
{
  "EndpointId": "cep_...",
  "Prompt": "Say hello in one word.",
  "SystemPrompt": null,
  "MaxTokens": 512,
  "TimeoutMs": 60000
}
```

**Response**: `200 OK` — `CompletionResponse`

```json
{
  "Success": true,
  "StatusCode": 200,
  "EndpointId": "cep_...",
  "Model": "gpt-4.1-mini",
  "Prompt": "Say hello in one word.",
  "SystemPrompt": null,
  "Output": "Hello",
  "ResponseTimeMs": 128.0,
  "CompletionCalls": [ ]
}
```

On an upstream failure such as a timeout, the route still returns `200 OK` with `Success=false` and
`StatusCode` set to the upstream failure code (for example `504`); `TimeoutMs` is clamped to a positive
value and to the endpoint's `MaximumTimeoutMs`.

**Errors**: `400` (missing `EndpointId` or `Prompt`), `401`, `404` (unknown endpoint), `429` (endpoint
concurrency limit reached).

---

### POST /v1.0/summarize
Summarize a piece of text through a completion endpoint, **without chunking or embedding**. Uses the same
summarization engine as `/v1.0/process`.

**Request Body**: `SummarizeRequest`

```json
{
  "Text": "Long text to summarize ...",
  "SummarizationConfiguration": {
    "CompletionEndpointId": "cep_...",
    "Order": "BottomUp",
    "MaxSummaryTokens": 1024,
    "MinCellLength": 0
  }
}
```

**Response**: `200 OK` — `SummarizeResponse`

```json
{
  "Success": true,
  "StatusCode": 200,
  "CompletionEndpointId": "cep_...",
  "Model": "gpt-4.1-mini",
  "Summary": "Short summary of the input text.",
  "Summaries": [ "Short summary of the input text." ],
  "ResponseTimeMs": 54.0,
  "CompletionCalls": [ ]
}
```

`Summary` is empty when the input did not meet `SummarizationConfiguration.MinCellLength`.

**Errors**: `400` (missing `Text` or `CompletionEndpointId`), `401`, `404` (unknown endpoint), `504`
(completion timeout).

---

## Proxy

The proxy is a **transparent passthrough** to a completion endpoint's upstream provider. You send the
provider's **native** request and Partio relays the provider's response **verbatim** — it performs no
request/response translation. This is the recommended way to use an off-the-shelf provider SDK (OpenAI,
Ollama, Ollama Cloud, Gemini, vLLM, xAI, Azure OpenAI, Anthropic, Cohere) against a Partio-managed endpoint: point the SDK's base URL at
`{baseUrl}/v1.0/proxy/{endpointId}` and the SDK appends its own native sub-path.

For each proxied request Partio: authenticates the caller (tenant bearer), resolves the endpoint (tenant
scope + `Active` + healthy), verifies the sub-path is valid for that endpoint's `ApiFormat`, **injects the
upstream credential headers** (so the caller never holds the provider credential), enforces the endpoint's
`MaxConcurrentRequests` / `MaxQueueDepth` / `MaximumTimeoutMs`, and records request history.

Two behaviors differ from the rest of the API on purpose, for native-SDK compatibility:

- **Status codes pass through.** A reached upstream's status and body are relayed exactly (real `429`,
  `500`, `504`, and native provider error JSON) — the proxy does **not** wrap failures as `200` +
  `Success=false` the way `/v1.0/completion` does.
- **Streaming passes through.** The upstream response is relayed as it arrives (chunked transfer for
  `text/event-stream` and provider streaming such as Ollama NDJSON), so `"stream": true` works end to end.

There is **no translation**: an endpoint speaks exactly one dialect and only that dialect's native
sub-paths are relayed. A request for another provider's path is rejected, never forwarded.

### {GET,POST} /v1.0/proxy/{endpointId}/{native-subpath}

Relay a native provider request to the endpoint's upstream. The request body and headers are forwarded as
sent (with hop-by-hop and the inbound `Authorization` header stripped; Partio injects the upstream credential: Bearer, `x-goog-api-key` for Gemini, `x-api-key` plus `anthropic-version` for Anthropic, and `api-key` (or Bearer) for Azure OpenAI. Ollama, vLLM, and OpenAI endpoints follow their [selectable authentication](#selectable-authentication-providersettingsauth): a bearer token, a custom header, basic auth, or a query parameter appended to the upstream URL). A caller-supplied `anthropic-version` header is kept.

**Path parameters**:
- `endpointId` — target completion endpoint ID (`cep_...`).
- `native-subpath` — the provider's own path (see the allow-list below).

**Allowed sub-paths by `ApiFormat`** (any other path returns `404`):
- **OpenAI / vLLM**: `v1/chat/completions`, `v1/completions`, `v1/embeddings`, `v1/models`, `v1/models/{model}`
- **Ollama / Ollama Cloud**: `api/chat`, `api/generate`, `api/embed`, `api/embeddings`, `api/tags`, `api/show`, `api/ps`, `api/version`
- **Gemini**: `v1beta/models`, `v1beta/models/{model}:{method}`
- **xAI**: `v1/responses`, `v1/chat/completions`, `v1/embeddings`, `v1/models`, `v1/models/{model}`, `v1/language-models`, `v1/embedding-models`
- **Azure OpenAI**: `openai/deployments/{deployment}/chat/completions`, `openai/deployments/{deployment}/completions`, `openai/deployments/{deployment}/embeddings`, `openai/models`, `openai/v1/chat/completions`, `openai/v1/embeddings`, `openai/v1/responses`, `openai/v1/models`. When the query string has no `api-version`, Partio appends the endpoint's `ProviderSettings.AzureOpenAI.ApiVersion` (default `2024-10-21`).
- **Anthropic**: `v1/messages`, `v1/messages/count_tokens`, `v1/models`, `v1/models/{model}`
- **Cohere**: `v2/chat`, `v2/embed`, `v1/models`, `v1/models/{model}`
- **Bedrock / Vertex AI**: not proxied. Every request must be signed by Partio, so any proxy request returns `404` with a message pointing to `POST /v1.0/completion`.

**Example** (OpenAI-dialect endpoint):

```
POST /v1.0/proxy/cep_abc123/v1/chat/completions
Authorization: Bearer {partio-token}
Content-Type: application/json

{ "model": "gpt-4.1-mini", "messages": [ { "role": "user", "content": "Say hello in one word." } ] }
```

**Example** (Anthropic endpoint; Partio adds `x-api-key` and `anthropic-version`):

```
POST /v1.0/proxy/cep_abc123/v1/messages
Authorization: Bearer {partio-token}
Content-Type: application/json

{ "model": "claude-opus-4-8", "max_tokens": 64, "messages": [ { "role": "user", "content": "Say hello in one word." } ] }
```

**Example** (Azure OpenAI endpoint; Partio adds `api-key` and `api-version`):

```
POST /v1.0/proxy/cep_abc123/openai/deployments/gpt-4o-mini/chat/completions
Authorization: Bearer {partio-token}
Content-Type: application/json

{ "messages": [ { "role": "user", "content": "Say hello in one word." } ] }
```

**Response**: the upstream provider's response, verbatim (status code, `Content-Type`, and body). Partio adds
`X-Partio-Endpoint-Id` and `X-Partio-Model` response headers.

```json
{
  "id": "chatcmpl-...",
  "object": "chat.completion",
  "choices": [ { "index": 0, "message": { "role": "assistant", "content": "Hello" } } ]
}
```

Using an off-the-shelf SDK, set only the base URL:

```python
from openai import OpenAI
client = OpenAI(base_url="http://localhost:8400/v1.0/proxy/cep_abc123/v1", api_key="{partio-token}")
client.chat.completions.create(model="gpt-4.1-mini", messages=[{"role": "user", "content": "Hi"}])
```

**Errors** (Partio-level failures return a Partio JSON error body): `400` (missing endpoint ID or
sub-path), `401` (bad token), `404` (unknown/inactive endpoint, or a sub-path not permitted for the
endpoint's dialect), `429` (endpoint concurrency limit reached), `502` (endpoint unhealthy or upstream
unreachable), `504` (upstream timeout). A **reached** upstream's own non-2xx status is passed through
unchanged rather than remapped.

---

## Explorer

The explorer endpoints are intended for diagnostics from the dashboard or SDKs. They execute the selected configured endpoint through Partio's own backend client path and always return a structured result payload with `Success`, `StatusCode`, any `Error`, and the captured upstream call details.

### POST /v1.0/explorer/embedding
Exercise a configured embedding endpoint through Partio.

**Request Body**:
```json
{
    "EndpointId": "eep_xxxx",
    "Input": "Partio explorer embedding test payload",
    "L2Normalization": false
}
```

**Response**: `200 OK` â€” `EndpointExplorerEmbeddingResponse`
```json
{
    "Success": true,
    "StatusCode": 200,
    "Error": null,
    "EndpointId": "eep_xxxx",
    "Model": "nomic-embed-text",
    "Input": "Partio explorer embedding test payload",
    "Embedding": [0.0123, -0.0456, 0.0789],
    "Dimensions": 768,
    "ResponseTimeMs": 123,
    "RequestHistoryId": "req_xxxx",
    "EmbeddingCalls": [],
    "TokenizationProfile": {
        "TokenizerKind": "Cl100kBase",
        "TokenizerModel": "cl100k_base",
        "MaxInputTokens": 8192,
        "ReservedInputTokens": 0,
        "EffectiveInputBudget": 8192,
        "BatchLimitMode": "PerInput",
        "ProfileSource": "ProviderDefault",
        "UsedFallback": true,
        "ProviderMetadata": {
            "EndpointId": "eep_xxxx",
            "ApiFormat": "OpenAI",
            "Model": "text-embedding-3-small"
        }
    }
}
```

When the provider call fails, `Success` is `false`, `StatusCode` contains the mapped failure code, `Error` contains the error text, `EmbeddingCalls` still contains any upstream request/response data captured before the failure, and `TokenizationProfile` still shows the resolved budgeting context when resolution succeeded.

### POST /v1.0/explorer/completion
Exercise a configured inference endpoint through Partio.

**Request Body**:
```json
{
    "EndpointId": "cep_xxxx",
    "Prompt": "Explain what Partio does in one short paragraph.",
    "SystemPrompt": "Be concise.",
    "MaxTokens": 512,
    "TimeoutMs": 60000
}
```

`TimeoutMs` is the requested completion timeout for this explorer call. Partio clamps it to a positive non-zero value and never allows it to exceed the selected endpoint's `MaximumTimeoutMs`.

**Response**: `200 OK` â€” `EndpointExplorerCompletionResponse`
```json
{
    "Success": true,
    "StatusCode": 200,
    "Error": null,
    "EndpointId": "cep_xxxx",
    "Model": "gpt-4.1-mini",
    "Prompt": "Explain what Partio does in one short paragraph.",
    "SystemPrompt": "Be concise.",
    "Output": "Partio is a multi-tenant service for chunking, embedding, and optional summarization.",
    "ResponseTimeMs": 187,
    "RequestHistoryId": "req_xxxx",
    "CompletionCalls": []
}
```

If the selected endpoint has `EnableRequestHistory = true` and request history is enabled globally, the explorer response also includes the created `RequestHistoryId`.
If the upstream provider times out, the explorer route still returns `200 OK`, but the payload sets `Success = false` and `StatusCode = 504`.

---

## Model Loading (Admin)

Model loading is an explicit control-plane action for a configured endpoint. The route calls the endpoint's configured host and model.
Loading local models can allocate GPU or system memory on the configured runner. In multi-instance deployments, the API call loads or warms the upstream host reached by the Partio instance handling the request. If the endpoint URL is itself load-balanced, the load or warm request reaches whichever model runner the upstream load balancer selects.

Provider semantics:

| Provider | Completion behavior | Embedding behavior | Success outcome |
| --- | --- | --- | --- |
| Ollama | Native `/api/generate` preload with `keep_alive` by default | `/api/embed` probe with `keep_alive` | `Loaded` |
| OpenAI | Minimal chat completion warm request | Minimal embedding warm request | `Warmed` |
| vLLM | OpenAI-compatible minimal chat completion warm request | OpenAI-compatible minimal embedding warm request | `Warmed`; the model must already be served by that vLLM process |
| Gemini | Minimal `generateContent` warm request | Minimal `embedContent` warm request | `Warmed` |
| Ollama Cloud | Minimal chat warm request (hosted models are already resident) | Minimal embedding warm request | `Warmed` |
| xAI | Minimal Responses API (`/v1/responses`) warm request | Minimal embedding warm request | `Warmed` |
| Azure OpenAI | Minimal chat completion warm request on the deployment | Minimal embedding warm request | `Warmed` |
| Anthropic | Minimal Messages API warm request | — (completion only) | `Warmed` |
| Cohere | Minimal v2 chat warm request | Minimal v2 embed warm request | `Warmed` |
| Bedrock | Minimal Converse warm request (signed) | Minimal InvokeModel embedding warm request (signed) | `Warmed` |
| Vertex AI | Minimal `generateContent` warm request | Minimal embedding warm request | `Warmed` |
| TEI | — (embedding only) | Minimal `/embed` warm request | `Warmed` |
| VoyageAI | — (embedding only) | Minimal embedding warm request | `Warmed` |

### POST /v1.0/endpoints/embedding/{id}/load
Load or warm a configured embedding endpoint model.

**Request Body**:
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

### POST /v1.0/endpoints/completion/{id}/load
Load or warm a configured completion endpoint model.

For an Ollama inference endpoint configured as `gemma3:4b` on `http://localhost:11434`, use:

```bash
curl -X POST http://localhost:8400/v1.0/endpoints/completion/cep_xxxx/load \
  -H "Authorization: Bearer partioadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "Strategy": "Auto",
    "KeepAlive": "30m",
    "SampleInput": "Partio model load probe",
    "MaxTokens": 1
  }'
```

**Response**: `200 OK` on success, or a mapped non-2xx status with the same response shape.

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

Errors:

| Scenario | Status |
| --- | --- |
| Invalid request body or `KeepAlive` unload value such as `0` | `400` |
| Unauthenticated or non-admin caller | `401` |
| Endpoint not found | `404` |
| Native load required for any provider other than Ollama | `409` |
| Endpoint concurrency limit reached | `429` |
| Upstream provider failure | `502` |
| Upstream provider timeout | `504` |

Successful and failed load attempts include `X-Partio-Endpoint-Id`, `X-Model`, and `X-Partio-Model` when the endpoint is known. Request-history detail includes a `ModelLoad` metadata object plus captured upstream call details when detailed history is recorded.

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

**Response**: `201 Created` â€” `TenantMetadata`

### GET /v1.0/tenants/{id}
Read a tenant by ID.

**Response**: `200 OK` â€” `TenantMetadata`

### PUT /v1.0/tenants/{id}
Update a tenant.

**Request Body**: `TenantMetadata` (partial update)

**Response**: `200 OK` â€” `TenantMetadata`

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

**Response**: `200 OK` â€” `EnumerationResult<TenantMetadata>`

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

**Response**: `200 OK` â€” `UserMaster` (password redacted)

### GET /v1.0/users/{id}
Read a user by ID (password redacted).

**Response**: `200 OK` â€” `UserMaster`

### PUT /v1.0/users/{id}
Update a user.

**Response**: `200 OK` â€” `UserMaster`

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

**Response**: `201 Created` â€” `Credential` (includes generated `BearerToken`)

### GET /v1.0/credentials/{id}
Read a credential.

**Response**: `200 OK` â€” `Credential`

### PUT /v1.0/credentials/{id}
Update a credential.

**Response**: `200 OK` â€” `Credential`

### DELETE /v1.0/credentials/{id}
Delete a credential.

**Response**: `204 No Content`

### HEAD /v1.0/credentials/{id}
Check if a credential exists.

**Response**: `200 OK` or `404 Not Found`

### POST /v1.0/credentials/enumerate
List credentials with pagination.

---

## Providers

Embedding and completion endpoints share one provider model. `ApiFormat` selects the PolyPrompt client
Partio uses. `Endpoint`, `ApiKey`, and `Model` carry the common connection values, and the optional
`ProviderSettings` object carries anything else a provider needs. Existing payloads that omit
`ProviderSettings` behave exactly as before.

### Supported `ApiFormat` values

| `ApiFormat` | Provider | Completion | Embedding | `Endpoint` (default when omitted) | `ApiKey` | `ProviderSettings` |
| --- | --- | :-: | :-: | --- | --- | --- |
| `Ollama` | Local or self-hosted Ollama | ✓ | ✓ | Required (for example `http://localhost:11434`) | Optional; sent per `Auth` (default: Bearer) | Optional `Auth` |
| `OllamaCloud` | Ollama Cloud (native Ollama API) | ✓ | ✓ | Always stored as `https://ollama.com` | Required (Bearer) | — |
| `OpenAI` | OpenAI and OpenAI-compatible APIs | ✓ | ✓ | `https://api.openai.com` | Usually required; sent per `Auth` (default: Bearer) | Optional `Auth` |
| `vLLM` | vLLM (OpenAI-compatible) | ✓ | ✓ | Required | Deployment-dependent; sent per `Auth` (default: Bearer) | Optional `Auth` |
| `Gemini` | Google Gemini API | ✓ | ✓ | `https://generativelanguage.googleapis.com` | Required in practice (`x-goog-api-key`) | — |
| `xAI` | xAI Grok; completions use the native Responses API | ✓ | ✓ | `https://api.x.ai` (trailing `/v1` dropped) | Required (Bearer) | — |
| `AzureOpenAI` | Azure OpenAI | ✓ | ✓ | **Required**: the resource URL, for example `https://my-resource.openai.azure.com` (trailing `/openai` dropped) | Required: `api-key`, or an Entra ID token when `AuthMode` is `BearerToken` | Optional `AzureOpenAI` |
| `Anthropic` | Anthropic Claude Messages API | ✓ | — | `https://api.anthropic.com` (trailing `/v1` dropped) | Required (`x-api-key` + `anthropic-version`) | Optional `Anthropic` |
| `Cohere` | Cohere v2 API | ✓ | ✓ | `https://api.cohere.com` (trailing `/v1` or `/v2` dropped) | Required (Bearer) | Optional `Cohere` |
| `Bedrock` | AWS Bedrock (Converse, InvokeModel) | ✓ | ✓ | `https://bedrock-runtime.{Region}.amazonaws.com` | Not used; requests are signed with AWS Signature V4 | **Required** `Bedrock` |
| `VertexAI` | Google Vertex AI | ✓ | ✓ | `https://{Region}-aiplatform.googleapis.com` | Only for `CredentialSource: AccessToken` (the token) | **Required** `VertexAI` |
| `TEI` | Hugging Face Text Embeddings Inference (self-hosted) | — | ✓ | Required (for example `http://localhost:8080`) | Optional; sent per `Auth` (default: Bearer, for servers started with `--api-key`) | Optional `Auth` |
| `VoyageAI` | VoyageAI embeddings | — | ✓ | `https://api.voyageai.com` (trailing `/v1` dropped) | Required (Bearer) | Optional `VoyageAI` |

Per-provider meaning of the common fields:

- **Azure OpenAI**: `Model` is the **deployment name**.
- **Bedrock**: `Model` is the Bedrock model ID, for example `anthropic.claude-3-5-sonnet-20240620-v1:0` or `amazon.titan-embed-text-v2:0`.
- **Vertex AI**: `Model` is the Vertex model, for example `gemini-2.5-flash` or `text-embedding-005`.
- **TEI**: the server hosts exactly one model, so `Model` is informational (it is still required).

`Endpoint` may be omitted, or sent as `""`, to use the default above. Providers without a default (`Ollama`, `vLLM`, `TEI`, `AzureOpenAI`) return `400` when it is empty.

### `ProviderSettings`

Only the section that matches `ApiFormat` is kept, plus `Auth` for `Ollama`, `vLLM`, `TEI`, and `OpenAI`.
Other sections are dropped on save, and blank strings are stored as `null`.

| Section | Field | Type | Default | Description |
| --- | --- | --- | --- | --- |
| `Auth` | `Mode` | `None` \| `Bearer` \| `Header` \| `QueryParameter` \| `Basic` | `Bearer` | How the `ApiKey` is sent. Only for `Ollama`, `vLLM`, `TEI`, and `OpenAI`; see [Selectable authentication](#selectable-authentication-providersettingsauth) |
| `Auth` | `HeaderName` | string? | `null` | Header that carries `ApiKey` when `Mode` is `Header`, for example `X-API-Key` |
| `Auth` | `QueryParameterName` | string? | `null` | Query parameter that carries `ApiKey` when `Mode` is `QueryParameter`, for example `api_key` |
| `Auth` | `Username` | string? | `null` | User name when `Mode` is `Basic`; `ApiKey` is the password |
| `AzureOpenAI` | `ApiVersion` | string? | `null` (`2024-10-21`) | Azure `api-version` query value |
| `AzureOpenAI` | `AuthMode` | `ApiKey` \| `BearerToken` | `ApiKey` | Send `ApiKey` as the `api-key` header, or as an Entra ID bearer token (static tokens expire) |
| `Anthropic` | `AnthropicVersion` | string? | `null` (`2023-06-01`) | `anthropic-version` header |
| `Anthropic` | `WorkspaceId` | string? | `null` | `anthropic-workspace-id` header (identity-linked keys) |
| `Bedrock` | `Region` | string | — (required) | AWS region, for example `us-east-1`. Selects the host and signing region |
| `Bedrock` | `CredentialSource` | `Static` \| `Environment` | `Static` | `Static` uses the keys below. `Environment` reads `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, and `AWS_SESSION_TOKEN` from the Partio server |
| `Bedrock` | `AccessKeyId` | string? | `null` | Required for `Static` |
| `Bedrock` | `SecretAccessKey` | string? | `null` | Required for `Static`. **Secret** |
| `Bedrock` | `SessionToken` | string? | `null` | Optional temporary-credential token. **Secret** |
| `VertexAI` | `ProjectId` | string | — (required) | Google Cloud project ID |
| `VertexAI` | `Region` | string | — (required) | Vertex region, for example `us-central1` |
| `VertexAI` | `CredentialSource` | `ServiceAccountJson` \| `ApplicationDefault` \| `AccessToken` | `ServiceAccountJson` | Service-account key JSON stored on the endpoint, the server's Application Default Credentials, or a static token in `ApiKey` (tokens expire) |
| `VertexAI` | `ServiceAccountJson` | string? | `null` | Full service-account key JSON (needs `client_email` and `private_key`). Required for `ServiceAccountJson`. **Secret** |
| `Cohere` | `InputType` | string? | `null` (`search_document`) | Embedding `input_type` |
| `VoyageAI` | `InputType` | string? | `null` | Embedding `input_type` (`document` or `query`) |
| `VoyageAI` | `OutputDimension` | int? | `null` | Matryoshka output dimension (256, 512, 1024, 2048). Must be `>= 1` |

Service-account and ADC credentials mint OAuth tokens that Partio caches and refreshes ahead of expiry.

Examples (only the provider fields are shown; every other endpoint field works as documented below):

```json
{ "ApiFormat": "AzureOpenAI", "Endpoint": "https://my-resource.openai.azure.com", "Model": "gpt-4o-mini",
  "ApiKey": "azure-key", "ProviderSettings": { "AzureOpenAI": { "ApiVersion": "2024-10-21", "AuthMode": "ApiKey" } } }

{ "ApiFormat": "Anthropic", "Model": "claude-opus-4-8", "ApiKey": "sk-ant-...",
  "ProviderSettings": { "Anthropic": { "AnthropicVersion": "2023-06-01" } } }

{ "ApiFormat": "Bedrock", "Model": "anthropic.claude-3-5-sonnet-20240620-v1:0",
  "ProviderSettings": { "Bedrock": { "Region": "us-east-1", "CredentialSource": "Static",
    "AccessKeyId": "AKIA...", "SecretAccessKey": "...", "SessionToken": null } } }

{ "ApiFormat": "Bedrock", "Model": "amazon.titan-embed-text-v2:0",
  "ProviderSettings": { "Bedrock": { "Region": "us-west-2", "CredentialSource": "Environment" } } }

{ "ApiFormat": "VertexAI", "Model": "gemini-2.5-flash",
  "ProviderSettings": { "VertexAI": { "ProjectId": "my-project", "Region": "us-central1",
    "CredentialSource": "ServiceAccountJson", "ServiceAccountJson": "{\"type\":\"service_account\",\"client_email\":\"...\",\"private_key\":\"...\"}" } } }

{ "ApiFormat": "VertexAI", "Model": "text-embedding-005",
  "ProviderSettings": { "VertexAI": { "ProjectId": "my-project", "Region": "us-central1", "CredentialSource": "ApplicationDefault" } } }

{ "ApiFormat": "VertexAI", "Model": "gemini-2.5-flash", "ApiKey": "ya29....",
  "ProviderSettings": { "VertexAI": { "ProjectId": "my-project", "Region": "us-central1", "CredentialSource": "AccessToken" } } }

{ "ApiFormat": "Cohere", "Model": "embed-v4.0", "ApiKey": "cohere-key",
  "ProviderSettings": { "Cohere": { "InputType": "search_document" } } }

{ "ApiFormat": "VoyageAI", "Model": "voyage-3.5", "ApiKey": "pa-...",
  "ProviderSettings": { "VoyageAI": { "InputType": "document", "OutputDimension": 1024 } } }

{ "ApiFormat": "TEI", "Endpoint": "http://localhost:8080", "Model": "BAAI/bge-small-en-v1.5", "ApiKey": null }

{ "ApiFormat": "Ollama", "Endpoint": "https://ollama.internal.example.com", "Model": "gemma3:4b", "ApiKey": "gateway-key",
  "ProviderSettings": { "Auth": { "Mode": "Header", "HeaderName": "X-Gateway-Key" } } }

{ "ApiFormat": "vLLM", "Endpoint": "https://vllm.example.com", "Model": "Qwen/Qwen2.5-7B-Instruct", "ApiKey": "query-key",
  "ProviderSettings": { "Auth": { "Mode": "QueryParameter", "QueryParameterName": "api_key" } } }
```

#### Selectable authentication (`ProviderSettings.Auth`)

Most providers have one documented authentication scheme, and Partio always uses it:

| Provider | How the credential is sent |
| --- | --- |
| `OllamaCloud`, `xAI`, `Cohere`, `VoyageAI` | `Authorization: Bearer {ApiKey}` |
| `Anthropic` | `x-api-key: {ApiKey}` plus `anthropic-version` (and `anthropic-workspace-id` when set) |
| `Gemini` | `x-goog-api-key: {ApiKey}` |
| `AzureOpenAI` | `api-key: {ApiKey}`, or `Authorization: Bearer {ApiKey}` when `ProviderSettings.AzureOpenAI.AuthMode` is `BearerToken` |
| `Bedrock` | AWS Signature V4 from `ProviderSettings.Bedrock` credentials; `ApiKey` is not used |
| `VertexAI` | Google OAuth bearer token from `ProviderSettings.VertexAI` (service account, ADC, or the `ApiKey` as a static access token) |

These providers ignore `ProviderSettings.Auth`, and it is dropped on save.

`Ollama`, `vLLM`, `TEI`, and `OpenAI` (including OpenAI-compatible gateways) are often deployed behind proxies
or gateways, so their authentication is selectable. The secret is always `ApiKey`, which is masked on read
and can be revealed like any other secret.

| `Mode` | What Partio sends | Required fields |
| --- | --- | --- |
| `None` | No credential. `ApiKey` is cleared on save | — |
| `Bearer` | `Authorization: Bearer {ApiKey}` | `ApiKey` |
| `Header` | `{HeaderName}: {ApiKey}` | `ApiKey`, `HeaderName` |
| `QueryParameter` | `?{QueryParameterName}={ApiKey}` appended to every upstream URL | `ApiKey`, `QueryParameterName` |
| `Basic` | `Authorization: Basic base64({Username}:{ApiKey})` | `ApiKey`, `Username` |

- **Without an `Auth` section** behavior is unchanged: a configured `ApiKey` is sent as a bearer token, and no
  key means no credential.
- **Everywhere:** the selected mode is applied to provider calls (completion, embedding, summarization,
  validation, model loading), to health checks (when `HealthCheckUseAuth` is on, which defaults to on whenever
  a credential is selected), and to the transparent proxy.
- **Request history:** custom-header and query-parameter credentials are attached by Partio's transport after
  PolyPrompt records the call, so they never appear in recorded call details or the recorded upstream URL.
  Bearer tokens are attached by PolyPrompt and appear in recorded request headers, as before.

### Validation

Create and update return `400 Bad Request` when:

- the provider does not support the endpoint kind: `TEI` and `VoyageAI` completion endpoints, or `Anthropic` embedding endpoints;
- `ApiKey` is missing for `OllamaCloud`, `xAI`, `AzureOpenAI`, `Anthropic`, `Cohere`, or `VoyageAI`;
- an `AzureOpenAI` endpoint has no `Endpoint`;
- a `Bedrock` endpoint lacks `ProviderSettings.Bedrock` or `Region`, or, for `Static` credentials, `AccessKeyId` or `SecretAccessKey`;
- a `VertexAI` endpoint lacks `ProviderSettings.VertexAI`, `ProjectId`, or `Region`; for `ServiceAccountJson`, the JSON is missing, invalid, or lacks `client_email` and `private_key`; for `AccessToken`, `ApiKey` is missing;
- `Endpoint` is empty for a provider without a default (`Ollama`, `vLLM`, `TEI`, `AzureOpenAI`);
- `ProviderSettings.Auth` (on `Ollama`, `vLLM`, `TEI`, or `OpenAI`) selects any mode other than `None` without an `ApiKey`; `Header` lacks `HeaderName`, uses a name that is not a valid HTTP token, or uses a header the HTTP client manages (`Host`, `Content-Length`, `Content-Type`, `Connection`, `Transfer-Encoding`, `Keep-Alive`, `Proxy-Connection`, `TE`, `Trailer`, `Upgrade`, `Expect`, `Accept`, `Accept-Encoding`, `User-Agent`); `QueryParameter` lacks `QueryParameterName` or it contains `&`, `=`, `?`, `#`, or a space; `Basic` lacks `Username` or it contains `:`.

The error message names the missing field, for example `Bedrock endpoints require ProviderSettings.Bedrock.Region.`

### Health check defaults

Defaults are applied on create and update when a field is unset or zero.

| `ApiFormat` | Health check | Interval / timeout | Auth |
| --- | --- | --- | --- |
| `Ollama` | `GET {Endpoint}/api/tags` | 5s / 2s | Off, unless `Auth` selects a credential |
| `OllamaCloud` | `GET https://ollama.com/api/tags` | 15s / 5s | On when `ApiKey` is set |
| `OpenAI`, `vLLM`, `xAI`, `Anthropic`, `Cohere` | `GET {Endpoint}/v1/models` | 15s / 5s | On when `ApiKey` is set |
| `Gemini` | `GET {Endpoint}/v1beta/models` | 15s / 5s | On when `ApiKey` is set |
| `AzureOpenAI` | `GET {Endpoint}/openai/models?api-version={ApiVersion}` | 15s / 5s | On when `ApiKey` is set |
| `TEI` | `GET {Endpoint}/health` | 15s / 5s | On when `ApiKey` is set |
| `Bedrock`, `VertexAI`, `VoyageAI` | PolyPrompt connectivity probe (no `HealthCheckUrl`) | 60s / 10s | Endpoint credentials |

Health-check auth uses each provider's real header. Gemini uses `x-goog-api-key`. Anthropic uses `x-api-key`
with `anthropic-version` (and `anthropic-workspace-id` when set). Azure OpenAI uses `api-key`, or Bearer when
`AuthMode` is `BearerToken`. `Ollama`, `vLLM`, `TEI`, and `OpenAI` follow `ProviderSettings.Auth` (a
query-parameter credential is added to the request URL only, never to the stored `HealthCheckUrl` or logs).
Every other provider uses `Authorization: Bearer`. An `Ollama` endpoint with an explicitly selected
credential authenticates its health checks by default.

Bedrock and Vertex AI requests must be signed per request, and VoyageAI has no model-listing API, so their
health checks run PolyPrompt's connectivity probe with the endpoint's own credentials and model:

| Provider | Probe |
| --- | --- |
| Bedrock | `GET /foundation-models` on the control plane (needs `bedrock:ListFoundationModels`) |
| Vertex AI (completion) | `countTokens` on the model |
| Vertex AI (embedding) | a one-word embedding |
| VoyageAI | a one-word embedding |

The embedding probes are billable, which is why these defaults are a longer 60s interval and 10s timeout.

### Secrets: masking and reveal

`ApiKey`, `ProviderSettings.Bedrock.SecretAccessKey`, `ProviderSettings.Bedrock.SessionToken`, and
`ProviderSettings.VertexAI.ServiceAccountJson` are **masked** in every endpoint response: read,
enumerate, create, and update. A secret of 12 or more characters is shown as `****` plus its last four
characters (for example `****9f2c`). Shorter secrets, and service-account JSON, are shown as `****`.

On update:

- sending back the masked value unchanged keeps the stored secret, so read-modify-write works;
- sending a new value replaces the secret;
- sending `null` clears it.

To read the stored secrets, use the reveal APIs. They are admin only, and each call is logged with the caller.

- `GET /v1.0/endpoints/embedding/{id}/reveal`
- `GET /v1.0/endpoints/completion/{id}/reveal`

**Response**: `200 OK`. The endpoint is returned with every secret unmasked, or `404` if it is not found.

The MCP server deliberately exposes no reveal tool. MCP clients always receive masked secrets.

---
## Embedding Endpoints (Admin)

### PUT /v1.0/endpoints/embedding
Create an embedding endpoint.

**Request Body**:
```json
{
    "TenantId": "ten_...",
    "Name": "My Embedding Endpoint",
    "Model": "nomic-embed-text",
    "Endpoint": "http://localhost:11434",
    "ApiFormat": "Ollama",
    "ApiKey": null,
    "Active": true,
    "EnableRequestHistory": true,
    "MaximumTimeoutMs": 60000,
    "MaxConcurrentRequests": 2,
    "MaxQueueDepth": 0,
    "HealthCheckEnabled": false,
    "HealthCheckUrl": null,
    "HealthCheckMethod": "GET",
    "HealthCheckIntervalMs": 5000,
    "HealthCheckTimeoutMs": 2000,
    "HealthCheckExpectedStatusCode": 200,
    "HealthyThreshold": 3,
    "UnhealthyThreshold": 3,
    "HealthCheckUseAuth": false,
    "Labels": ["production", "embedding"],
    "Tags": {
        "owner": "search",
        "env": "prod"
    },
    "Tokenization": {
        "TokenizerKind": "BertWordPiece",
        "TokenizerModel": "bert-base-uncased",
        "MaxInputTokens": 512,
        "ReservedInputTokens": 0,
        "BatchLimitMode": "PerInput",
        "AutoDetect": true
    }
}
```

#### Endpoint Metadata Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Labels` | `List<string>` | `[]` | Free-form labels stored with the embedding endpoint |
| `Tags` | `Dictionary<string, string>` | `{}` | Free-form key/value metadata stored with the embedding endpoint |

#### Health Check Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaximumTimeoutMs` | int | `60000` | Maximum upstream provider timeout in milliseconds for embedding calls. Stored in milliseconds and clamped to a positive non-zero integer. |
| `MaxConcurrentRequests` | int | `2` | Maximum concurrent upstream provider requests allowed for this embedding endpoint. Clamped server-side to `>= 1`. |
| `MaxQueueDepth` | int | `0` | Requests allowed to wait for a concurrency slot once `MaxConcurrentRequests` is reached. `0` rejects immediately with 429. Clamped server-side to `>= 0`. Waiting is bounded by `MaximumTimeoutMs`. |
| `HealthCheckEnabled` | bool | `false` | Enable background health checking for this endpoint |
| `HealthCheckUrl` | string? | `null` | Custom URL to check (defaults to the endpoint URL if null) |
| `HealthCheckMethod` | string | `"GET"` | HTTP method for health checks (`GET` or `HEAD`) |
| `HealthCheckIntervalMs` | int | `5000` | Milliseconds between health checks |
| `HealthCheckTimeoutMs` | int | `2000` | Timeout per health check request in milliseconds |
| `HealthCheckExpectedStatusCode` | int | `200` | Expected HTTP status code for a healthy response |
| `HealthyThreshold` | int | `3` | Consecutive successes required to transition to healthy |
| `UnhealthyThreshold` | int | `3` | Consecutive failures required to transition to unhealthy |
| `HealthCheckUseAuth` | bool | `false` | Include the endpoint's credential in health checks, using the provider's header (see [Health check defaults](#health-check-defaults)) |
| `ProviderSettings` | object? | `null` | Provider-specific settings (see [`ProviderSettings`](#providersettings)); required for `Bedrock` and `VertexAI` |

#### Tokenization Properties

`Tokenization` is optional. If omitted or incomplete, Partio continues resolution through provider probe, provider defaults, then the server global fallback.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `TokenizerKind` | string? | `null` | Tokenizer family used for chunk counting and token-range slicing |
| `TokenizerModel` | string? | `null` | Tokenizer model or vocabulary identifier |
| `MaxInputTokens` | int? | `null` | Upstream maximum accepted input tokens |
| `ReservedInputTokens` | int? | `null` | Tokens reserved before chunking begins |
| `BatchLimitMode` | string? | `null` | `PerInput`, `WholeRequest`, or `Unknown` |
| `AutoDetect` | bool | `true` | Whether Partio should continue probing or falling back for missing fields |

When `HealthCheckEnabled` is `true` and the endpoint is active, the server runs a background loop that periodically checks the endpoint. If the endpoint becomes unhealthy, process requests to it return `502 Bad Gateway`.
If the embedding provider call itself exceeds `MaximumTimeoutMs`, process routes return `504 Gateway Timeout`.
If the endpoint already has `MaxConcurrentRequests` upstream calls in flight, Partio queues up to `MaxQueueDepth` further requests to wait for a slot. A queued request that waits past `MaximumTimeoutMs` returns `504 Gateway Timeout`, and when the queue is full Partio returns `429 Too Many Requests`. With the default `MaxQueueDepth` of `0`, requests over the concurrency limit are rejected immediately with `429 Too Many Requests`.

See [Providers](#providers) for the supported `ApiFormat` values, `ProviderSettings`, validation rules, health check defaults, and secret masking.

**Response**: `201 Created` â€” `EmbeddingEndpoint`

### GET /v1.0/endpoints/embedding/{id}
Read an embedding endpoint.

**Response**: `200 OK` â€” `EmbeddingEndpoint` with secrets masked

### GET /v1.0/endpoints/embedding/{id}/reveal
Read an embedding endpoint with `ApiKey` and secret `ProviderSettings` fields unmasked. Admin only; each call is logged. See [Secrets: masking and reveal](#secrets-masking-and-reveal).

**Response**: `200 OK` â€” `EmbeddingEndpoint` (unmasked)

### PUT /v1.0/endpoints/embedding/{id}
Update an embedding endpoint. Secrets sent back in their masked form keep the stored value; `null` clears them.

**Response**: `200 OK` â€” `EmbeddingEndpoint`

### DELETE /v1.0/endpoints/embedding/{id}
Delete an embedding endpoint.

**Response**: `204 No Content`

### HEAD /v1.0/endpoints/embedding/{id}
Check if an embedding endpoint exists.

**Response**: `200 OK` or `404 Not Found`

### POST /v1.0/endpoints/embedding/enumerate
List embedding endpoints with pagination.

---

## Embedding Endpoint Health (Admin)

### GET /v1.0/endpoints/embedding/{id}/health
Get the health status for a specific monitored embedding endpoint.

**Path Parameters**:
- `id` â€” Embedding endpoint ID

**Response**: `200 OK` â€” `EndpointHealthStatus`
```json
{
    "EndpointId": "eep_xxxx",
    "EndpointName": "nomic-embed-text",
    "TenantId": "ten_xxxx",
    "IsHealthy": true,
    "FirstCheckUtc": "2026-02-07T12:00:00Z",
    "LastCheckUtc": "2026-02-07T12:01:00Z",
    "LastHealthyUtc": "2026-02-07T12:00:30Z",
    "LastUnhealthyUtc": null,
    "LastStateChangeUtc": "2026-02-07T12:00:30Z",
    "TotalUptimeMs": 60000,
    "TotalDowntimeMs": 30000,
    "UptimePercentage": 66.67,
    "ConsecutiveSuccesses": 3,
    "ConsecutiveFailures": 0,
    "LastError": null,
    "History": [
        { "TimestampUtc": "2026-02-07T12:00:10Z", "Success": false },
        { "TimestampUtc": "2026-02-07T12:00:20Z", "Success": true },
        { "TimestampUtc": "2026-02-07T12:00:30Z", "Success": true }
    ]
}
```

**Errors**:
- `404 Not Found` â€” No health state exists (health check not enabled or endpoint not found)

### GET /v1.0/endpoints/embedding/health
Get health status for all monitored embedding endpoints. Non-admin callers see only their tenant's endpoints.

**Response**: `200 OK` â€” `List<EndpointHealthStatus>`

---

## Completion Endpoints (Admin)

### PUT /v1.0/endpoints/completion
Create a completion endpoint.

**Request Body**:
```json
{
    "TenantId": "ten_...",
    "Name": "My Inference Endpoint",
    "Model": "llama3",
    "Endpoint": "http://localhost:11434",
    "ApiFormat": "Ollama",
    "ApiKey": null,
    "Active": true,
    "EnableRequestHistory": true,
    "MaximumTimeoutMs": 60000,
    "MaxConcurrentRequests": 2,
    "MaxQueueDepth": 0,
    "HealthCheckEnabled": false,
    "Labels": ["production", "inference"],
    "Tags": {
        "owner": "search",
        "env": "prod"
    }
}
```

Completion endpoints accept every `ApiFormat` except `TEI` and `VoyageAI` (embedding only). See [Providers](#providers).

**Response**: `201 Created` â€” `CompletionEndpoint`

#### Completion Endpoint Metadata Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Labels` | `List<string>` | `[]` | Free-form labels stored with the completion/inference endpoint |
| `Tags` | `Dictionary<string, string>` | `{}` | Free-form key/value metadata stored with the completion/inference endpoint |

#### Completion Endpoint Timeout and Health Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaximumTimeoutMs` | int | `60000` | Maximum upstream provider timeout in milliseconds for inference calls. Stored in milliseconds and clamped to a positive non-zero integer. |
| `MaxConcurrentRequests` | int | `2` | Maximum concurrent upstream provider requests allowed for this inference endpoint. Clamped server-side to `>= 1`. |
| `MaxQueueDepth` | int | `0` | Requests allowed to wait for a concurrency slot once `MaxConcurrentRequests` is reached. `0` rejects immediately with 429. Clamped server-side to `>= 0`. Waiting is bounded by `MaximumTimeoutMs`. |
| `HealthCheckEnabled` | bool | `true` | Enable background health checking for this endpoint |
| `HealthCheckUrl` | string? | `null` | Custom URL to check (defaults from `ApiFormat` when omitted) |
| `HealthCheckMethod` | string | `"GET"` | HTTP method for health checks (`GET` or `HEAD`) |
| `HealthCheckIntervalMs` | int | `5000`, `15000`, or `60000` | Milliseconds between health checks, depending on `ApiFormat` |
| `HealthCheckTimeoutMs` | int | `2000`, `5000`, or `10000` | Timeout per health check request in milliseconds, depending on `ApiFormat` |
| `HealthCheckExpectedStatusCode` | int | `200` | Expected HTTP status code for a healthy response |
| `HealthyThreshold` | int | `2` | Consecutive successes required to transition to healthy |
| `UnhealthyThreshold` | int | `2` | Consecutive failures required to transition to unhealthy |
| `HealthCheckUseAuth` | bool | `false` or `true` | Include the endpoint API key when the health probe requires authentication |
| `ProviderSettings` | object? | `null` | Provider-specific settings (see [`ProviderSettings`](#providersettings)); required for `Bedrock` and `VertexAI` |

Completion explorer and summarization calls never exceed `MaximumTimeoutMs`, even if the caller requests a larger `TimeoutMs`. When the inference provider exceeds this ceiling, process routes return `504 Gateway Timeout`. If the endpoint is already servicing `MaxConcurrentRequests` upstream calls, Partio queues up to `MaxQueueDepth` further requests to wait for a slot. A queued request that waits past `MaximumTimeoutMs` returns `504 Gateway Timeout`, and when the queue is full Partio returns `429 Too Many Requests`. With the default `MaxQueueDepth` of `0`, requests over the concurrency limit are rejected immediately with `429 Too Many Requests`.

### GET /v1.0/endpoints/completion/{id}
Read a completion endpoint.

**Response**: `200 OK` â€” `CompletionEndpoint` with secrets masked

### GET /v1.0/endpoints/completion/{id}/reveal
Read a completion endpoint with `ApiKey` and secret `ProviderSettings` fields unmasked. Admin only; each call is logged. See [Secrets: masking and reveal](#secrets-masking-and-reveal).

**Response**: `200 OK` â€” `CompletionEndpoint` (unmasked)

### PUT /v1.0/endpoints/completion/{id}
Update a completion endpoint. Secrets sent back in their masked form keep the stored value; `null` clears them.

**Response**: `200 OK` â€” `CompletionEndpoint`

### DELETE /v1.0/endpoints/completion/{id}
Delete a completion endpoint.

**Response**: `204 No Content`

### HEAD /v1.0/endpoints/completion/{id}
Check if a completion endpoint exists.

**Response**: `200 OK` or `404 Not Found`

### POST /v1.0/endpoints/completion/enumerate
List completion endpoints with pagination.

---

## Completion Endpoint Health (Admin)

### GET /v1.0/endpoints/completion/{id}/health
Get the health status for a specific completion endpoint.

**Response**: `200 OK` â€” `EndpointHealthStatus`

### GET /v1.0/endpoints/completion/health
Get health status for all monitored completion endpoints.

**Response**: `200 OK` â€” `List<EndpointHealthStatus>`

---

## Request History (Admin)

### GET /v1.0/requests/{id}
Read a request history entry.

**Response**: `200 OK` â€” `RequestHistoryEntry`

### GET /v1.0/requests/{id}/detail
Read request/response body detail from filesystem.

**Response**: `200 OK` â€” JSON object with the following fields:

| Field | Type | Description |
|-------|------|-------------|
| `RequestHeaders` | object? | Outer request headers (key-value pairs) |
| `RequestBody` | string? | Outer request body (may be truncated) |
| `ResponseHeaders` | object? | Outer response headers (key-value pairs) |
| `ResponseBody` | string? | Outer response body (may be truncated) |
| `EmbeddingCalls` | array? | Upstream embedding HTTP call details (present only for process requests) |
| `CompletionCalls` | array? | Upstream completion/inference HTTP call details (present only for requests that use summarization or other completion features) |
| `TokenizationProfile` | object? | Resolved embedding tokenization profile for process and explorer embedding requests when available |

Each item in the `EmbeddingCalls` or `CompletionCalls` array:

| Field | Type | Description |
|-------|------|-------------|
| `Url` | string? | Full URL called on the upstream embedding endpoint |
| `Method` | string? | HTTP method (e.g. `POST`) |
| `RequestHeaders` | object? | Headers sent to the upstream endpoint |
| `RequestBody` | string? | Body sent to the upstream endpoint (may be truncated) |
| `StatusCode` | int? | HTTP status code returned by the upstream endpoint |
| `ResponseHeaders` | object? | Response headers from the upstream endpoint |
| `ResponseBody` | string? | Response body from the upstream endpoint (may be truncated) |
| `ResponseTimeMs` | long? | Round-trip time for this call in milliseconds |
| `Success` | bool | Whether the call returned a success status code |
| `Error` | string? | Error message if the call failed |
| `TimestampUtc` | string | ISO 8601 timestamp when the call was initiated |

### DELETE /v1.0/requests/{id}
Delete a request history entry.

**Response**: `204 No Content`

### POST /v1.0/requests/enumerate
List request history with pagination.

### POST /v1.0/requests/statistics
Get aggregated request statistics grouped by time bucket, broken out by success/failure.

**Request Body**: `RequestStatisticsRequest`
```json
{
    "RequestType": "Embedding",
    "Timeframe": "Day",
    "EndpointFilter": null
}
```

| Field | Type | Description |
|-------|------|-------------|
| `RequestType` | string? | `"Embedding"`, `"Inference"`, or null for all requests. Embedding matches URLs containing `/process` or `/embedding`. Inference matches URLs containing `/completion`. |
| `Timeframe` | string? | `"Hour"` (1-minute buckets, ~60 samples), `"Day"` (15-minute buckets, ~96 samples), `"Week"` (1-hour buckets, ~168 samples), or `"Month"` (4-hour buckets, ~180 samples). Defaults to `"Day"`. |
| `EndpointFilter` | string? | Optional URL substring filter to narrow results to a specific endpoint. |

**Response**: `200 OK` â€” `RequestStatisticsResponse`
```json
{
    "Buckets": [
        {
            "TimeBucket": "2026-03-20T14",
            "SuccessCount": 42,
            "FailureCount": 3
        },
        {
            "TimeBucket": "2026-03-20T15",
            "SuccessCount": 38,
            "FailureCount": 1
        }
    ],
    "TotalSuccess": 80,
    "TotalFailure": 4
}
```

| Field | Type | Description |
|-------|------|-------------|
| `Buckets` | array | Time-bucketed request counts |
| `Buckets[].TimeBucket` | string | ISO 8601 timestamp for the bucket start, always `"yyyy-MM-ddTHH:mm"` format (e.g. `"2026-03-20T14:30"`) |
| `Buckets[].SuccessCount` | long | Requests with HTTP status 100-399 |
| `Buckets[].FailureCount` | long | Requests with HTTP status 400+ or null status |
| `TotalSuccess` | long | Total successful requests in the time range |
| `TotalFailure` | long | Total failed requests in the time range |

---

## Summarization

Summarization is an optional step in the processing pipeline that runs **before** chunking and embedding. When a `SummarizationConfiguration` is provided inline on the `SemanticCellRequest`, the server uses a completion endpoint to generate summaries of the input content. The resulting summary cells have `Type = "Summary"`.

### Strategies

- **TopDown** â€” Summarizes the entire content in a single pass, producing one summary from the full input.
- **BottomUp** â€” Splits the content into smaller pieces first, summarizes each piece individually, then optionally combines the summaries.

### Prompt Template

The summarization prompt is configurable via a template string. The following tokens are available for substitution:

| Token | Description |
|-------|-------------|
| `{tokens}` | The target token count for the summary |
| `{content}` | The content to be summarized |
| `{context}` | Additional context provided by the caller |

### Retry Semantics

Summarization supports two levels of retry control:

- **`MaxRetriesPerSummary`** â€” Maximum number of retries for each individual summary cell. If a single cell fails repeatedly, it is skipped after this many attempts.
- **`MaxRetries`** â€” Global maximum number of total retries across the entire summarization operation. Once this limit is reached, the operation fails regardless of per-cell limits.

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
| 502 | Bad Gateway (endpoint is unhealthy) |
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

- `CreatedAscending` â€” oldest first
- `CreatedDescending` â€” newest first (default)
- `NameAscending` â€” alphabetical A-Z
- `NameDescending` â€” alphabetical Z-A

### Filtering

- `NameFilter` â€” partial match on name field
- `LabelFilter` â€” exact match on labels
- `TagKeyFilter` / `TagValueFilter` â€” filter by tag key/value
- `ActiveFilter` â€” filter by active status (true/false)
