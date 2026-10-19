# Partio JavaScript SDK

A JavaScript/Node.js client library for the Partio REST API. Uses native `fetch` with zero external dependencies.

## Overview

The Partio JavaScript SDK provides a `PartioClient` class for interacting with a Partio server. It covers the full API surface:

- Health checks and identity (`health`, `whoami`)
- Tenant CRUD (`createTenant`, `getTenant`, `updateTenant`, `deleteTenant`, `tenantExists`, `enumerateTenants`)
- User CRUD (`createUser`, `getUser`, `updateUser`, `deleteUser`, `userExists`, `enumerateUsers`)
- Credential CRUD (`createCredential`, `getCredential`, `updateCredential`, `deleteCredential`, `credentialExists`, `enumerateCredentials`)
- Embedding Endpoint CRUD (`createEndpoint`, `getEndpoint`, `revealEndpoint`, `updateEndpoint`, `deleteEndpoint`, `endpointExists`, `enumerateEndpoints`)
- Completion Endpoint CRUD (`createCompletionEndpoint`, `getCompletionEndpoint`, `revealCompletionEndpoint`, `updateCompletionEndpoint`, `deleteCompletionEndpoint`, `completionEndpointExists`, `enumerateCompletionEndpoints`)
- Model loading and warming (`loadEndpoint`, `loadCompletionEndpoint`)
- Embedding & Completion Endpoint Health (`getEndpointHealth`, `getAllEndpointHealth`, `getCompletionEndpointHealth`, `getAllCompletionEndpointHealth`)
- Semantic cell processing (`process`, `processBatch`)
- Endpoint explorer (`exploreEmbeddingEndpoint`, `exploreCompletionEndpoint`)
- Transparent completion proxy (`proxy`, `proxyPost`, `proxyGet`) — relay a native provider request through an endpoint and get the upstream response (`{statusCode, headers, body}`) verbatim; a non-2xx upstream status is returned, not thrown
- Request history (`getRequestHistory`, `getRequestHistoryDetail`, `deleteRequestHistory`, `enumerateRequestHistory`)

Embedding and completion endpoint payloads accept `ApiFormat` values `Ollama`, `OllamaCloud`, `OpenAI`, `Gemini`, `vLLM`, `xAI`, `AzureOpenAI`, `Anthropic`, `Cohere`, `Bedrock`, `VertexAI`, `TEI`, and `VoyageAI`, plus optional `Labels` and string key/value `Tags` for endpoint metadata.
Endpoint payloads are passed through unchanged, so optional embedding-endpoint `Tokenization` overrides and explorer `TokenizationProfile` diagnostics are available without extra client-side translation.
Use `MaximumTimeoutMs` and `MaxConcurrentRequests` on embedding or completion endpoints to cap upstream provider calls per endpoint. Process routes that hit the timeout cap raise `PartioError` with status code `504`; concurrency-limit rejections raise `PartioError` with status code `429`. Set `MaxQueueDepth` (default `0`) to let further requests wait for a concurrency slot once `MaxConcurrentRequests` is reached; a queued request that waits past `MaximumTimeoutMs` raises `PartioError` with status code `504`, and when the queue is full the request raises `PartioError` with status code `429`.
Model loading reports provider-specific semantics: Ollama can return `Loaded`; every other provider returns `Warmed` because those APIs do not expose a general remote model-residency operation.

`OllamaCloud` and `xAI` endpoints require `ApiKey` (the server returns 400 without one). `OllamaCloud` always targets `https://ollama.com`, whatever `Endpoint` is sent. `xAI` uses `https://api.x.ai` (a trailing `/v1` is accepted and dropped), serves completions through xAI's native Responses API, and relays `v1/responses` through the proxy.

### Providers and `ProviderSettings`

Most providers are configured by `Endpoint`, `ApiKey`, and `Model` alone. Azure OpenAI, Anthropic, Bedrock, Vertex AI, Cohere, and VoyageAI also accept an optional `ProviderSettings` object; the server keeps only the section that matches `ApiFormat`.

