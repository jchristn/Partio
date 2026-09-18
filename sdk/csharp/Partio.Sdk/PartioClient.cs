namespace Partio.Sdk
{
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using Partio.Sdk.Models;

    /// <summary>
    /// Client for the Partio REST API.
    /// </summary>
    public class PartioClient : IDisposable
    {
        private readonly HttpClient _HttpClient;
        private readonly string _Endpoint;
        private readonly JsonSerializerOptions _JsonOptions;

        /// <summary>
        /// Initialize a new PartioClient.
        /// </summary>
        /// <param name="endpoint">Base URL of the Partio server (e.g. http://localhost:8400).</param>
        /// <param name="accessKey">Bearer token or admin API key.</param>
        public PartioClient(string endpoint, string accessKey)
        {
            _Endpoint = endpoint.TrimEnd('/');
            _HttpClient = new HttpClient();
            _HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessKey);
            _JsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <summary>Dispose the HTTP client.</summary>
        public void Dispose()
        {
            _HttpClient.Dispose();
        }

        private async Task<T?> MakeRequestAsync<T>(HttpMethod method, string path, object? data = null)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, _Endpoint + path);

            if (data != null)
            {
                string json = JsonSerializer.Serialize(data, _JsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response = await _HttpClient.SendAsync(request).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return default;

            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                ApiErrorResponse? errorResponse = null;
                try { errorResponse = JsonSerializer.Deserialize<ApiErrorResponse>(responseBody, _JsonOptions); } catch { }
                throw new PartioException(
                    errorResponse?.Message ?? $"HTTP {(int)response.StatusCode}",
                    (int)response.StatusCode,
                    errorResponse);
            }

            if (string.IsNullOrEmpty(responseBody))
                return default;

            return JsonSerializer.Deserialize<T>(responseBody, _JsonOptions);
        }

        // Health
        public Task<Dictionary<string, string>?> HealthAsync() =>
            MakeRequestAsync<Dictionary<string, string>>(HttpMethod.Get, "/v1.0/health");

        public Task<WhoAmIResponse?> WhoAmIAsync() =>
            MakeRequestAsync<WhoAmIResponse>(HttpMethod.Get, "/v1.0/whoami");

        // Process
        public Task<SemanticCellResponse?> ProcessAsync(SemanticCellRequest request) =>
            MakeRequestAsync<SemanticCellResponse>(HttpMethod.Post, "/v1.0/process", request);

        public Task<List<SemanticCellResponse>?> ProcessBatchAsync(List<SemanticCellRequest> requests) =>
            MakeRequestAsync<List<SemanticCellResponse>>(HttpMethod.Post, "/v1.0/process/batch", requests);

        // Chunk & Embed
        public Task<ChunkResponse?> ChunkAsync(ChunkRequest request) =>
            MakeRequestAsync<ChunkResponse>(HttpMethod.Post, "/v1.0/chunk", request);

        public Task<EmbedResponse?> EmbedAsync(EmbedRequest request) =>
            MakeRequestAsync<EmbedResponse>(HttpMethod.Post, "/v1.0/embed", request);

        public Task<SummarizeResponse?> SummarizeAsync(SummarizeRequest request) =>
            MakeRequestAsync<SummarizeResponse>(HttpMethod.Post, "/v1.0/summarize", request);

        public Task<CompletionResponse?> CompleteAsync(CompletionRequest request) =>
            MakeRequestAsync<CompletionResponse>(HttpMethod.Post, "/v1.0/completion", request);

        // Proxy (transparent passthrough)

        /// <summary>
        /// Send a native provider request through the transparent completion proxy. Partio injects the
        /// upstream API key, enforces per-endpoint concurrency/timeout, and relays the provider's response
        /// verbatim (status code and body). The <paramref name="subpath"/> must be the endpoint dialect's
        /// native path — for example <c>v1/chat/completions</c> for an OpenAI endpoint or <c>api/chat</c>
        /// for an Ollama endpoint. A non-2xx upstream status is returned in <see cref="ProxyResponse"/>
        /// rather than raised. A Partio-level failure (unknown endpoint, disallowed path, upstream
        /// unreachable) is returned as a Partio JSON error body with the corresponding status code.
        /// </summary>
        /// <param name="endpointId">Target completion endpoint ID.</param>
        /// <param name="subpath">Native provider sub-path (for example <c>v1/chat/completions</c>).</param>
        /// <param name="method">HTTP method (defaults to POST).</param>
        /// <param name="body">Raw request body, sent verbatim; null for a bodyless request.</param>
        /// <param name="contentType">Content type for the body.</param>
        /// <returns>The relayed upstream response.</returns>
        public async Task<ProxyResponse> ProxyAsync(string endpointId, string subpath, HttpMethod? method = null, string? body = null, string contentType = "application/json")
        {
            HttpMethod httpMethod = method ?? HttpMethod.Post;
            string path = "/v1.0/proxy/" + endpointId.Trim('/') + "/" + subpath.TrimStart('/');
            HttpRequestMessage request = new HttpRequestMessage(httpMethod, _Endpoint + path);

            if (body != null && httpMethod != HttpMethod.Get && httpMethod != HttpMethod.Head)
                request.Content = new StringContent(body, Encoding.UTF8, contentType);

            HttpResponseMessage response = await _HttpClient.SendAsync(request).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            ProxyResponse result = new ProxyResponse
            {
                StatusCode = (int)response.StatusCode,
                ContentType = response.Content.Headers.ContentType?.ToString(),
                Body = responseBody
            };
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
                result.Headers[header.Key] = string.Join(", ", header.Value);
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
                result.Headers[header.Key] = string.Join(", ", header.Value);
            return result;
        }

        /// <summary>Convenience: POST a native provider request body through the proxy.</summary>
        /// <param name="endpointId">Target completion endpoint ID.</param>
        /// <param name="subpath">Native provider sub-path.</param>
        /// <param name="body">Raw request body, sent verbatim.</param>
        /// <param name="contentType">Content type for the body.</param>
        /// <returns>The relayed upstream response.</returns>
        public Task<ProxyResponse> ProxyPostAsync(string endpointId, string subpath, string body, string contentType = "application/json") =>
            ProxyAsync(endpointId, subpath, HttpMethod.Post, body, contentType);

        /// <summary>Convenience: GET a native provider sub-path (for example model discovery) through the proxy.</summary>
        /// <param name="endpointId">Target completion endpoint ID.</param>
        /// <param name="subpath">Native provider sub-path.</param>
        /// <returns>The relayed upstream response.</returns>
        public Task<ProxyResponse> ProxyGetAsync(string endpointId, string subpath) =>
            ProxyAsync(endpointId, subpath, HttpMethod.Get);

        // Explorer
        public Task<EndpointExplorerEmbeddingResponse?> ExploreEmbeddingEndpointAsync(EndpointExplorerEmbeddingRequest request) =>
            MakeRequestAsync<EndpointExplorerEmbeddingResponse>(HttpMethod.Post, "/v1.0/explorer/embedding", request);

        public Task<EndpointExplorerCompletionResponse?> ExploreCompletionEndpointAsync(EndpointExplorerCompletionRequest request) =>
            MakeRequestAsync<EndpointExplorerCompletionResponse>(HttpMethod.Post, "/v1.0/explorer/completion", request);

        // Tenants
        public Task<TenantMetadata?> CreateTenantAsync(TenantMetadata tenant) =>
            MakeRequestAsync<TenantMetadata>(HttpMethod.Put, "/v1.0/tenants", tenant);

        public Task<TenantMetadata?> GetTenantAsync(string id) =>
            MakeRequestAsync<TenantMetadata>(HttpMethod.Get, $"/v1.0/tenants/{id}");

        public Task<TenantMetadata?> UpdateTenantAsync(string id, TenantMetadata tenant) =>
            MakeRequestAsync<TenantMetadata>(HttpMethod.Put, $"/v1.0/tenants/{id}", tenant);

        public async Task DeleteTenantAsync(string id) =>
            await MakeRequestAsync<object>(HttpMethod.Delete, $"/v1.0/tenants/{id}").ConfigureAwait(false);

        public async Task<bool> TenantExistsAsync(string id)
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, _Endpoint + $"/v1.0/tenants/{id}");
                request.Headers.Authorization = _HttpClient.DefaultRequestHeaders.Authorization;
                HttpResponseMessage response = await _HttpClient.SendAsync(request).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public Task<EnumerationResult<TenantMetadata>?> EnumerateTenantsAsync(EnumerationRequest? req = null) =>
            MakeRequestAsync<EnumerationResult<TenantMetadata>>(HttpMethod.Post, "/v1.0/tenants/enumerate", req ?? new EnumerationRequest());

        // Users
        public Task<UserMaster?> CreateUserAsync(UserMaster user) =>
            MakeRequestAsync<UserMaster>(HttpMethod.Put, "/v1.0/users", user);

        public Task<UserMaster?> GetUserAsync(string id) =>
            MakeRequestAsync<UserMaster>(HttpMethod.Get, $"/v1.0/users/{id}");

        public Task<UserMaster?> UpdateUserAsync(string id, UserMaster user) =>
            MakeRequestAsync<UserMaster>(HttpMethod.Put, $"/v1.0/users/{id}", user);

        public async Task DeleteUserAsync(string id) =>
            await MakeRequestAsync<object>(HttpMethod.Delete, $"/v1.0/users/{id}").ConfigureAwait(false);

        public async Task<bool> UserExistsAsync(string id)
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, _Endpoint + $"/v1.0/users/{id}");
                request.Headers.Authorization = _HttpClient.DefaultRequestHeaders.Authorization;
                HttpResponseMessage response = await _HttpClient.SendAsync(request).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public Task<EnumerationResult<UserMaster>?> EnumerateUsersAsync(EnumerationRequest? req = null) =>
            MakeRequestAsync<EnumerationResult<UserMaster>>(HttpMethod.Post, "/v1.0/users/enumerate", req ?? new EnumerationRequest());

        // Credentials
        public Task<Credential?> CreateCredentialAsync(Credential credential) =>
            MakeRequestAsync<Credential>(HttpMethod.Put, "/v1.0/credentials", credential);

        public Task<Credential?> GetCredentialAsync(string id) =>
            MakeRequestAsync<Credential>(HttpMethod.Get, $"/v1.0/credentials/{id}");

        public Task<Credential?> UpdateCredentialAsync(string id, Credential credential) =>
            MakeRequestAsync<Credential>(HttpMethod.Put, $"/v1.0/credentials/{id}", credential);

        public async Task DeleteCredentialAsync(string id) =>
            await MakeRequestAsync<object>(HttpMethod.Delete, $"/v1.0/credentials/{id}").ConfigureAwait(false);

        public async Task<bool> CredentialExistsAsync(string id)
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, _Endpoint + $"/v1.0/credentials/{id}");
                request.Headers.Authorization = _HttpClient.DefaultRequestHeaders.Authorization;
                HttpResponseMessage response = await _HttpClient.SendAsync(request).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public Task<EnumerationResult<Credential>?> EnumerateCredentialsAsync(EnumerationRequest? req = null) =>
            MakeRequestAsync<EnumerationResult<Credential>>(HttpMethod.Post, "/v1.0/credentials/enumerate", req ?? new EnumerationRequest());

        // Embedding Endpoints
        public Task<EmbeddingEndpoint?> CreateEndpointAsync(EmbeddingEndpoint endpoint) =>
            MakeRequestAsync<EmbeddingEndpoint>(HttpMethod.Put, "/v1.0/endpoints/embedding", endpoint);

        public Task<EmbeddingEndpoint?> GetEndpointAsync(string id) =>
            MakeRequestAsync<EmbeddingEndpoint>(HttpMethod.Get, $"/v1.0/endpoints/embedding/{id}");

        public Task<EmbeddingEndpoint?> UpdateEndpointAsync(string id, EmbeddingEndpoint endpoint) =>
            MakeRequestAsync<EmbeddingEndpoint>(HttpMethod.Put, $"/v1.0/endpoints/embedding/{id}", endpoint);

        public async Task DeleteEndpointAsync(string id) =>
            await MakeRequestAsync<object>(HttpMethod.Delete, $"/v1.0/endpoints/embedding/{id}").ConfigureAwait(false);

        public async Task<bool> EndpointExistsAsync(string id)
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, _Endpoint + $"/v1.0/endpoints/embedding/{id}");
                request.Headers.Authorization = _HttpClient.DefaultRequestHeaders.Authorization;
                HttpResponseMessage response = await _HttpClient.SendAsync(request).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public Task<EnumerationResult<EmbeddingEndpoint>?> EnumerateEndpointsAsync(EnumerationRequest? req = null) =>
            MakeRequestAsync<EnumerationResult<EmbeddingEndpoint>>(HttpMethod.Post, "/v1.0/endpoints/embedding/enumerate", req ?? new EnumerationRequest());

        public Task<ModelLoadResponse?> LoadEndpointAsync(string id, ModelLoadRequest? request = null) =>
            MakeRequestAsync<ModelLoadResponse>(HttpMethod.Post, $"/v1.0/endpoints/embedding/{id}/load", request ?? new ModelLoadRequest());

        // Embedding Endpoint Health
        public Task<EndpointHealthStatus?> GetEndpointHealthAsync(string id) =>
            MakeRequestAsync<EndpointHealthStatus>(HttpMethod.Get, $"/v1.0/endpoints/embedding/{id}/health");

        public Task<List<EndpointHealthStatus>?> GetAllEndpointHealthAsync() =>
            MakeRequestAsync<List<EndpointHealthStatus>>(HttpMethod.Get, "/v1.0/endpoints/embedding/health");

        // Completion Endpoints
        public Task<CompletionEndpoint?> CreateCompletionEndpointAsync(CompletionEndpoint endpoint) =>
            MakeRequestAsync<CompletionEndpoint>(HttpMethod.Put, "/v1.0/endpoints/completion", endpoint);

        public Task<CompletionEndpoint?> GetCompletionEndpointAsync(string id) =>
            MakeRequestAsync<CompletionEndpoint>(HttpMethod.Get, $"/v1.0/endpoints/completion/{id}");

        public Task<CompletionEndpoint?> UpdateCompletionEndpointAsync(string id, CompletionEndpoint endpoint) =>
            MakeRequestAsync<CompletionEndpoint>(HttpMethod.Put, $"/v1.0/endpoints/completion/{id}", endpoint);

        public async Task DeleteCompletionEndpointAsync(string id) =>
            await MakeRequestAsync<object>(HttpMethod.Delete, $"/v1.0/endpoints/completion/{id}").ConfigureAwait(false);

        public async Task<bool> CompletionEndpointExistsAsync(string id)
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, _Endpoint + $"/v1.0/endpoints/completion/{id}");
                request.Headers.Authorization = _HttpClient.DefaultRequestHeaders.Authorization;
                HttpResponseMessage response = await _HttpClient.SendAsync(request).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public Task<EnumerationResult<CompletionEndpoint>?> EnumerateCompletionEndpointsAsync(EnumerationRequest? req = null) =>
            MakeRequestAsync<EnumerationResult<CompletionEndpoint>>(HttpMethod.Post, "/v1.0/endpoints/completion/enumerate", req ?? new EnumerationRequest());

        public Task<ModelLoadResponse?> LoadCompletionEndpointAsync(string id, ModelLoadRequest? request = null) =>
            MakeRequestAsync<ModelLoadResponse>(HttpMethod.Post, $"/v1.0/endpoints/completion/{id}/load", request ?? new ModelLoadRequest());

        // Completion Endpoint Health
        public Task<EndpointHealthStatus?> GetCompletionEndpointHealthAsync(string id) =>
            MakeRequestAsync<EndpointHealthStatus>(HttpMethod.Get, $"/v1.0/endpoints/completion/{id}/health");

        public Task<List<EndpointHealthStatus>?> GetAllCompletionEndpointHealthAsync() =>
            MakeRequestAsync<List<EndpointHealthStatus>>(HttpMethod.Get, "/v1.0/endpoints/completion/health");

        // Request History
        public Task<RequestHistoryEntry?> GetRequestHistoryAsync(string id) =>
            MakeRequestAsync<RequestHistoryEntry>(HttpMethod.Get, $"/v1.0/requests/{id}");

        public Task<RequestHistoryDetail?> GetRequestHistoryDetailAsync(string id) =>
            MakeRequestAsync<RequestHistoryDetail>(HttpMethod.Get, $"/v1.0/requests/{id}/detail");

        public async Task DeleteRequestHistoryAsync(string id) =>
            await MakeRequestAsync<object>(HttpMethod.Delete, $"/v1.0/requests/{id}").ConfigureAwait(false);

        public Task<EnumerationResult<RequestHistoryEntry>?> EnumerateRequestHistoryAsync(EnumerationRequest? req = null) =>
            MakeRequestAsync<EnumerationResult<RequestHistoryEntry>>(HttpMethod.Post, "/v1.0/requests/enumerate", req ?? new EnumerationRequest());
    }
}
