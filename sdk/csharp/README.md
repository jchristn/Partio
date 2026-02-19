# Partio C# SDK

A .NET client library for the Partio REST API. Supports .NET 8.0 and .NET 10.0.

## Overview

The Partio C# SDK provides a strongly-typed client (`PartioClient`) for interacting with a Partio server. It covers the full API surface:

- Health checks and identity (`HealthAsync`, `WhoAmIAsync`)
- Tenant CRUD (`CreateTenantAsync`, `GetTenantAsync`, `UpdateTenantAsync`, `DeleteTenantAsync`, `TenantExistsAsync`, `EnumerateTenantsAsync`)
- User CRUD (`CreateUserAsync`, `GetUserAsync`, `UpdateUserAsync`, `DeleteUserAsync`, `UserExistsAsync`, `EnumerateUsersAsync`)
- Credential CRUD (`CreateCredentialAsync`, `GetCredentialAsync`, `UpdateCredentialAsync`, `DeleteCredentialAsync`, `CredentialExistsAsync`, `EnumerateCredentialsAsync`)
- Embedding Endpoint CRUD (`CreateEndpointAsync`, `GetEndpointAsync`, `UpdateEndpointAsync`, `DeleteEndpointAsync`, `EndpointExistsAsync`, `EnumerateEndpointsAsync`)
- Completion Endpoint CRUD (`CreateCompletionEndpointAsync`, `GetCompletionEndpointAsync`, `UpdateCompletionEndpointAsync`, `DeleteCompletionEndpointAsync`, `CompletionEndpointExistsAsync`, `EnumerateCompletionEndpointsAsync`)
- Embedding & Completion Endpoint Health (`GetEndpointHealthAsync`, `GetAllEndpointHealthAsync`, `GetCompletionEndpointHealthAsync`, `GetAllCompletionEndpointHealthAsync`)
- Semantic cell processing (`ProcessAsync`, `ProcessBatchAsync`)
- Request history (`GetRequestHistoryAsync`, `GetRequestHistoryDetailAsync`, `DeleteRequestHistoryAsync`, `EnumerateRequestHistoryAsync`)

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

using var client = new PartioClient("http://localhost:8000", "your-access-key");

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

Or directly with `dotnet`:

```bash
dotnet run --project Partio.Sdk.TestHarness -- http://localhost:8000 partioadmin
```

### Test Output

The harness prints one line per test with pass/fail status and elapsed time, followed by an overall summary:

```
Partio C# SDK Test Harness
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
