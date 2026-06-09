# Partio JavaScript SDK

A JavaScript/Node.js client library for the Partio REST API. Uses native `fetch` with zero external dependencies.

## Overview

The Partio JavaScript SDK provides a `PartioClient` class for interacting with a Partio server. It covers the full API surface:

- Health checks and identity (`health`, `whoami`)
- Tenant CRUD (`createTenant`, `getTenant`, `updateTenant`, `deleteTenant`, `tenantExists`, `enumerateTenants`)
- User CRUD (`createUser`, `getUser`, `updateUser`, `deleteUser`, `userExists`, `enumerateUsers`)
- Credential CRUD (`createCredential`, `getCredential`, `updateCredential`, `deleteCredential`, `credentialExists`, `enumerateCredentials`)
- Embedding Endpoint CRUD (`createEndpoint`, `getEndpoint`, `updateEndpoint`, `deleteEndpoint`, `endpointExists`, `enumerateEndpoints`)
- Completion Endpoint CRUD (`createCompletionEndpoint`, `getCompletionEndpoint`, `updateCompletionEndpoint`, `deleteCompletionEndpoint`, `completionEndpointExists`, `enumerateCompletionEndpoints`)
- Model loading and warming (`loadEndpoint`, `loadCompletionEndpoint`)
- Embedding & Completion Endpoint Health (`getEndpointHealth`, `getAllEndpointHealth`, `getCompletionEndpointHealth`, `getAllCompletionEndpointHealth`)
- Semantic cell processing (`process`, `processBatch`)
- Endpoint explorer (`exploreEmbeddingEndpoint`, `exploreCompletionEndpoint`)
- Request history (`getRequestHistory`, `getRequestHistoryDetail`, `deleteRequestHistory`, `enumerateRequestHistory`)

Embedding and completion endpoint payloads accept `ApiFormat` values such as `Ollama`, `OpenAI`, `Gemini`, and `vLLM`, plus optional `Labels` and string key/value `Tags` for endpoint metadata.
Endpoint payloads are passed through unchanged, so optional embedding-endpoint `Tokenization` overrides and explorer `TokenizationProfile` diagnostics are available without extra client-side translation.
Use `MaximumTimeoutMs` and `MaxConcurrentRequests` on embedding or completion endpoints to cap upstream provider calls per endpoint. Process routes that hit the timeout cap raise `PartioError` with status code `504`; concurrency-limit rejections raise `PartioError` with status code `429`.
Model loading reports provider-specific semantics: Ollama can return `Loaded`; OpenAI, Gemini, and vLLM return `Warmed` because those APIs do not expose a general remote model-residency operation.

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
