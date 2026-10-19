"""Partio Python SDK Test Harness."""

import json
import os
import sys
import time
import urllib.request
from partio_sdk import PartioClient, PartioError


class SkipTest(Exception):
    pass


def main():
    endpoint = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:8400"
    admin_key = sys.argv[2] if len(sys.argv) > 2 else "partioadmin"
    provider_endpoint = os.environ.get("PARTIO_TEST_PROVIDER_ENDPOINT") or (sys.argv[3] if len(sys.argv) > 3 else "http://localhost:11434")
    embedding_model = os.environ.get("PARTIO_TEST_EMBEDDING_MODEL") or (sys.argv[4] if len(sys.argv) > 4 else "nomic-embed-text")
    completion_model = os.environ.get("PARTIO_TEST_COMPLETION_MODEL") or (sys.argv[5] if len(sys.argv) > 5 else "gemma3:4b")
    provider_bearer = os.environ.get("PARTIO_TEST_PROVIDER_BEARER") or (sys.argv[6] if len(sys.argv) > 6 else None)
    provider_available = is_provider_available(provider_endpoint, provider_bearer)

    print("Partio Python SDK Test Harness")
    print(f"Endpoint: {endpoint}")
    print(f"Admin Key: {admin_key}")
    print(f"Provider Endpoint: {provider_endpoint} ({'available' if provider_available else 'unavailable; provider-dependent tests will skip'})")
    print(f"Embedding Model: {embedding_model}")
    print(f"Completion Model: {completion_model}")
    print()

    passed = 0
    failed = 0
    skipped = 0
    failed_tests = []
    total_start = time.time()

    test_tenant_id = None
    test_user_id = None
    test_cred_id = None
    test_ep_id = None
    test_cep_id = None
    gemini_ep_id = None
    vllm_ep_id = None
    gemini_cep_id = None
    vllm_cep_id = None

    with PartioClient(endpoint, admin_key) as client:

        def run_test(name, fn):
            nonlocal passed, failed, skipped
            start = time.time()
            try:
                fn()
                elapsed = int((time.time() - start) * 1000)
                print(f"  PASS  {name} ({elapsed}ms)")
                passed += 1
            except SkipTest as ex:
                elapsed = int((time.time() - start) * 1000)
                print(f"  SKIP  {name} ({elapsed}ms) - {ex}")
                skipped += 1
            except Exception as ex:
                elapsed = int((time.time() - start) * 1000)
                print(f"  FAIL  {name} ({elapsed}ms) - {ex}")
                failed += 1
                failed_tests.append(name)

        def skip_if_provider_unavailable():
            if not provider_available:
                raise SkipTest(f"requires reachable Ollama-compatible provider at {provider_endpoint}")

        def get_harness_embedding_endpoint():
            active_ep = client.get_endpoint(test_ep_id)
            if not active_ep or active_ep.get("Active", True) is False:
                raise SkipTest("no active embedding endpoint")
            return active_ep

        # Health
        def test_health():
            result = client.health()
            assert result and result.get("Status") == "Healthy"
        run_test("Health Check", test_health)

        def test_whoami():
            result = client.whoami()
            assert result and "Role" in result
        run_test("Who Am I", test_whoami)

        # Tenant CRUD
        def test_create_tenant():
            nonlocal test_tenant_id
            tenant = client.create_tenant({"Name": "Test Tenant", "Labels": ["test"]})
            assert tenant and "Id" in tenant
            test_tenant_id = tenant["Id"]
        run_test("Create Tenant", test_create_tenant)

        def test_read_tenant():
            tenant = client.get_tenant(test_tenant_id)
            assert tenant and tenant["Name"] == "Test Tenant"
        run_test("Read Tenant", test_read_tenant)

        def test_update_tenant():
            updated = client.update_tenant(test_tenant_id, {"Name": "Updated Tenant"})
            assert updated and updated["Name"] == "Updated Tenant"
        run_test("Update Tenant", test_update_tenant)

        def test_tenant_exists():
            assert client.tenant_exists(test_tenant_id)
        run_test("Tenant Exists (HEAD)", test_tenant_exists)

        def test_enumerate_tenants():
            result = client.enumerate_tenants()
            assert result and len(result.get("Data", [])) > 0
        run_test("Enumerate Tenants", test_enumerate_tenants)

        # User CRUD
        def test_create_user():
            nonlocal test_user_id
            user = client.create_user({"TenantId": test_tenant_id, "Email": "test@test.com", "Password": "testpass"})
            assert user and "Id" in user
            test_user_id = user["Id"]
        run_test("Create User", test_create_user)

        def test_read_user():
            user = client.get_user(test_user_id)
            assert user and user["Email"] == "test@test.com"
        run_test("Read User", test_read_user)

        def test_update_user():
            updated = client.update_user(test_user_id, {"Email": "updated@test.com", "TenantId": test_tenant_id})
            assert updated is not None
        run_test("Update User", test_update_user)

        def test_user_exists():
            assert client.user_exists(test_user_id)
        run_test("User Exists (HEAD)", test_user_exists)

        def test_enumerate_users():
            result = client.enumerate_users()
            assert result and len(result.get("Data", [])) > 0
        run_test("Enumerate Users", test_enumerate_users)

        # Credential CRUD
        def test_create_credential():
            nonlocal test_cred_id
            cred = client.create_credential({"TenantId": test_tenant_id, "UserId": test_user_id, "Name": "Test Key"})
            assert cred and "Id" in cred
            test_cred_id = cred["Id"]
        run_test("Create Credential", test_create_credential)

        def test_read_credential():
            cred = client.get_credential(test_cred_id)
            assert cred and cred["Name"] == "Test Key"
        run_test("Read Credential", test_read_credential)

        def test_credential_exists():
            assert client.credential_exists(test_cred_id)
        run_test("Credential Exists (HEAD)", test_credential_exists)

        def test_enumerate_credentials():
            result = client.enumerate_credentials()
            assert result and len(result.get("Data", [])) > 0
        run_test("Enumerate Credentials", test_enumerate_credentials)

        # Endpoint CRUD
        def test_create_endpoint():
            nonlocal test_ep_id
            ep = client.create_endpoint({
                "TenantId": test_tenant_id,
                "Name": "Test Embedding",
                "Model": embedding_model,
                "Endpoint": provider_endpoint,
                "ApiFormat": "Ollama",
                "HealthCheckEnabled": False,
                "MaximumTimeoutMs": 61000,
                "MaxConcurrentRequests": 3,
                "MaxQueueDepth": 3,
                "Tokenization": {
                    "TokenizerKind": "BertWordPiece",
                    "TokenizerModel": "bert-base-uncased",
                    "MaxInputTokens": 384,
                    "ReservedInputTokens": 16,
                    "BatchLimitMode": "PerInput",
                    "AutoDetect": True
                }
            })
            assert ep and "Id" in ep
            assert ep.get("MaximumTimeoutMs") == 61000
            assert ep.get("MaxConcurrentRequests") == 3
            assert ep.get("MaxQueueDepth") == 3
            test_ep_id = ep["Id"]
        run_test("Create Endpoint", test_create_endpoint)

        def test_read_endpoint():
            ep = client.get_endpoint(test_ep_id)
            assert ep and ep["Model"] == embedding_model
            assert ep.get("MaximumTimeoutMs") == 61000
            assert ep.get("MaxConcurrentRequests") == 3
            assert ep.get("MaxQueueDepth") == 3
            assert ep.get("Tokenization"), "Expected tokenization override"
            assert ep["Tokenization"].get("TokenizerModel") == "bert-base-uncased"
            assert ep["Tokenization"].get("MaxInputTokens") == 384
        run_test("Read Endpoint", test_read_endpoint)

        def test_update_endpoint():
            updated = client.update_endpoint(test_ep_id, {
                "TenantId": test_tenant_id,
                "Name": "Updated Embedding",
                "Model": embedding_model,
                "Endpoint": provider_endpoint,
                "ApiFormat": "Ollama",
                "HealthCheckEnabled": False,
                "MaximumTimeoutMs": 91000,
                "MaxConcurrentRequests": 5,
                "MaxQueueDepth": 4,
                "Tokenization": {
                    "TokenizerKind": "Cl100kBase",
                    "TokenizerModel": "cl100k_base",
                    "MaxInputTokens": 2048,
                    "ReservedInputTokens": 32,
                    "BatchLimitMode": "WholeRequest",
                    "AutoDetect": False
                }
            })
            assert updated is not None
            assert updated.get("MaximumTimeoutMs") == 91000
            assert updated.get("MaxConcurrentRequests") == 5
            assert updated.get("MaxQueueDepth") == 4
            assert updated.get("Tokenization"), "Expected updated tokenization override"
            assert updated["Tokenization"].get("BatchLimitMode") == "WholeRequest"
            assert updated["Tokenization"].get("AutoDetect") is False
        run_test("Update Endpoint", test_update_endpoint)

        def test_endpoint_exists():
            assert client.endpoint_exists(test_ep_id)
        run_test("Endpoint Exists (HEAD)", test_endpoint_exists)

        def test_enumerate_endpoints():
            result = client.enumerate_endpoints()
            assert result and len(result.get("Data", [])) > 0
        run_test("Enumerate Endpoints", test_enumerate_endpoints)

        def test_create_gemini_endpoint():
            nonlocal gemini_ep_id
            ep = client.create_endpoint({"TenantId": test_tenant_id, "Name": "Gemini Embedding", "Model": "gemini-embedding-001", "Endpoint": "https://generativelanguage.googleapis.com", "ApiFormat": "Gemini", "ApiKey": "test-api-key", "HealthCheckEnabled": False})
            assert ep and "Id" in ep
            gemini_ep_id = ep["Id"]
        run_test("Create Gemini Embedding Endpoint", test_create_gemini_endpoint)

        def test_create_vllm_endpoint():
            nonlocal vllm_ep_id
            ep = client.create_endpoint({"TenantId": test_tenant_id, "Name": "vLLM Embedding", "Model": "intfloat/e5-small-v2", "Endpoint": "http://localhost:8000", "ApiFormat": "vLLM", "HealthCheckEnabled": False})
            assert ep and "Id" in ep
            vllm_ep_id = ep["Id"]
        run_test("Create vLLM Embedding Endpoint", test_create_vllm_endpoint)

        # Completion Endpoint CRUD
        def test_create_completion_endpoint():
            nonlocal test_cep_id
            cep = client.create_completion_endpoint({"TenantId": test_tenant_id, "Name": "Test Inference", "Model": completion_model, "Endpoint": provider_endpoint, "ApiFormat": "Ollama", "HealthCheckEnabled": False, "MaximumTimeoutMs": 61000, "MaxConcurrentRequests": 3, "MaxQueueDepth": 5})
            assert cep and "Id" in cep
            assert cep.get("MaximumTimeoutMs") == 61000
            assert cep.get("MaxConcurrentRequests") == 3
            assert cep.get("MaxQueueDepth") == 5
            test_cep_id = cep["Id"]
        run_test("Create Completion Endpoint", test_create_completion_endpoint)

        def test_read_completion_endpoint():
            cep = client.get_completion_endpoint(test_cep_id)
            assert cep and cep["Model"] == completion_model
            assert cep.get("MaximumTimeoutMs") == 61000
            assert cep.get("MaxConcurrentRequests") == 3
            assert cep.get("MaxQueueDepth") == 5
        run_test("Read Completion Endpoint", test_read_completion_endpoint)

        def test_update_completion_endpoint():
            updated = client.update_completion_endpoint(test_cep_id, {"TenantId": test_tenant_id, "Name": "Updated Inference", "Model": completion_model, "Endpoint": provider_endpoint, "ApiFormat": "Ollama", "HealthCheckEnabled": False, "MaximumTimeoutMs": 91000, "MaxConcurrentRequests": 5, "MaxQueueDepth": 6})
            assert updated is not None
            assert updated.get("MaximumTimeoutMs") == 91000
            assert updated.get("MaxConcurrentRequests") == 5
            assert updated.get("MaxQueueDepth") == 6
        run_test("Update Completion Endpoint", test_update_completion_endpoint)

        def test_completion_endpoint_exists():
            assert client.completion_endpoint_exists(test_cep_id)
        run_test("Completion Endpoint Exists (HEAD)", test_completion_endpoint_exists)

        def test_enumerate_completion_endpoints():
            result = client.enumerate_completion_endpoints()
            assert result and len(result.get("Data", [])) > 0
        run_test("Enumerate Completion Endpoints", test_enumerate_completion_endpoints)

        def test_create_gemini_completion_endpoint():
            nonlocal gemini_cep_id
            cep = client.create_completion_endpoint({"TenantId": test_tenant_id, "Name": "Gemini Inference", "Model": "gemini-2.5-flash", "Endpoint": "https://generativelanguage.googleapis.com", "ApiFormat": "Gemini", "ApiKey": "test-api-key", "HealthCheckEnabled": False})
            assert cep and "Id" in cep
            gemini_cep_id = cep["Id"]
        run_test("Create Gemini Completion Endpoint", test_create_gemini_completion_endpoint)

        def test_create_vllm_completion_endpoint():
            nonlocal vllm_cep_id
            cep = client.create_completion_endpoint({"TenantId": test_tenant_id, "Name": "vLLM Inference", "Model": "Qwen/Qwen2.5-7B-Instruct", "Endpoint": "http://localhost:8000", "ApiFormat": "vLLM", "HealthCheckEnabled": False})
            assert cep and "Id" in cep
            vllm_cep_id = cep["Id"]
        run_test("Create vLLM Completion Endpoint", test_create_vllm_completion_endpoint)

        def test_create_cloud_provider_endpoints():
            cloud = client.create_completion_endpoint({"TenantId": test_tenant_id, "Name": "Ollama Cloud Inference", "Model": "gpt-oss:20b", "Endpoint": "http://ignored-host:11434", "ApiFormat": "OllamaCloud", "ApiKey": "test-api-key", "HealthCheckEnabled": False})
            try:
                assert cloud and "Id" in cloud
                assert cloud["Endpoint"] == "https://ollama.com", "Expected the fixed Ollama Cloud host, got " + str(cloud.get("Endpoint"))
            finally:
                if cloud and "Id" in cloud:
                    client.delete_completion_endpoint(cloud["Id"])

            xai = client.create_endpoint({"TenantId": test_tenant_id, "Name": "xAI Embedding", "Model": "grok-embedding", "Endpoint": "https://api.x.ai/v1", "ApiFormat": "xAI", "ApiKey": "test-api-key", "HealthCheckEnabled": False})
            try:
                assert xai and "Id" in xai
                assert xai["Endpoint"] == "https://api.x.ai", "Expected the /v1 suffix to be dropped, got " + str(xai.get("Endpoint"))
            finally:
                if xai and "Id" in xai:
                    client.delete_endpoint(xai["Id"])

            try:
                client.create_completion_endpoint({"TenantId": test_tenant_id, "Name": "xAI Missing Key", "Model": "grok-4", "Endpoint": "https://api.x.ai", "ApiFormat": "xAI", "HealthCheckEnabled": False})
                raise AssertionError("Expected 400")
            except PartioError as e:
                assert e.status_code == 400
        run_test("Create Ollama Cloud and xAI Endpoints", test_create_cloud_provider_endpoints)

        # Provider-specific endpoints, secret masking, and reveal
        azure_key = "azure-test-key-1234567890"
        anthropic_key = "sk-ant-test-key-abcdef1234"
        bedrock_secret = "bedrock-secret-abcdefgh"
        bedrock_session = "short"
        voyage_key = "pa-voyage-test-key-9876"
        service_account_json = json.dumps({
            "type": "service_account",
            "project_id": "partio-test",
            "client_email": "partio@partio-test.iam.gserviceaccount.com",
            "private_key": "-----BEGIN PRIVATE KEY-----\nMIIBVQIBADANBgkqhkiG9w0BAQEFAASCAT8wggE7AgEAAkEA\n-----END PRIVATE KEY-----\n",
        })
        provider_ids = {}

        def expect_400(fn, label):
            try:
                fn()
            except PartioError as e:
                assert e.status_code == 400, f"Expected 400 for {label}, got {e.status_code}"
                return
            raise AssertionError("Expected 400 for " + label)

        def test_create_provider_endpoints():
            azure = client.create_completion_endpoint({
                "TenantId": test_tenant_id, "Name": "Azure Inference", "Model": "gpt-4o-mini", "Endpoint": "https://partio-test.openai.azure.com/openai",
                "ApiFormat": "AzureOpenAI", "ApiKey": azure_key, "HealthCheckEnabled": False,
                "ProviderSettings": {"AzureOpenAI": {"ApiVersion": "2024-10-21", "AuthMode": "ApiKey"}},
            })
            assert azure and "Id" in azure, "No Azure response"
            provider_ids["azure"] = azure["Id"]
            assert azure["Endpoint"] == "https://partio-test.openai.azure.com", "Expected the /openai suffix to be dropped, got " + str(azure.get("Endpoint"))
            assert azure["ApiKey"] == "****7890", "Expected masked Azure ApiKey, got " + str(azure.get("ApiKey"))
            assert azure["ProviderSettings"]["AzureOpenAI"]["ApiVersion"] == "2024-10-21", "Azure ApiVersion mismatch"

            anthropic = client.create_completion_endpoint({
                "TenantId": test_tenant_id, "Name": "Anthropic Inference", "Model": "claude-opus-4-8",
                "ApiFormat": "Anthropic", "ApiKey": anthropic_key, "HealthCheckEnabled": False,
            })
            assert anthropic and "Id" in anthropic, "No Anthropic response"
            provider_ids["anthropic"] = anthropic["Id"]
            assert anthropic["Endpoint"] == "https://api.anthropic.com", "Expected the default Anthropic endpoint, got " + str(anthropic.get("Endpoint"))

            bedrock = client.create_endpoint({
                "TenantId": test_tenant_id, "Name": "Bedrock Embedding", "Model": "amazon.titan-embed-text-v2:0",
                "ApiFormat": "Bedrock", "HealthCheckEnabled": False,
                "ProviderSettings": {"Bedrock": {"Region": "us-east-1", "CredentialSource": "Static", "AccessKeyId": "AKIAPARTIOTEST", "SecretAccessKey": bedrock_secret, "SessionToken": bedrock_session}},
            })
            assert bedrock and "Id" in bedrock, "No Bedrock response"
            provider_ids["bedrock"] = bedrock["Id"]
            assert bedrock["Endpoint"] == "https://bedrock-runtime.us-east-1.amazonaws.com", "Expected the region-derived Bedrock endpoint, got " + str(bedrock.get("Endpoint"))

            vertex = client.create_endpoint({
                "TenantId": test_tenant_id, "Name": "Vertex Embedding", "Model": "text-embedding-005",
                "ApiFormat": "VertexAI", "HealthCheckEnabled": False,
                "ProviderSettings": {"VertexAI": {"ProjectId": "partio-test", "Region": "us-central1", "CredentialSource": "ServiceAccountJson", "ServiceAccountJson": service_account_json}},
            })
            assert vertex and "Id" in vertex, "No Vertex response"
            provider_ids["vertex"] = vertex["Id"]
            assert vertex["Endpoint"] == "https://us-central1-aiplatform.googleapis.com", "Expected the region-derived Vertex endpoint, got " + str(vertex.get("Endpoint"))

            tei = client.create_endpoint({
                "TenantId": test_tenant_id, "Name": "TEI Embedding", "Model": "BAAI/bge-small-en-v1.5", "Endpoint": "http://localhost:8080",
                "ApiFormat": "TEI", "HealthCheckEnabled": False,
            })
            assert tei and "Id" in tei, "No TEI response"
            provider_ids["tei"] = tei["Id"]

            voyage = client.create_endpoint({
                "TenantId": test_tenant_id, "Name": "Voyage Embedding", "Model": "voyage-3.5",
                "ApiFormat": "VoyageAI", "ApiKey": voyage_key, "HealthCheckEnabled": False,
                "ProviderSettings": {"VoyageAI": {"InputType": "document", "OutputDimension": 1024}},
            })
            assert voyage and "Id" in voyage, "No Voyage response"
            provider_ids["voyage"] = voyage["Id"]
            assert voyage["ProviderSettings"]["VoyageAI"]["OutputDimension"] == 1024, "Voyage OutputDimension mismatch"
        run_test("Create Provider-Specific Endpoints", test_create_provider_endpoints)

        def test_read_masks_secrets():
            azure = client.get_completion_endpoint(provider_ids["azure"])
            assert azure["ApiKey"] == "****7890", "Expected masked Azure ApiKey, got " + str(azure.get("ApiKey"))
            aws = client.get_endpoint(provider_ids["bedrock"])["ProviderSettings"]["Bedrock"]
            assert aws["SecretAccessKey"] == "****efgh", "Expected masked SecretAccessKey, got " + str(aws.get("SecretAccessKey"))
            assert aws["SessionToken"] == "****", "Expected masked SessionToken, got " + str(aws.get("SessionToken"))
            assert aws["AccessKeyId"] == "AKIAPARTIOTEST", "AccessKeyId should not be masked"
            vertex = client.get_endpoint(provider_ids["vertex"])
            assert vertex["ProviderSettings"]["VertexAI"]["ServiceAccountJson"] == "****", "Expected masked ServiceAccountJson"
            listed = client.enumerate_completion_endpoints() or {}
            for item in listed.get("Data") or []:
                key = item.get("ApiKey")
                assert not key or key.startswith("****"), "Enumeration returned an unmasked ApiKey for " + str(item.get("Id"))
            assert client.get_completion_endpoint(provider_ids["anthropic"])["ApiKey"] == "****1234", "Expected masked Anthropic ApiKey"
        run_test("Read Masks Endpoint Secrets", test_read_masks_secrets)

        def test_reveal_secrets():
            assert client.reveal_completion_endpoint(provider_ids["azure"])["ApiKey"] == azure_key, "Reveal did not return the Azure ApiKey"
            aws = client.reveal_endpoint(provider_ids["bedrock"])["ProviderSettings"]["Bedrock"]
            assert aws["SecretAccessKey"] == bedrock_secret, "Reveal did not return the SecretAccessKey"
            assert aws["SessionToken"] == bedrock_session, "Reveal did not return the SessionToken"
            vertex = client.reveal_endpoint(provider_ids["vertex"])
            assert vertex["ProviderSettings"]["VertexAI"]["ServiceAccountJson"] == service_account_json, "Reveal did not return the ServiceAccountJson"
            with PartioClient(endpoint, "invalid-token") as bad_client:
                try:
                    bad_client.reveal_endpoint(provider_ids["bedrock"])
                    raise AssertionError("Expected unauthenticated reveal to fail")
                except PartioError as e:
                    assert e.status_code == 401, f"Expected 401, got {e.status_code}"
        run_test("Reveal Endpoint Secrets", test_reveal_secrets)

        def test_update_keeps_masked_secrets():
            azure = client.get_completion_endpoint(provider_ids["azure"])
            azure["Name"] = "Azure Inference Renamed"
            updated = client.update_completion_endpoint(provider_ids["azure"], azure)
            assert updated["Name"] == "Azure Inference Renamed", "Azure rename failed"
            assert client.reveal_completion_endpoint(provider_ids["azure"])["ApiKey"] == azure_key, "Azure ApiKey was overwritten by the masked value"

            bedrock = client.get_endpoint(provider_ids["bedrock"])
            bedrock["Name"] = "Bedrock Embedding Renamed"
            client.update_endpoint(provider_ids["bedrock"], bedrock)
            aws = client.reveal_endpoint(provider_ids["bedrock"])["ProviderSettings"]["Bedrock"]
            assert aws["SecretAccessKey"] == bedrock_secret, "SecretAccessKey was overwritten by the masked value"
            assert aws["SessionToken"] == bedrock_session, "SessionToken was overwritten by the masked value"

            vertex = client.get_endpoint(provider_ids["vertex"])
            client.update_endpoint(provider_ids["vertex"], vertex)
            revealed = client.reveal_endpoint(provider_ids["vertex"])
            assert revealed["ProviderSettings"]["VertexAI"]["ServiceAccountJson"] == service_account_json, "ServiceAccountJson was overwritten by the masked value"

            voyage = client.get_endpoint(provider_ids["voyage"])
            voyage["ApiKey"] = "pa-voyage-rotated-key-0000"
            client.update_endpoint(provider_ids["voyage"], voyage)
            assert client.reveal_endpoint(provider_ids["voyage"])["ApiKey"] == "pa-voyage-rotated-key-0000", "A new ApiKey should replace the stored one"
        run_test("Update Echoing Masked Secrets Keeps Them", test_update_keeps_masked_secrets)

        def test_provider_validation_errors():
            expect_400(lambda: client.create_completion_endpoint({"TenantId": test_tenant_id, "Name": "TEI Inference", "Model": "bge", "Endpoint": "http://localhost:8080", "ApiFormat": "TEI", "HealthCheckEnabled": False}), "TEI completion")
            expect_400(lambda: client.create_endpoint({"TenantId": test_tenant_id, "Name": "Anthropic Embedding", "Model": "claude", "ApiFormat": "Anthropic", "ApiKey": anthropic_key, "HealthCheckEnabled": False}), "Anthropic embedding")
            expect_400(lambda: client.create_endpoint({
                "TenantId": test_tenant_id, "Name": "Bedrock No Region", "Model": "amazon.titan-embed-text-v2:0", "Endpoint": "https://bedrock-runtime.us-east-1.amazonaws.com",
                "ApiFormat": "Bedrock", "HealthCheckEnabled": False,
                "ProviderSettings": {"Bedrock": {"AccessKeyId": "AKIAPARTIOTEST", "SecretAccessKey": bedrock_secret}},
            }), "Bedrock without Region")
            expect_400(lambda: client.create_completion_endpoint({"TenantId": test_tenant_id, "Name": "Azure No Key", "Model": "gpt-4o-mini", "Endpoint": "https://partio-test.openai.azure.com", "ApiFormat": "AzureOpenAI", "HealthCheckEnabled": False}), "Azure without ApiKey")
        run_test("Provider Validation Errors (400)", test_provider_validation_errors)

        def test_delete_provider_endpoints():
            for key in ("azure", "anthropic"):
                if provider_ids.get(key):
                    client.delete_completion_endpoint(provider_ids[key])
            for key in ("bedrock", "vertex", "tei", "voyage"):
                if provider_ids.get(key):
                    client.delete_endpoint(provider_ids[key])
            if provider_ids.get("bedrock"):
                assert not client.endpoint_exists(provider_ids["bedrock"]), "Bedrock endpoint still exists"
        run_test("Delete Provider-Specific Endpoints", test_delete_provider_endpoints)

        def test_load_completion_endpoint():
            skip_if_provider_unavailable()
            result = client.load_completion_endpoint(test_cep_id, {"KeepAlive": "30m"})
            assert result is not None, "No response"
            assert result.get("Success") is True, "Load failed: " + str(result.get("Message"))
            assert result.get("EndpointId") == test_cep_id, "Endpoint mismatch"
            assert result.get("Outcome") == "Loaded", "Expected Loaded"
        run_test("Load Completion Endpoint", test_load_completion_endpoint)

        def test_load_embedding_endpoint():
            skip_if_provider_unavailable()
            result = client.load_endpoint(test_ep_id, {"KeepAlive": "30m"})
            assert result is not None, "No response"
            assert result.get("Success") is True, "Load failed: " + str(result.get("Message"))
            assert result.get("EndpointId") == test_ep_id, "Endpoint mismatch"
            assert result.get("Outcome") == "Loaded", "Expected Loaded"
        run_test("Load Embedding Endpoint", test_load_embedding_endpoint)

        def test_unsupported_native_load():
            try:
                client.load_completion_endpoint(vllm_cep_id, {"Strategy": "NativeProviderLoad", "RequireNativeLoad": True})
                raise Exception("Expected PartioError with 409")
            except PartioError as exc:
                assert exc.status_code == 409
        run_test("Unsupported Native Load", test_unsupported_native_load)

        # Request History
        def test_enumerate_history():
            result = client.enumerate_request_history()
            assert result is not None
        run_test("Enumerate Request History", test_enumerate_history)

        def test_explore_embedding_endpoint():
            skip_if_provider_unavailable()
            result = client.explore_embedding_endpoint({
                "EndpointId": test_ep_id,
                "Input": "Python SDK explorer embedding payload"
            })
            assert result is not None, "No response"
            assert result.get("EndpointId") == test_ep_id, "Endpoint mismatch"
            assert result.get("EmbeddingCalls") and len(result["EmbeddingCalls"]) > 0, "Expected upstream call details"
            assert result.get("TokenizationProfile"), "Expected tokenization profile"
            assert result["TokenizationProfile"].get("EffectiveInputBudget", 0) > 0, "Expected effective input budget"
            if result.get("RequestHistoryId"):
                detail = client.get_request_history_detail(result["RequestHistoryId"])
                assert detail and detail.get("TokenizationProfile"), "Expected tokenization profile in request history detail"
        run_test("Explore Embedding Endpoint", test_explore_embedding_endpoint)

        def test_explore_completion_endpoint():
            skip_if_provider_unavailable()
            result = client.explore_completion_endpoint({
                "EndpointId": test_cep_id,
                "Prompt": "Python SDK explorer completion payload"
            })
            assert result is not None, "No response"
            assert result.get("EndpointId") == test_cep_id, "Endpoint mismatch"
            assert result.get("CompletionCalls") and len(result["CompletionCalls"]) > 0, "Expected upstream call details"
        run_test("Explore Completion Endpoint", test_explore_completion_endpoint)

        # Proxy (transparent passthrough): relay native Ollama requests through a dedicated Ollama endpoint.
        proxy_state = {"cep_id": None}

        def test_proxy_create_endpoint():
            cep = client.create_completion_endpoint({
                "TenantId": test_tenant_id, "Name": "Proxy Passthrough", "Model": completion_model,
                "Endpoint": provider_endpoint, "ApiFormat": "Ollama", "ApiKey": provider_bearer,
                "HealthCheckEnabled": False, "MaximumTimeoutMs": 120000
            })
            assert cep and "Id" in cep, "No proxy endpoint returned"
            proxy_state["cep_id"] = cep["Id"]
        run_test("Proxy Create Endpoint", test_proxy_create_endpoint)

        def test_proxy_get_tags():
            skip_if_provider_unavailable()
            r = client.proxy_get(proxy_state["cep_id"], "api/tags")
            assert r["status_code"] == 200, f"Expected 200, got {r['status_code']}: {r['body']}"
        run_test("Proxy GET api/tags", test_proxy_get_tags)

        def test_proxy_post_chat():
            skip_if_provider_unavailable()
            body = {"model": completion_model, "messages": [{"role": "user", "content": "Reply with exactly: OK"}], "stream": False}
            r = client.proxy_post(proxy_state["cep_id"], "api/chat", body)
            assert r["status_code"] == 200, f"Expected 200, got {r['status_code']}: {r['body']}"
        run_test("Proxy POST api/chat", test_proxy_post_chat)

        def test_proxy_disallowed_path():
            # An OpenAI path is not permitted for an Ollama endpoint -> 404 by policy, not forwarded.
            r = client.proxy_post(proxy_state["cep_id"], "v1/chat/completions", {})
            assert r["status_code"] == 404, f"Expected 404 for disallowed sub-path, got {r['status_code']}"
        run_test("Proxy Disallowed Sub-path (404)", test_proxy_disallowed_path)

        def test_proxy_delete_endpoint():
            if proxy_state["cep_id"]:
                client.delete_completion_endpoint(proxy_state["cep_id"])
        run_test("Proxy Delete Endpoint", test_proxy_delete_endpoint)

        # Process Single Cell (requires an active embedding endpoint)
        def test_process_single_cell():
            skip_if_provider_unavailable()
            active_ep = get_harness_embedding_endpoint()

            result = client.process({
                "Type": "Text",
                "Text": "Partio is a multi-tenant embedding platform.",
                "ChunkingConfiguration": {"Strategy": "FixedTokenCount", "FixedTokenCount": 256},
                "EmbeddingConfiguration": {"L2Normalization": False, "EmbeddingEndpointId": active_ep["Id"]},
                "Labels": ["test"],
                "Tags": {"source": "sdk-test"}
            })

            assert result is not None, "No response"
            assert result.get("Text"), "Missing Text"
            assert result.get("Chunks") and len(result["Chunks"]) > 0, "No chunks"
            assert result["Chunks"][0].get("Embeddings") and len(result["Chunks"][0]["Embeddings"]) > 0, "No embeddings"
            assert result["Chunks"][0].get("Labels") and len(result["Chunks"][0]["Labels"]) > 0, "No labels on chunk"
            assert result["Chunks"][0].get("Tags") and len(result["Chunks"][0]["Tags"]) > 0, "No tags on chunk"
        run_test("Process Single Cell", test_process_single_cell)

        # Process Table - Row
        def test_process_table_row():
            skip_if_provider_unavailable()
            active_ep = get_harness_embedding_endpoint()

            result = client.process({
                "Type": "Table",
                "Table": [["id", "firstname", "lastname"], ["1", "george", "bush"], ["2", "barack", "obama"]],
                "ChunkingConfiguration": {"Strategy": "Row"},
                "EmbeddingConfiguration": {"L2Normalization": False, "EmbeddingEndpointId": active_ep["Id"]}
            })
            assert result is not None, "No response"
            assert result.get("Chunks") and len(result["Chunks"]) == 2, "Expected 2 chunks"
        run_test("Process Table (Row)", test_process_table_row)

        # Process Table - RowWithHeaders
        def test_process_table_row_with_headers():
            skip_if_provider_unavailable()
            active_ep = get_harness_embedding_endpoint()

            result = client.process({
                "Type": "Table",
                "Table": [["id", "firstname", "lastname"], ["1", "george", "bush"], ["2", "barack", "obama"]],
                "ChunkingConfiguration": {"Strategy": "RowWithHeaders"},
                "EmbeddingConfiguration": {"L2Normalization": False, "EmbeddingEndpointId": active_ep["Id"]}
            })
            assert result is not None, "No response"
            assert result.get("Chunks") and len(result["Chunks"]) == 2, "Expected 2 chunks"
        run_test("Process Table (RowWithHeaders)", test_process_table_row_with_headers)

        # Process Table - RowGroupWithHeaders
        def test_process_table_row_group():
            skip_if_provider_unavailable()
            active_ep = get_harness_embedding_endpoint()

            result = client.process({
                "Type": "Table",
                "Table": [["id", "firstname", "lastname"], ["1", "george", "bush"], ["2", "barack", "obama"], ["3", "donald", "trump"]],
                "ChunkingConfiguration": {"Strategy": "RowGroupWithHeaders", "RowGroupSize": 2},
                "EmbeddingConfiguration": {"L2Normalization": False, "EmbeddingEndpointId": active_ep["Id"]}
            })
            assert result is not None, "No response"
            assert result.get("Chunks") and len(result["Chunks"]) == 2, "Expected 2 chunks (groups of 2)"
        run_test("Process Table (RowGroupWithHeaders)", test_process_table_row_group)

        # Process Table - KeyValuePairs
        def test_process_table_kv():
            skip_if_provider_unavailable()
            active_ep = get_harness_embedding_endpoint()

            result = client.process({
                "Type": "Table",
                "Table": [["id", "firstname", "lastname"], ["1", "george", "bush"]],
                "ChunkingConfiguration": {"Strategy": "KeyValuePairs"},
                "EmbeddingConfiguration": {"L2Normalization": False, "EmbeddingEndpointId": active_ep["Id"]}
            })
            assert result is not None, "No response"
            assert result.get("Chunks") and len(result["Chunks"]) == 1, "Expected 1 chunk"
        run_test("Process Table (KeyValuePairs)", test_process_table_kv)

        # Process Table - WholeTable
        def test_process_table_whole():
            skip_if_provider_unavailable()
            active_ep = get_harness_embedding_endpoint()

            result = client.process({
                "Type": "Table",
                "Table": [["id", "firstname", "lastname"], ["1", "george", "bush"], ["2", "barack", "obama"]],
                "ChunkingConfiguration": {"Strategy": "WholeTable"},
                "EmbeddingConfiguration": {"L2Normalization": False, "EmbeddingEndpointId": active_ep["Id"]}
            })
            assert result is not None, "No response"
            assert result.get("Chunks") and len(result["Chunks"]) == 1, "Expected 1 chunk"
        run_test("Process Table (WholeTable)", test_process_table_whole)

        # Process Text - RegexBased
        def test_process_regex_based():
            skip_if_provider_unavailable()
            active_ep = get_harness_embedding_endpoint()

            result = client.process({
                "Type": "Text",
                "Text": "# Intro\nSome text.\n\n# Body\nMore text.\n\n# End\nFinal text.",
                "ChunkingConfiguration": {
                    "Strategy": "RegexBased",
                    "RegexPattern": r"(?=^#{1,3}\s)",
                    "FixedTokenCount": 512
                },
                "EmbeddingConfiguration": {"L2Normalization": False, "EmbeddingEndpointId": active_ep["Id"]}
            })
            assert result is not None, "No response"
            assert result.get("Chunks") and len(result["Chunks"]) > 0, "No chunks"
        run_test("Process Text (RegexBased)", test_process_regex_based)

        # Regex Strategy Missing Pattern (400)
        def test_regex_missing_pattern():
            skip_if_provider_unavailable()
            active_ep = get_harness_embedding_endpoint()

            try:
                client.process({
                    "Type": "Text",
                    "Text": "Some text here.",
                    "ChunkingConfiguration": {"Strategy": "RegexBased"},
                    "EmbeddingConfiguration": {"L2Normalization": False, "EmbeddingEndpointId": active_ep["Id"]}
                })
                raise AssertionError("Expected 400")
            except PartioError as e:
                assert e.status_code == 400
        run_test("Regex Strategy Missing Pattern (400)", test_regex_missing_pattern)

        # Negative test: table strategy on text atom
        def test_table_strategy_on_text():
            skip_if_provider_unavailable()
            active_ep = get_harness_embedding_endpoint()

            try:
                client.process({
                    "Type": "Text",
                    "Text": "This is text, not a table.",
                    "ChunkingConfiguration": {"Strategy": "Row"},
                    "EmbeddingConfiguration": {"L2Normalization": False, "EmbeddingEndpointId": active_ep["Id"]}
                })
                raise AssertionError("Expected 400")
            except PartioError as e:
                assert e.status_code == 400
        run_test("Table Strategy on Text (400)", test_table_strategy_on_text)

        # Error cases
        def test_unauthenticated():
            with PartioClient(endpoint, "invalid-token") as bad_client:
                try:
                    bad_client.enumerate_tenants()
                    raise AssertionError("Expected 401")
                except PartioError as e:
                    assert e.status_code == 401
        run_test("Unauthenticated Request (401)", test_unauthenticated)

        def test_not_found():
            try:
                client.get_tenant("nonexistent-id-12345")
                raise AssertionError("Expected 404")
            except PartioError as e:
                assert e.status_code == 404
        run_test("Non-existent Resource (404)", test_not_found)

        # Cleanup
        def test_delete_completion_endpoint():
            client.delete_completion_endpoint(test_cep_id)
            assert not client.completion_endpoint_exists(test_cep_id)
        run_test("Delete Completion Endpoint", test_delete_completion_endpoint)

        def test_delete_gemini_completion_endpoint():
            if gemini_cep_id:
                client.delete_completion_endpoint(gemini_cep_id)
        run_test("Delete Gemini Completion Endpoint", test_delete_gemini_completion_endpoint)

        def test_delete_vllm_completion_endpoint():
            if vllm_cep_id:
                client.delete_completion_endpoint(vllm_cep_id)
        run_test("Delete vLLM Completion Endpoint", test_delete_vllm_completion_endpoint)

        def test_delete_endpoint():
            client.delete_endpoint(test_ep_id)
            assert not client.endpoint_exists(test_ep_id)
        run_test("Delete Endpoint", test_delete_endpoint)

        def test_delete_gemini_endpoint():
            if gemini_ep_id:
                client.delete_endpoint(gemini_ep_id)
        run_test("Delete Gemini Embedding Endpoint", test_delete_gemini_endpoint)

        def test_delete_vllm_endpoint():
            if vllm_ep_id:
                client.delete_endpoint(vllm_ep_id)
        run_test("Delete vLLM Embedding Endpoint", test_delete_vllm_endpoint)

        def test_delete_credential():
            client.delete_credential(test_cred_id)
        run_test("Delete Credential", test_delete_credential)

        def test_delete_user():
            client.delete_user(test_user_id)
        run_test("Delete User", test_delete_user)

        def test_delete_tenant():
            client.delete_tenant(test_tenant_id)
            assert not client.tenant_exists(test_tenant_id)
        run_test("Delete Tenant", test_delete_tenant)

    total_ms = int((time.time() - total_start) * 1000)

    print()
    print("=== SUMMARY ===")
    print(f"Total: {passed + failed + skipped}  Passed: {passed}  Failed: {failed}  Skipped: {skipped}")
    print(f"Runtime: {total_ms}ms")
    print(f"Result: {'PASS' if failed == 0 else 'FAIL'}")

    if failed_tests:
        print()
        print("Failed tests:")
        for name in failed_tests:
            print(f"  - {name}")

    print("================")
    sys.exit(0 if failed == 0 else 1)


def is_provider_available(endpoint, bearer=None):
    try:
        req = urllib.request.Request(endpoint.rstrip("/") + "/api/tags")
        if bearer:
            req.add_header("Authorization", f"Bearer {bearer}")
        with urllib.request.urlopen(req, timeout=2) as response:
            return 200 <= response.status < 300
    except Exception:
        return False


if __name__ == "__main__":
    main()
