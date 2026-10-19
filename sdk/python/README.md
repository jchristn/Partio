# Partio Python SDK

A Python client library for the Partio REST API.

## Overview

The Partio Python SDK provides a `PartioClient` class for interacting with a Partio server. It covers the full API surface:

- Health checks and identity (`health`, `whoami`)
- Tenant CRUD (`create_tenant`, `get_tenant`, `update_tenant`, `delete_tenant`, `tenant_exists`, `enumerate_tenants`)
- User CRUD (`create_user`, `get_user`, `update_user`, `delete_user`, `user_exists`, `enumerate_users`)
- Credential CRUD (`create_credential`, `get_credential`, `update_credential`, `delete_credential`, `credential_exists`, `enumerate_credentials`)
- Embedding Endpoint CRUD (`create_endpoint`, `get_endpoint`, `reveal_endpoint`, `update_endpoint`, `delete_endpoint`, `endpoint_exists`, `enumerate_endpoints`)
- Completion Endpoint CRUD (`create_completion_endpoint`, `get_completion_endpoint`, `reveal_completion_endpoint`, `update_completion_endpoint`, `delete_completion_endpoint`, `completion_endpoint_exists`, `enumerate_completion_endpoints`)
- Model loading and warming (`load_endpoint`, `load_completion_endpoint`)
- Embedding & Completion Endpoint Health (`get_endpoint_health`, `get_all_endpoint_health`, `get_completion_endpoint_health`, `get_all_completion_endpoint_health`)
- Semantic cell processing (`process`, `process_batch`)
- Endpoint explorer (`explore_embedding_endpoint`, `explore_completion_endpoint`)
- Transparent completion proxy (`proxy`, `proxy_post`, `proxy_get`) — relay a native provider request through an endpoint and get the upstream response (`{status_code, headers, body}`) verbatim; a non-2xx upstream status is returned, not raised
- Request history (`get_request_history`, `get_request_history_detail`, `delete_request_history`, `enumerate_request_history`)

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

### Secret masking and reveal

Every endpoint response (read, enumerate, create, and update) masks secrets: `ApiKey`, `ProviderSettings.Bedrock.SecretAccessKey`, and `ProviderSettings.Bedrock.SessionToken` are returned as `****` plus the last four characters (or `****` alone for values shorter than twelve characters), and `ProviderSettings.VertexAI.ServiceAccountJson` is returned as `****`. Sending a masked value back on update keeps the stored secret, so a read-modify-write round trip is safe; sending a new value replaces it, and `null` clears it. To read the unmasked endpoint, call the admin-only reveal API (`GET /v1.0/endpoints/embedding/{id}/reveal` and `GET /v1.0/endpoints/completion/{id}/reveal`).

```python
azure = client.create_completion_endpoint({
    "TenantId": "default",
    "Model": "gpt-4o-mini",                     # deployment name
    "Endpoint": "https://my-resource.openai.azure.com",
    "ApiFormat": "AzureOpenAI",
    "ApiKey": "your-azure-key",
    "ProviderSettings": {"AzureOpenAI": {"ApiVersion": "2024-10-21", "AuthMode": "ApiKey"}},
})

bedrock = client.create_endpoint({
    "TenantId": "default",
    "Model": "amazon.titan-embed-text-v2:0",
    "ApiFormat": "Bedrock",                      # Endpoint derived from the region
    "ProviderSettings": {"Bedrock": {"Region": "us-east-1", "AccessKeyId": "AKIA...", "SecretAccessKey": "..."}},
})

print(azure["ApiKey"])                                         # ****-key (masked)
revealed = client.reveal_completion_endpoint(azure["Id"])
revealed_bedrock = client.reveal_endpoint(bedrock["Id"])
```

`API_FORMATS` lists the accepted `ApiFormat` values.

## Prerequisites

- Python 3.8 or later
- `requests` library (`pip install requests`)

## Project Structure

```
python/
  partio_sdk.py       # SDK module (PartioClient, PartioError)
  test_harness.py     # Test harness script
  requirements.txt    # Dependencies
```

## Installation

```bash
pip install -r requirements.txt
```

## Usage

```python
from partio_sdk import PartioClient

with PartioClient("http://localhost:8400", "your-access-key") as client:
    endpoint = client.create_endpoint({
        "TenantId": "default",
        "Name": "Pinned MiniLM",
        "Model": "all-minilm",
        "Endpoint": "http://localhost:11434",
        "ApiFormat": "Ollama",
        "MaximumTimeoutMs": 60000,
        "MaxConcurrentRequests": 2,
        "MaxQueueDepth": 0,
        "Tokenization": {
            "TokenizerKind": "BertWordPiece",
            "TokenizerModel": "bert-base-uncased",
            "MaxInputTokens": 512,
            "ReservedInputTokens": 0,
            "BatchLimitMode": "PerInput",
            "AutoDetect": True
        }
    })

    result = client.process({
        "Type": "Text",
        "Text": "Hello, world!",
        "EmbeddingConfiguration": {
            "EmbeddingEndpointId": "eep_your_endpoint_id"
        }
    })
    print(f"Chunks: {len(result['Chunks'])}")

    explorer = client.explore_completion_endpoint({
        "EndpointId": "cep_your_completion_endpoint_id",
        "Prompt": "Explain what Partio does in one short paragraph.",
        "TimeoutMs": 60000
    })
    print(f"Explorer success: {explorer['Success']}")

    embedding_explorer = client.explore_embedding_endpoint({
        "EndpointId": endpoint["Id"],
        "Input": "Tokenizer diagnostics sample"
    })
    print(embedding_explorer["TokenizationProfile"]["ProfileSource"])

    load_result = client.load_completion_endpoint("cep_your_completion_endpoint_id", {
        "Strategy": "Auto",
        "KeepAlive": "30m"
    })
    print(f"{load_result['Outcome']}: {load_result['Message']}")
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

Or directly with Python:

```bash
python test_harness.py http://localhost:8400 partioadmin
```

### Test Output

The harness prints one line per test with pass/fail status and elapsed time, followed by an overall summary:

```
Partio Python SDK Test Harness
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

## Release Versioning

The Python SDK is currently source-distributed from this repository rather than published as a separate package artifact. Its release version tracks the Partio repository release tag for this feature set, which is `0.5.2`.
