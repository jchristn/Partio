# Partio C# SDK

A .NET client library for the Partio REST API. Supports .NET 8.0 and .NET 10.0.

## Overview

The Partio C# SDK provides a strongly-typed client (`PartioClient`) for interacting with a Partio server. It covers the full API surface:

- Health checks and identity (`HealthAsync`, `WhoAmIAsync`)
- Tenant CRUD (`CreateTenantAsync`, `GetTenantAsync`, `UpdateTenantAsync`, `DeleteTenantAsync`, `TenantExistsAsync`, `EnumerateTenantsAsync`)
- User CRUD (`CreateUserAsync`, `GetUserAsync`, `UpdateUserAsync`, `DeleteUserAsync`, `UserExistsAsync`, `EnumerateUsersAsync`)
- Credential CRUD (`CreateCredentialAsync`, `GetCredentialAsync`, `UpdateCredentialAsync`, `DeleteCredentialAsync`, `CredentialExistsAsync`, `EnumerateCredentialsAsync`)
- Embedding Endpoint CRUD (`CreateEndpointAsync`, `GetEndpointAsync`, `RevealEndpointAsync`, `UpdateEndpointAsync`, `DeleteEndpointAsync`, `EndpointExistsAsync`, `EnumerateEndpointsAsync`)
- Completion Endpoint CRUD (`CreateCompletionEndpointAsync`, `GetCompletionEndpointAsync`, `RevealCompletionEndpointAsync`, `UpdateCompletionEndpointAsync`, `DeleteCompletionEndpointAsync`, `CompletionEndpointExistsAsync`, `EnumerateCompletionEndpointsAsync`)
- Model loading and warming (`LoadEndpointAsync`, `LoadCompletionEndpointAsync`)
- Embedding & Completion Endpoint Health (`GetEndpointHealthAsync`, `GetAllEndpointHealthAsync`, `GetCompletionEndpointHealthAsync`, `GetAllCompletionEndpointHealthAsync`)
- Semantic cell processing (`ProcessAsync`, `ProcessBatchAsync`)
- Endpoint explorer (`ExploreEmbeddingEndpointAsync`, `ExploreCompletionEndpointAsync`)
- Transparent completion proxy (`ProxyAsync`, `ProxyPostAsync`, `ProxyGetAsync`) — relay a native provider request through an endpoint and get the upstream response (`ProxyResponse`) verbatim; a non-2xx upstream status is returned, not thrown
- Request history (`GetRequestHistoryAsync`, `GetRequestHistoryDetailAsync`, `DeleteRequestHistoryAsync`, `EnumerateRequestHistoryAsync`)

Embedding and completion endpoint models accept `ApiFormat` values `Ollama`, `OllamaCloud`, `OpenAI`, `Gemini`, `vLLM`, `xAI`, `AzureOpenAI`, `Anthropic`, `Cohere`, `Bedrock`, `VertexAI`, `TEI`, and `VoyageAI`, plus optional `Labels` and string key/value `Tags` for endpoint metadata.
Endpoint models also expose `MaximumTimeoutMs` and `MaxConcurrentRequests`, the per-endpoint caps enforced for upstream provider calls. Process routes that hit the timeout cap return HTTP `504`, while requests rejected by the concurrency cap return HTTP `429`, both surfaced by the SDK as `PartioException`.
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

