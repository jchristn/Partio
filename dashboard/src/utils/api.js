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
  createEndpoint(data) { return this.request('PUT', '/v1.0/endpoints/embedding', data); }
  getEndpoint(id) { return this.request('GET', `/v1.0/endpoints/embedding/${id}`); }
  updateEndpoint(id, data) { return this.request('PUT', `/v1.0/endpoints/embedding/${id}`, data); }
  deleteEndpoint(id) { return this.request('DELETE', `/v1.0/endpoints/embedding/${id}`); }
  enumerateEndpoints(req = {}) { return this.request('POST', '/v1.0/endpoints/embedding/enumerate', req); }
  loadEndpoint(id, request = {}) { return this.request('POST', `/v1.0/endpoints/embedding/${id}/load`, request); }

  // Embedding Endpoint Health
  getEndpointHealth(id) { return this.request('GET', `/v1.0/endpoints/embedding/${id}/health`); }
  getAllEndpointHealth() { return this.request('GET', '/v1.0/endpoints/embedding/health'); }

  // Completion Endpoints
  createCompletionEndpoint(data) { return this.request('PUT', '/v1.0/endpoints/completion', data); }
  getCompletionEndpoint(id) { return this.request('GET', `/v1.0/endpoints/completion/${id}`); }
  updateCompletionEndpoint(id, data) { return this.request('PUT', `/v1.0/endpoints/completion/${id}`, data); }
  deleteCompletionEndpoint(id) { return this.request('DELETE', `/v1.0/endpoints/completion/${id}`); }
  enumerateCompletionEndpoints(req = {}) { return this.request('POST', '/v1.0/endpoints/completion/enumerate', req); }
  loadCompletionEndpoint(id, request = {}) { return this.request('POST', `/v1.0/endpoints/completion/${id}/load`, request); }

  // Completion Endpoint Health
  getCompletionEndpointHealth(id) { return this.request('GET', `/v1.0/endpoints/completion/${id}/health`); }
  getAllCompletionEndpointHealth() { return this.request('GET', '/v1.0/endpoints/completion/health'); }

  // Request History
  getRequestHistory(id) { return this.request('GET', `/v1.0/requests/${id}`); }
  getRequestHistoryDetail(id) { return this.request('GET', `/v1.0/requests/${id}/detail`); }
  deleteRequestHistory(id) { return this.request('DELETE', `/v1.0/requests/${id}`); }
  enumerateRequestHistory(req = {}) { return this.request('POST', '/v1.0/requests/enumerate', req); }
  getRequestStatistics(req = {}) { return this.request('POST', '/v1.0/requests/statistics', req); }

  // Process
  process(data) { return this.request('POST', '/v1.0/process', data); }
  processBatch(data) { return this.request('POST', '/v1.0/process/batch', data); }
  chunk(data) { return this.request('POST', '/v1.0/chunk', data); }
  embed(data) { return this.request('POST', '/v1.0/embed', data); }

  // Explorer
  exploreEmbeddingEndpoint(data) { return this.request('POST', '/v1.0/explorer/embedding', data); }
  exploreCompletionEndpoint(data) { return this.request('POST', '/v1.0/explorer/completion', data); }

  // Proxy (transparent passthrough)
  // Relays a native provider request to the endpoint's upstream and returns the response verbatim.
  // A non-2xx upstream status is returned (not thrown) as { statusCode, headers, body }.
  async proxy(endpointId, subpath, { method = 'POST', body = null, contentType = 'application/json', signal } = {}) {
    const path = `/v1.0/proxy/${String(endpointId).replace(/^\/+|\/+$/g, '')}/${String(subpath).replace(/^\/+/, '')}`;
    const options = { method, headers: { 'Authorization': `Bearer ${this.bearerToken}` }, signal };
    if (body !== null && method !== 'GET' && method !== 'HEAD') {
      options.body = typeof body === 'string' ? body : JSON.stringify(body);
      options.headers['Content-Type'] = contentType;
    }
    const response = await fetch(`${this.serverUrl}${path}`, options);
    const headers = {};
    for (const [k, v] of response.headers.entries()) headers[k] = v;
    return { statusCode: response.status, headers, body: await response.text() };
  }

  // Streaming variant: relays the upstream response as it arrives, invoking onChunk(text) with each
  // decoded piece. Returns { statusCode }. On a non-2xx status it returns { statusCode, body } without
  // streaming so the caller can surface the error.
  async proxyStream(endpointId, subpath, { method = 'POST', body = null, contentType = 'application/json', onChunk, signal } = {}) {
    const path = `/v1.0/proxy/${String(endpointId).replace(/^\/+|\/+$/g, '')}/${String(subpath).replace(/^\/+/, '')}`;
    const options = { method, headers: { 'Authorization': `Bearer ${this.bearerToken}` }, signal };
    if (body !== null && method !== 'GET' && method !== 'HEAD') {
      options.body = typeof body === 'string' ? body : JSON.stringify(body);
      options.headers['Content-Type'] = contentType;
    }
    const response = await fetch(`${this.serverUrl}${path}`, options);
    if (!response.ok || !response.body) {
      return { statusCode: response.status, body: await response.text() };
    }
    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      if (value && onChunk) onChunk(decoder.decode(value, { stream: true }));
    }
    return { statusCode: response.status };
  }
}