| ApiFormat | Kinds | Required | `ProviderSettings` section |
|---|---|---|---|
| `AzureOpenAI` | completion, embedding | `Endpoint` (resource URL), `ApiKey`, `Model` = deployment name | `AzureOpenAI`: `ApiVersion` (default `2024-10-21`), `AuthMode` (`ApiKey` or `BearerToken` for an Entra ID token in `ApiKey`) |
| `Anthropic` | completion | `ApiKey` | `Anthropic`: `AnthropicVersion` (default `2023-06-01`), `WorkspaceId` |
| `Cohere` | completion, embedding | `ApiKey` | `Cohere`: `InputType` (embeddings) |
| `Bedrock` | completion, embedding | `ProviderSettings.Bedrock.Region`; `AccessKeyId` + `SecretAccessKey` when `CredentialSource` is `Static` | `Bedrock`: `Region`, `CredentialSource` (`Static` or `Environment`), `AccessKeyId`, `SecretAccessKey`, `SessionToken` |
| `VertexAI` | completion, embedding | `ProviderSettings.VertexAI.ProjectId` and `Region`; `ServiceAccountJson` when `CredentialSource` is `ServiceAccountJson`; `ApiKey` (token) when it is `AccessToken` | `VertexAI`: `ProjectId`, `Region`, `CredentialSource` (`ServiceAccountJson`, `ApplicationDefault`, or `AccessToken`), `ServiceAccountJson` |
| `TEI` | embedding | `Endpoint` | none (`ApiKey` optional) |
| `VoyageAI` | embedding | `ApiKey` | `VoyageAI`: `InputType`, `OutputDimension` |

Hosted providers default `Endpoint` to their public API root when it is omitted, and Bedrock and Vertex AI derive it from the region. A trailing version segment (`/v1`, `/openai`) is dropped. The server returns 400 when a required value is missing, or when a provider is used for an endpoint kind it does not support (for example `TEI` or `VoyageAI` as a completion endpoint, or `Anthropic` as an embedding endpoint). Bedrock and Vertex AI endpoints cannot be used through the transparent proxy.

### Selectable authentication (Ollama, vLLM, TEI, OpenAI)

Hosted providers use one documented authentication scheme, so their credential always goes where the provider expects it. Ollama, vLLM, TEI, and OpenAI-compatible services are often deployed behind gateways, so their authentication is selectable through `ProviderSettings.Auth`. The credential itself is always `ApiKey` (masked on read, revealable like any other secret).

| `Mode` | What Partio sends | Extra field |
|---|---|---|
| `None` | Nothing (the server stores `ApiKey` as `null`) | |
| `Bearer` | `Authorization: Bearer {ApiKey}` | |
| `Header` | `{HeaderName}: {ApiKey}` | `HeaderName`, for example `X-API-Key` |
| `QueryParameter` | `?{QueryParameterName}={ApiKey}` on every request | `QueryParameterName`, for example `api_key` |
| `Basic` | `Authorization: Basic base64({Username}:{ApiKey})` | `Username` |

Without an `Auth` section, a configured `ApiKey` is sent as a bearer token. The setting applies to provider calls, health checks, and the proxy. The server returns 400 when a mode other than `None` has no `ApiKey`, when `Header` has no valid `HeaderName` (or names a header the HTTP client manages, such as `Host` or `Content-Type`), when `QueryParameter` has no name or one containing `&`, `=`, `?`, `#`, or a space, and when `Basic` has no `Username` or one containing `:`.

```javascript
import { ApiFormats, EndpointAuthModes } from './partio-sdk.js';

const gateway = await client.createCompletionEndpoint({
  TenantId: 'default',
  Model: 'gemma3:4b',
  Endpoint: 'https://ollama.internal.example.com',
  ApiFormat: ApiFormats.Ollama,
  ApiKey: 'your-gateway-key',
  ProviderSettings: { Auth: { Mode: EndpointAuthModes.Header, HeaderName: 'X-Gateway-Key' } },
});
```

### Secret masking and reveal

Every endpoint response (read, enumerate, create, and update) masks secrets: `ApiKey`, `ProviderSettings.Bedrock.SecretAccessKey`, and `ProviderSettings.Bedrock.SessionToken` are returned as `****` plus the last four characters (or `****` alone for values shorter than twelve characters), and `ProviderSettings.VertexAI.ServiceAccountJson` is returned as `****`. Sending a masked value back on update keeps the stored secret, so a read-modify-write round trip is safe; sending a new value replaces it, and `null` clears it. To read the unmasked endpoint, call the admin-only reveal API (`GET /v1.0/endpoints/embedding/{id}/reveal` and `GET /v1.0/endpoints/completion/{id}/reveal`).

