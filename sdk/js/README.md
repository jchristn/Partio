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
- Embedding & Completion Endpoint Health (`getEndpointHealth`, `getAllEndpointHealth`, `getCompletionEndpointHealth`, `getAllCompletionEndpointHealth`)
- Semantic cell processing (`process`, `processBatch`)
- Request history (`getRequestHistory`, `getRequestHistoryDetail`, `deleteRequestHistory`, `enumerateRequestHistory`)

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

const client = new PartioClient('http://localhost:8000', 'your-access-key');

const result = await client.process({
  Type: 'Text',
  Text: 'Hello, world!',
  EmbeddingConfiguration: {
    EmbeddingEndpointId: 'eep_your_endpoint_id'
  }
});

console.log(`Chunks: ${result.Chunks.length}`);
```

## Running the Test Harness

The test harness runs a comprehensive suite of CRUD and processing tests against a live Partio server.

Using the launcher scripts:

```bash
# Windows
go.bat http://localhost:8000 partioadmin

# Linux / macOS
./go.sh http://localhost:8000 partioadmin
```

Or directly with Node.js:

```bash
node test-harness.js http://localhost:8000 partioadmin
```

### Test Output

The harness prints one line per test with pass/fail status and elapsed time, followed by an overall summary:

```
Partio JavaScript SDK Test Harness
Endpoint: http://localhost:8000
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