```csharp
CompletionEndpoint? azure = await client.CreateCompletionEndpointAsync(new CompletionEndpoint
{
    TenantId = "default",
    Model = "gpt-4o-mini",                        // deployment name
    Endpoint = "https://my-resource.openai.azure.com",
    ApiFormat = ApiFormats.AzureOpenAI,
    ApiKey = "your-azure-key",
    ProviderSettings = new ProviderSettings
    {
        AzureOpenAI = new AzureOpenAiProviderSettings { ApiVersion = "2024-10-21", AuthMode = AzureAuthModes.ApiKey }
    }
});

EmbeddingEndpoint? bedrock = await client.CreateEndpointAsync(new EmbeddingEndpoint
{
    TenantId = "default",
    Model = "amazon.titan-embed-text-v2:0",
    Endpoint = "https://bedrock-runtime.us-east-1.amazonaws.com",
    ApiFormat = ApiFormats.Bedrock,
    ProviderSettings = new ProviderSettings
    {
        Bedrock = new BedrockProviderSettings { Region = "us-east-1", AccessKeyId = "AKIA...", SecretAccessKey = "..." }
    }
});

Console.WriteLine(azure?.ApiKey);                                    // ****-key (masked)
CompletionEndpoint? revealed = await client.RevealCompletionEndpointAsync(azure!.Id!);
EmbeddingEndpoint? revealedBedrock = await client.RevealEndpointAsync(bedrock!.Id!);
```

`ApiFormats`, `AzureAuthModes`, `AwsCredentialSources`, and `GcpCredentialSources` hold the accepted string values. The C# models default `Endpoint` to an empty string, which the server rejects, so always set `Endpoint` (use the provider URLs above).

## Installation

```bash
dotnet add package Partio.Sdk
```

The package is published on [NuGet](https://www.nuget.org/packages/Partio.Sdk/) with symbols. You can also reference `Partio.Sdk.csproj` directly from this repository.

## Prerequisites

- .NET 8.0 SDK or later

## Project Structure

```
csharp/
  Partio.Sdk/              # SDK library
    PartioClient.cs         # Main client class
    PartioException.cs      # Custom exception type
    Models/                 # Request/response model classes
  Partio.Sdk.TestHarness/   # Test harness console app
    Program.cs
  Partio.Sdk.sln            # Solution file
```

## Usage

```csharp
using Partio.Sdk;
using Partio.Sdk.Models;

using var client = new PartioClient("http://localhost:8400", "your-access-key");

// Process a semantic cell
var result = await client.ProcessAsync(new SemanticCellRequest
{
    Type = "Text",
    Text = "Hello, world!",
    EmbeddingConfiguration = new EmbeddingConfiguration
    {
        EmbeddingEndpointId = "eep_your_endpoint_id"
    }
});

Console.WriteLine($"Chunks: {result.Chunks.Count}");

var explorer = await client.ExploreCompletionEndpointAsync(new EndpointExplorerCompletionRequest
{
    EndpointId = "cep_your_completion_endpoint_id",
    Prompt = "Explain what Partio does in one short paragraph.",
    TimeoutMs = 60000
});

Console.WriteLine($"Explorer success: {explorer?.Success}");

ModelLoadResponse? loadResult = await client.LoadCompletionEndpointAsync(
    "cep_your_completion_endpoint_id",
    new ModelLoadRequest
    {
        Strategy = "Auto",
        KeepAlive = "30m"
    });

Console.WriteLine($"{loadResult?.Outcome}: {loadResult?.Message}");
```

```csharp
EmbeddingEndpoint? endpoint = await client.CreateEndpointAsync(new EmbeddingEndpoint
{
    TenantId = "default",
    Name = "Pinned MiniLM",
    Model = "all-minilm",
    Endpoint = "http://localhost:11434",
    ApiFormat = "Ollama",
    MaximumTimeoutMs = 60000,
    MaxConcurrentRequests = 2
});
```

## Running the Test Harness

The test harness runs a comprehensive suite of CRUD and processing tests against a live Partio server.

Using the launcher scripts:

```bash
# Windows
go.bat http://localhost:8400 partioadmin

# Linux / macOS
./go.sh http://localhost:8400 partioadmin
```

Or directly with `dotnet`:

```bash
dotnet run --project Partio.Sdk.TestHarness -- http://localhost:8400 partioadmin
```

### Test Output

The harness prints one line per test with pass/fail status and elapsed time, followed by an overall summary:

```
Partio C# SDK Test Harness
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
