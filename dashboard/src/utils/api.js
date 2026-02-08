export class PartioApi {
  constructor(serverUrl, bearerToken) {
    this.serverUrl = serverUrl;
    this.bearerToken = bearerToken;
  }

  async request(method, path, body = null) {
    const options = {
      method,
      headers: {
        'Authorization': `Bearer ${this.bearerToken}`,
        'Content-Type': 'application/json'
      }
    };
    if (body !== null) {
      options.body = JSON.stringify(body);
    }
    const response = await fetch(`${this.serverUrl}${path}`, options);
    if (response.status === 204) return null;
    if (!response.ok) {
      let errorData;
      try { errorData = await response.json(); } catch { errorData = null; }
      const err = new Error(errorData?.Message || `HTTP ${response.status}`);
      err.statusCode = response.status;
      err.response = errorData;
      throw err;
    }
    const text = await response.text();
    if (!text) return null;
    return JSON.parse(text);
  }

  // Health
  health() { return this.request('GET', '/v1.0/health'); }
  whoami() { return this.request('GET', '/v1.0/whoami'); }

  // Tenants
  createTenant(data) { return this.request('PUT', '/v1.0/tenants', data); }
  getTenant(id) { return this.request('GET', `/v1.0/tenants/${id}`); }
  updateTenant(id, data) { return this.request('PUT', `/v1.0/tenants/${id}`, data); }
  deleteTenant(id) { return this.request('DELETE', `/v1.0/tenants/${id}`); }
  enumerateTenants(req = {}) { return this.request('POST', '/v1.0/tenants/enumerate', req); }

  // Users
  createUser(data) { return this.request('PUT', '/v1.0/users', data); }
  getUser(id) { return this.request('GET', `/v1.0/users/${id}`); }
  updateUser(id, data) { return this.request('PUT', `/v1.0/users/${id}`, data); }
  deleteUser(id) { return this.request('DELETE', `/v1.0/users/${id}`); }
  enumerateUsers(req = {}) { return this.request('POST', '/v1.0/users/enumerate', req); }

  // Credentials
  createCredential(data) { return this.request('PUT', '/v1.0/credentials', data); }
  getCredential(id) { return this.request('GET', `/v1.0/credentials/${id}`); }
  updateCredential(id, data) { return this.request('PUT', `/v1.0/credentials/${id}`, data); }
  deleteCredential(id) { return this.request('DELETE', `/v1.0/credentials/${id}`); }
  enumerateCredentials(req = {}) { return this.request('POST', '/v1.0/credentials/enumerate', req); }

  // Embedding Endpoints
  createEndpoint(data) { return this.request('PUT', '/v1.0/endpoints', data); }
  getEndpoint(id) { return this.request('GET', `/v1.0/endpoints/${id}`); }
  updateEndpoint(id, data) { return this.request('PUT', `/v1.0/endpoints/${id}`, data); }
  deleteEndpoint(id) { return this.request('DELETE', `/v1.0/endpoints/${id}`); }
  enumerateEndpoints(req = {}) { return this.request('POST', '/v1.0/endpoints/enumerate', req); }

  // Endpoint Health
  getEndpointHealth(id) { return this.request('GET', `/v1.0/endpoints/${id}/health`); }
  getAllEndpointHealth() { return this.request('GET', '/v1.0/endpoints/health'); }

  // Request History
  getRequestHistory(id) { return this.request('GET', `/v1.0/requests/${id}`); }
  getRequestHistoryDetail(id) { return this.request('GET', `/v1.0/requests/${id}/detail`); }
  deleteRequestHistory(id) { return this.request('DELETE', `/v1.0/requests/${id}`); }
  enumerateRequestHistory(req = {}) { return this.request('POST', '/v1.0/requests/enumerate', req); }

  // Process
  process(endpointId, data) { return this.request('POST', `/v1.0/endpoints/${endpointId}/process`, data); }
  processBatch(endpointId, data) { return this.request('POST', `/v1.0/endpoints/${endpointId}/process/batch`, data); }
}
