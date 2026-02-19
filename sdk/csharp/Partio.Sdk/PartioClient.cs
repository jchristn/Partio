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
        /// <param name="endpoint">Base URL of the Partio server (e.g. http://localhost:8000).</param>
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
