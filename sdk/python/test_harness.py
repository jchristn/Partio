"""Partio Python SDK Test Harness."""

import sys
import time
from partio_sdk import PartioClient, PartioError


def main():
    endpoint = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:8400"
    admin_key = sys.argv[2] if len(sys.argv) > 2 else "partioadmin"

    print("Partio Python SDK Test Harness")
    print(f"Endpoint: {endpoint}")
    print(f"Admin Key: {admin_key}")
    print()

    passed = 0
    failed = 0
    failed_tests = []
    total_start = time.time()

    test_tenant_id = None
    test_user_id = None
    test_cred_id = None
    test_ep_id = None
    test_cep_id = None

    with PartioClient(endpoint, admin_key) as client:

        def run_test(name, fn):
            nonlocal passed, failed
            start = time.time()
            try:
                fn()
                elapsed = int((time.time() - start) * 1000)
                print(f"  PASS  {name} ({elapsed}ms)")
                passed += 1
            except Exception as ex:
                elapsed = int((time.time() - start) * 1000)
                print(f"  FAIL  {name} ({elapsed}ms) - {ex}")
                failed += 1
                failed_tests.append(name)

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
            ep = client.create_endpoint({"TenantId": test_tenant_id, "Model": "test-model", "Endpoint": "http://localhost:11434", "ApiFormat": "Ollama"})
            assert ep and "Id" in ep
            test_ep_id = ep["Id"]
        run_test("Create Endpoint", test_create_endpoint)

        def test_read_endpoint():
            ep = client.get_endpoint(test_ep_id)
            assert ep and ep["Model"] == "test-model"
        run_test("Read Endpoint", test_read_endpoint)

        def test_update_endpoint():
            updated = client.update_endpoint(test_ep_id, {"TenantId": test_tenant_id, "Model": "test-model-updated", "Endpoint": "http://localhost:11434", "ApiFormat": "Ollama"})
            assert updated is not None
        run_test("Update Endpoint", test_update_endpoint)

        def test_endpoint_exists():
            assert client.endpoint_exists(test_ep_id)
        run_test("Endpoint Exists (HEAD)", test_endpoint_exists)

        def test_enumerate_endpoints():
            result = client.enumerate_endpoints()
            assert result and len(result.get("Data", [])) > 0
        run_test("Enumerate Endpoints", test_enumerate_endpoints)

        # Completion Endpoint CRUD
        def test_create_completion_endpoint():
            nonlocal test_cep_id
            cep = client.create_completion_endpoint({"TenantId": test_tenant_id, "Name": "Test Inference", "Model": "test-model", "Endpoint": "http://localhost:11434", "ApiFormat": "Ollama"})
            assert cep and "Id" in cep
            test_cep_id = cep["Id"]
        run_test("Create Completion Endpoint", test_create_completion_endpoint)

        def test_read_completion_endpoint():
            cep = client.get_completion_endpoint(test_cep_id)
            assert cep and cep["Model"] == "test-model"
        run_test("Read Completion Endpoint", test_read_completion_endpoint)

        def test_update_completion_endpoint():
            updated = client.update_completion_endpoint(test_cep_id, {"TenantId": test_tenant_id, "Name": "Updated Inference", "Model": "test-model-updated", "Endpoint": "http://localhost:11434", "ApiFormat": "Ollama"})
            assert updated is not None
        run_test("Update Completion Endpoint", test_update_completion_endpoint)

        def test_completion_endpoint_exists():
            assert client.completion_endpoint_exists(test_cep_id)
        run_test("Completion Endpoint Exists (HEAD)", test_completion_endpoint_exists)

        def test_enumerate_completion_endpoints():
            result = client.enumerate_completion_endpoints()
            assert result and len(result.get("Data", [])) > 0
        run_test("Enumerate Completion Endpoints", test_enumerate_completion_endpoints)

        # Request History
        def test_enumerate_history():
            result = client.enumerate_request_history()
            assert result is not None
        run_test("Enumerate Request History", test_enumerate_history)

        # Process Single Cell (requires an active embedding endpoint)
        def test_process_single_cell():
            eps = client.enumerate_endpoints()
            active_ep = None
            if eps and "Data" in eps:
                for ep in eps["Data"]:
                    if ep.get("Active", True) is not False:
                        active_ep = ep
                        break
            if not active_ep:
                raise Exception("SKIP: no active embedding endpoint")

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
            eps = client.enumerate_endpoints()
            active_ep = None
            if eps and "Data" in eps:
                for ep in eps["Data"]:
                    if ep.get("Active", True) is not False:
                        active_ep = ep
                        break
            if not active_ep:
                raise Exception("SKIP: no active embedding endpoint")

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
            eps = client.enumerate_endpoints()
            active_ep = None
            if eps and "Data" in eps:
                for ep in eps["Data"]:
                    if ep.get("Active", True) is not False:
                        active_ep = ep
                        break
            if not active_ep:
                raise Exception("SKIP: no active embedding endpoint")

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
            eps = client.enumerate_endpoints()
            active_ep = None
            if eps and "Data" in eps:
                for ep in eps["Data"]:
                    if ep.get("Active", True) is not False:
                        active_ep = ep
                        break
            if not active_ep:
                raise Exception("SKIP: no active embedding endpoint")

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
            eps = client.enumerate_endpoints()
            active_ep = None
            if eps and "Data" in eps:
                for ep in eps["Data"]:
                    if ep.get("Active", True) is not False:
                        active_ep = ep
                        break
            if not active_ep:
                raise Exception("SKIP: no active embedding endpoint")

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
            eps = client.enumerate_endpoints()
            active_ep = None
            if eps and "Data" in eps:
                for ep in eps["Data"]:
                    if ep.get("Active", True) is not False:
                        active_ep = ep
                        break
            if not active_ep:
                raise Exception("SKIP: no active embedding endpoint")

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
            eps = client.enumerate_endpoints()
            active_ep = None
            if eps and "Data" in eps:
                for ep in eps["Data"]:
                    if ep.get("Active", True) is not False:
                        active_ep = ep
                        break
            if not active_ep:
                raise Exception("SKIP: no active embedding endpoint")

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
            eps = client.enumerate_endpoints()
            active_ep = None
            if eps and "Data" in eps:
                for ep in eps["Data"]:
                    if ep.get("Active", True) is not False:
                        active_ep = ep
                        break
            if not active_ep:
                raise Exception("SKIP: no active embedding endpoint")

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
            eps = client.enumerate_endpoints()
            active_ep = None
            if eps and "Data" in eps:
                for ep in eps["Data"]:
                    if ep.get("Active", True) is not False:
                        active_ep = ep
                        break
            if not active_ep:
                raise Exception("SKIP: no active embedding endpoint")

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

        def test_delete_endpoint():
            client.delete_endpoint(test_ep_id)
            assert not client.endpoint_exists(test_ep_id)
        run_test("Delete Endpoint", test_delete_endpoint)

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
    print(f"Total: {passed + failed}  Passed: {passed}  Failed: {failed}")
    print(f"Runtime: {total_ms}ms")
    print(f"Result: {'PASS' if failed == 0 else 'FAIL'}")

    if failed_tests:
        print()
        print("Failed tests:")
        for name in failed_tests:
            print(f"  - {name}")

    print("================")
    sys.exit(0 if failed == 0 else 1)


if __name__ == "__main__":
    main()
