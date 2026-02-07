"""Partio SDK for Python."""

import requests


class PartioError(Exception):
    """Exception raised when a Partio API call fails."""

    def __init__(self, message, status_code=None, response=None):
        super().__init__(message)
        self.status_code = status_code
        self.response = response


class PartioClient:
    """Client for the Partio REST API."""

    def __init__(self, endpoint, access_key):
        self.endpoint = endpoint.rstrip("/")
        self.access_key = access_key
        self.session = requests.Session()
        self.session.headers.update({
            "Authorization": f"Bearer {access_key}",
            "Content-Type": "application/json",
        })

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()

    def close(self):
        self.session.close()

    def _request(self, method, path, json_data=None):
        url = f"{self.endpoint}{path}"
        response = self.session.request(method, url, json=json_data)

        if response.status_code == 204:
            return None

        if not response.ok:
            error_data = None
            try:
                error_data = response.json()
            except Exception:
                pass
            message = error_data.get("Message", f"HTTP {response.status_code}") if error_data else f"HTTP {response.status_code}"
            raise PartioError(message, response.status_code, error_data)

        text = response.text
        if not text:
            return None
        return response.json()

    # Health
    def health(self):
        return self._request("GET", "/v1.0/health")

    def whoami(self):
        return self._request("GET", "/v1.0/whoami")

    # Process
    def process(self, endpoint_id, request):
        return self._request("POST", f"/v1.0/endpoints/{endpoint_id}/process", request)

    def process_batch(self, endpoint_id, requests_list):
        return self._request("POST", f"/v1.0/endpoints/{endpoint_id}/process/batch", requests_list)

    # Tenants
    def create_tenant(self, data):
        return self._request("PUT", "/v1.0/tenants", data)

    def get_tenant(self, tenant_id):
        return self._request("GET", f"/v1.0/tenants/{tenant_id}")

    def update_tenant(self, tenant_id, data):
        return self._request("PUT", f"/v1.0/tenants/{tenant_id}", data)

    def delete_tenant(self, tenant_id):
        return self._request("DELETE", f"/v1.0/tenants/{tenant_id}")

    def tenant_exists(self, tenant_id):
        response = self.session.head(f"{self.endpoint}/v1.0/tenants/{tenant_id}")
        return response.status_code == 200

    def enumerate_tenants(self, req=None):
        return self._request("POST", "/v1.0/tenants/enumerate", req or {})

    # Users
    def create_user(self, data):
        return self._request("PUT", "/v1.0/users", data)

    def get_user(self, user_id):
        return self._request("GET", f"/v1.0/users/{user_id}")

    def update_user(self, user_id, data):
        return self._request("PUT", f"/v1.0/users/{user_id}", data)

    def delete_user(self, user_id):
        return self._request("DELETE", f"/v1.0/users/{user_id}")

    def user_exists(self, user_id):
        response = self.session.head(f"{self.endpoint}/v1.0/users/{user_id}")
        return response.status_code == 200

    def enumerate_users(self, req=None):
        return self._request("POST", "/v1.0/users/enumerate", req or {})

    # Credentials
    def create_credential(self, data):
        return self._request("PUT", "/v1.0/credentials", data)

    def get_credential(self, credential_id):
        return self._request("GET", f"/v1.0/credentials/{credential_id}")

    def update_credential(self, credential_id, data):
        return self._request("PUT", f"/v1.0/credentials/{credential_id}", data)

    def delete_credential(self, credential_id):
        return self._request("DELETE", f"/v1.0/credentials/{credential_id}")

    def credential_exists(self, credential_id):
        response = self.session.head(f"{self.endpoint}/v1.0/credentials/{credential_id}")
        return response.status_code == 200

    def enumerate_credentials(self, req=None):
        return self._request("POST", "/v1.0/credentials/enumerate", req or {})

    # Embedding Endpoints
    def create_endpoint(self, data):
        return self._request("PUT", "/v1.0/endpoints", data)

    def get_endpoint(self, endpoint_id):
        return self._request("GET", f"/v1.0/endpoints/{endpoint_id}")

    def update_endpoint(self, endpoint_id, data):
        return self._request("PUT", f"/v1.0/endpoints/{endpoint_id}", data)

    def delete_endpoint(self, endpoint_id):
        return self._request("DELETE", f"/v1.0/endpoints/{endpoint_id}")

    def endpoint_exists(self, endpoint_id):
        response = self.session.head(f"{self.endpoint}/v1.0/endpoints/{endpoint_id}")
        return response.status_code == 200

    def enumerate_endpoints(self, req=None):
        return self._request("POST", "/v1.0/endpoints/enumerate", req or {})

    # Request History
    def get_request_history(self, entry_id):
        return self._request("GET", f"/v1.0/requests/{entry_id}")

    def get_request_history_detail(self, entry_id):
        return self._request("GET", f"/v1.0/requests/{entry_id}/detail")

    def delete_request_history(self, entry_id):
        return self._request("DELETE", f"/v1.0/requests/{entry_id}")

    def enumerate_request_history(self, req=None):
        return self._request("POST", "/v1.0/requests/enumerate", req or {})
