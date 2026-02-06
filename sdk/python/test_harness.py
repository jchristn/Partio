"""Partio Python SDK Test Harness."""

import sys
import time
from partio_sdk import PartioClient, PartioError


def main():
    endpoint = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:8000"
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

        def test_enumerate_endpoints():
            result = client.enumerate_endpoints()
            assert result and len(result.get("Data", [])) > 0
        run_test("Enumerate Endpoints", test_enumerate_endpoints)

        # Request History
        def test_enumerate_history():
            result = client.enumerate_request_history()
            assert result is not None
        run_test("Enumerate Request History", test_enumerate_history)

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
