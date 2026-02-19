/**
 * Partio SDK for JavaScript/Node.js.
 * Uses native fetch (no external dependencies).
 */

export class PartioError extends Error {
  constructor(message, statusCode, response) {
    super(message);
    this.name = 'PartioError';
    this.statusCode = statusCode;
    this.response = response;
  }
}

export class PartioClient {
  constructor(endpoint, accessKey) {
    this.endpoint = endpoint.replace(/\/+$/, '');
    this.accessKey = accessKey;
  }

  async _request(method, path, body = null) {
    const options = {
      method,
      headers: {
        'Authorization': `Bearer ${this.accessKey}`,
        'Content-Type': 'application/json',
      },
    };
    if (body !== null) {
      options.body = JSON.stringify(body);
    }

    const response = await fetch(`${this.endpoint}${path}`, options);

    if (response.status === 204) return null;

    if (!response.ok) {
      let errorData = null;
      try { errorData = await response.json(); } catch {}
      const message = errorData?.Message || `HTTP ${response.status}`;
      throw new PartioError(message, response.status, errorData);
    }

    const text = await response.text();
    if (!text) return null;
    return JSON.parse(text);
  }

  // Health
  async health() { return this._request('GET', '/v1.0/health'); }
  async whoami() { return this._request('GET', '/v1.0/whoami'); }

  // Process
  async process(request) { return this._request('POST', '/v1.0/process', request); }
  async processBatch(requests) { return this._request('POST', '/v1.0/process/batch', requests); }

  // Tenants
  async createTenant(data) { return this._request('PUT', '/v1.0/tenants', data); }
  async getTenant(id) { return this._request('GET', `/v1.0/tenants/${id}`); }
  async updateTenant(id, data) { return this._request('PUT', `/v1.0/tenants/${id}`, data); }
  async deleteTenant(id) { return this._request('DELETE', `/v1.0/tenants/${id}`); }
  async tenantExists(id) {
    try {
      const res = await fetch(`${this.endpoint}/v1.0/tenants/${id}`, {
        method: 'HEAD',
        headers: { 'Authorization': `Bearer ${this.accessKey}` }
      });
      return res.ok;
    } catch { return false; }
  }
  async enumerateTenants(req = {}) { return this._request('POST', '/v1.0/tenants/enumerate', req); }

  // Users
  async createUser(data) { return this._request('PUT', '/v1.0/users', data); }
  async getUser(id) { return this._request('GET', `/v1.0/users/${id}`); }
  async updateUser(id, data) { return this._request('PUT', `/v1.0/users/${id}`, data); }
  async deleteUser(id) { return this._request('DELETE', `/v1.0/users/${id}`); }
  async userExists(id) {
    try {
      const res = await fetch(`${this.endpoint}/v1.0/users/${id}`, {
        method: 'HEAD',
        headers: { 'Authorization': `Bearer ${this.accessKey}` }
      });
      return res.ok;
    } catch { return false; }
  }
  async enumerateUsers(req = {}) { return this._request('POST', '/v1.0/users/enumerate', req); }

  // Credentials
  async createCredential(data) { return this._request('PUT', '/v1.0/credentials', data); }
  async getCredential(id) { return this._request('GET', `/v1.0/credentials/${id}`); }
  async updateCredential(id, data) { return this._request('PUT', `/v1.0/credentials/${id}`, data); }
  async deleteCredential(id) { return this._request('DELETE', `/v1.0/credentials/${id}`); }
  async credentialExists(id) {
    try {
      const res = await fetch(`${this.endpoint}/v1.0/credentials/${id}`, {
        method: 'HEAD',
        headers: { 'Authorization': `Bearer ${this.accessKey}` }
      });
      return res.ok;
    } catch { return false; }
  }
  async enumerateCredentials(req = {}) { return this._request('POST', '/v1.0/credentials/enumerate', req); }

  // Embedding Endpoints
  async createEndpoint(data) { return this._request('PUT', '/v1.0/endpoints/embedding', data); }
  async getEndpoint(id) { return this._request('GET', `/v1.0/endpoints/embedding/${id}`); }
  async updateEndpoint(id, data) { return this._request('PUT', `/v1.0/endpoints/embedding/${id}`, data); }
  async deleteEndpoint(id) { return this._request('DELETE', `/v1.0/endpoints/embedding/${id}`); }
  async endpointExists(id) {
    try {
      const res = await fetch(`${this.endpoint}/v1.0/endpoints/embedding/${id}`, {
        method: 'HEAD',
        headers: { 'Authorization': `Bearer ${this.accessKey}` }
      });
      return res.ok;
    } catch { return false; }
  }
  async enumerateEndpoints(req = {}) { return this._request('POST', '/v1.0/endpoints/embedding/enumerate', req); }

  // Embedding Endpoint Health
  async getEndpointHealth(id) { return this._request('GET', `/v1.0/endpoints/embedding/${id}/health`); }
  async getAllEndpointHealth() { return this._request('GET', '/v1.0/endpoints/embedding/health'); }

  // Completion Endpoints
  async createCompletionEndpoint(data) { return this._request('PUT', '/v1.0/endpoints/completion', data); }
  async getCompletionEndpoint(id) { return this._request('GET', `/v1.0/endpoints/completion/${id}`); }
  async updateCompletionEndpoint(id, data) { return this._request('PUT', `/v1.0/endpoints/completion/${id}`, data); }
  async deleteCompletionEndpoint(id) { return this._request('DELETE', `/v1.0/endpoints/completion/${id}`); }
  async completionEndpointExists(id) {
    try {
      const res = await fetch(`${this.endpoint}/v1.0/endpoints/completion/${id}`, {
        method: 'HEAD',
        headers: { 'Authorization': `Bearer ${this.accessKey}` }
      });
      return res.ok;
    } catch { return false; }
  }
  async enumerateCompletionEndpoints(req = {}) { return this._request('POST', '/v1.0/endpoints/completion/enumerate', req); }

  // Completion Endpoint Health
  async getCompletionEndpointHealth(id) { return this._request('GET', `/v1.0/endpoints/completion/${id}/health`); }
  async getAllCompletionEndpointHealth() { return this._request('GET', '/v1.0/endpoints/completion/health'); }

  // Request History
  async getRequestHistory(id) { return this._request('GET', `/v1.0/requests/${id}`); }
  async getRequestHistoryDetail(id) { return this._request('GET', `/v1.0/requests/${id}/detail`); }
  async deleteRequestHistory(id) { return this._request('DELETE', `/v1.0/requests/${id}`); }
  async enumerateRequestHistory(req = {}) { return this._request('POST', '/v1.0/requests/enumerate', req); }
}