```javascript
import { ApiFormats, PartioClient } from './partio-sdk.js';

const azure = await client.createCompletionEndpoint({
  TenantId: 'default',
  Model: 'gpt-4o-mini',                         // deployment name
  Endpoint: 'https://my-resource.openai.azure.com',
  ApiFormat: ApiFormats.AzureOpenAI,
  ApiKey: 'your-azure-key',
  ProviderSettings: { AzureOpenAI: { ApiVersion: '2024-10-21', AuthMode: 'ApiKey' } },
});

const vertex = await client.createEndpoint({
  TenantId: 'default',
  Model: 'text-embedding-005',
  ApiFormat: ApiFormats.VertexAI,               // Endpoint derived from the region
  ProviderSettings: { VertexAI: { ProjectId: 'my-project', Region: 'us-central1', CredentialSource: 'ServiceAccountJson', ServiceAccountJson: saJson } },
});

console.log(azure.ApiKey);                                        // ****-key (masked)
const revealed = await client.revealCompletionEndpoint(azure.Id);
const revealedVertex = await client.revealEndpoint(vertex.Id);
```

## Prerequisites

- Node.js 18 or later (for native `fetch` support)

## Project Structure

```
js/
  partio-sdk.js       # SDK module (PartioClient, PartioError)
  test-harness.js     # Test harness script
  package.json        # Package metadata
```

## Usage

```javascript
import { PartioClient } from './partio-sdk.js';

const client = new PartioClient('http://localhost:8400', 'your-access-key');

const endpoint = await client.createEndpoint({
  TenantId: 'default',
  Name: 'Pinned MiniLM',
  Model: 'all-minilm',
  Endpoint: 'http://localhost:11434',
  ApiFormat: 'Ollama',
  MaximumTimeoutMs: 60000,
  MaxConcurrentRequests: 2,
  MaxQueueDepth: 0,
  Tokenization: {
    TokenizerKind: 'BertWordPiece',
    TokenizerModel: 'bert-base-uncased',
    MaxInputTokens: 512,
    ReservedInputTokens: 0,
    BatchLimitMode: 'PerInput',
    AutoDetect: true
  }
});

const result = await client.process({
  Type: 'Text',
  Text: 'Hello, world!',
  EmbeddingConfiguration: {
    EmbeddingEndpointId: 'eep_your_endpoint_id'
  }
});

console.log(`Chunks: ${result.Chunks.length}`);

const explorer = await client.exploreCompletionEndpoint({
  EndpointId: 'cep_your_completion_endpoint_id',
  Prompt: 'Explain what Partio does in one short paragraph.',
  TimeoutMs: 60000
});

console.log(`Explorer success: ${explorer.Success}`);

const embeddingExplorer = await client.exploreEmbeddingEndpoint({
  EndpointId: endpoint.Id,
  Input: 'Tokenizer diagnostics sample'
});

console.log(embeddingExplorer.TokenizationProfile.ProfileSource);

const loadResult = await client.loadCompletionEndpoint('cep_your_completion_endpoint_id', {
  Strategy: 'Auto',
  KeepAlive: '30m'
});

console.log(`${loadResult.Outcome}: ${loadResult.Message}`);
```

Explorer requests still return `200 OK` for provider-level failures reported in the response payload, but concurrency-limit rejections return HTTP `429`.

## Running the Test Harness

The test harness runs a comprehensive suite of CRUD and processing tests against a live Partio server.

Using the launcher scripts:

```bash
# Windows
go.bat http://localhost:8400 partioadmin

# Linux / macOS
./go.sh http://localhost:8400 partioadmin
```

Or directly with Node.js:

```bash
node test-harness.js http://localhost:8400 partioadmin
```

### Test Output

The harness prints one line per test with pass/fail status and elapsed time, followed by an overall summary:

```
Partio JavaScript SDK Test Harness
Endpoint: http://localhost:8400
Admin Key: partioadmin

  PASS  Health Check (12ms)
  PASS  Who Am I (5ms)
  PASS  Create Tenant (23ms)
  ...

=== SUMMARY ===
Total: 35  Passed: 35  Failed: 0
Runtime: 1234ms
Result: PASS
================
```
