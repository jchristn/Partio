# Partio Python SDK

A Python client library for the Partio REST API.

## Overview

The Partio Python SDK provides a `PartioClient` class for interacting with a Partio server. It covers the full API surface:

- Health checks and identity (`health`, `whoami`)
- Tenant CRUD (`create_tenant`, `get_tenant`, `update_tenant`, `delete_tenant`, `tenant_exists`, `enumerate_tenants`)
- User CRUD (`create_user`, `get_user`, `update_user`, `delete_user`, `user_exists`, `enumerate_users`)
- Credential CRUD (`create_credential`, `get_credential`, `update_credential`, `delete_credential`, `credential_exists`, `enumerate_credentials`)
- Embedding Endpoint CRUD (`create_endpoint`, `get_endpoint`, `update_endpoint`, `delete_endpoint`, `endpoint_exists`, `enumerate_endpoints`)
- Completion Endpoint CRUD (`create_completion_endpoint`, `get_completion_endpoint`, `update_completion_endpoint`, `delete_completion_endpoint`, `completion_endpoint_exists`, `enumerate_completion_endpoints`)
- Embedding & Completion Endpoint Health (`get_endpoint_health`, `get_all_endpoint_health`, `get_completion_endpoint_health`, `get_all_completion_endpoint_health`)
- Semantic cell processing (`process`, `process_batch`)
- Request history (`get_request_history`, `get_request_history_detail`, `delete_request_history`, `enumerate_request_history`)

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

with PartioClient("http://localhost:8000", "your-access-key") as client:
    result = client.process({
        "Type": "Text",
        "Text": "Hello, world!",
        "EmbeddingConfiguration": {
            "EmbeddingEndpointId": "eep_your_endpoint_id"
        }
    })
    print(f"Chunks: {len(result['Chunks'])}")
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

Or directly with Python:

```bash
python test_harness.py http://localhost:8000 partioadmin
```

### Test Output

The harness prints one line per test with pass/fail status and elapsed time, followed by an overall summary:

```
Partio Python SDK Test Harness
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
