namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Partio.Sdk;
    using Partio.Sdk.Models;

    public static class SharedIntegrationTests
    {
        private const string DefaultOllamaEndpoint = "http://127.0.0.1:11434";
        private static string _Endpoint = "http://localhost:8400";
        private static string _AdminKey = "partioadmin";
        private static string _TestToken = "default";
        private static string _OllamaEndpoint = DefaultOllamaEndpoint;

        // Shared state across sequential tests
        private static string _TestTenantId = "";
        private static string _TestUserId = "";
        private static string _TestCredId = "";
        private static string _TestCredToken = "";
        private static string _TestEpId = "";
        private static string _GeminiEpId = "";
        private static string _VllmEpId = "";
        private static string _TestCepId = "";
        private static string _GeminiCepId = "";
        private static string _VllmCepId = "";

        public static void Configure(string endpoint, string adminKey, string testToken, string? ollamaEndpoint = null)
        {
            _Endpoint = endpoint;
            _AdminKey = adminKey;
            _TestToken = testToken;
            _OllamaEndpoint = string.IsNullOrWhiteSpace(ollamaEndpoint) ? DefaultOllamaEndpoint : ollamaEndpoint.TrimEnd('/');

            _TestTenantId = "";
            _TestUserId = "";
            _TestCredId = "";
            _TestCredToken = "";
            _TestEpId = "";
            _GeminiEpId = "";
            _VllmEpId = "";
            _TestCepId = "";
            _GeminiCepId = "";
            _VllmCepId = "";
        }

        // ===== Health =====

        public static async Task TestHealthCheckAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                Dictionary<string, string>? result = await admin.HealthAsync();
                if (result == null || !result.ContainsKey("Status")) throw new Exception("No health response");
                if (result["Status"] != "Healthy") throw new Exception("Not healthy: " + result["Status"]);
            }
        }

        // ===== Tenant CRUD =====

        public static async Task TestCreateTenantAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                TenantMetadata? tenant = await admin.CreateTenantAsync(new TenantMetadata
                {
                    Name = "Automated Test Tenant",
                    Labels = new List<string> { "automated", "test" },
                    Tags = new Dictionary<string, string> { { "env", "test" } }
                });
                if (tenant == null || string.IsNullOrEmpty(tenant.Id)) throw new Exception("No tenant returned");
                _TestTenantId = tenant.Id;
            }
        }

        public static async Task TestReadTenantAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                TenantMetadata? tenant = await admin.GetTenantAsync(_TestTenantId);
                if (tenant == null) throw new Exception("Tenant not found");
                if (tenant.Name != "Automated Test Tenant") throw new Exception("Name mismatch: " + tenant.Name);
            }
        }

        public static async Task TestUpdateTenantAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                TenantMetadata? updated = await admin.UpdateTenantAsync(_TestTenantId, new TenantMetadata
                {
                    Name = "Updated Test Tenant"
                });
                if (updated == null || updated.Name != "Updated Test Tenant") throw new Exception("Update failed");
            }
        }

        public static async Task TestTenantExistsAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                bool exists = await admin.TenantExistsAsync(_TestTenantId);
                if (!exists) throw new Exception("Tenant should exist");
            }
        }

        public static async Task TestEnumerateTenantsAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EnumerationResult<TenantMetadata>? result = await admin.EnumerateTenantsAsync(new EnumerationRequest { MaxResults = 10 });
                if (result == null || result.Data.Count == 0) throw new Exception("No tenants returned");
            }
        }

        // ===== User CRUD =====

        public static async Task TestCreateUserAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                UserMaster? userResult = await admin.CreateUserAsync(new UserMaster
                {
                    TenantId = _TestTenantId,
                    Email = "autotest@partio.test",
                    Password = "TestPassword123",
                    FirstName = "Auto",
                    LastName = "Tester",
                    IsAdmin = false
                });
                if (userResult == null || string.IsNullOrEmpty(userResult.Id)) throw new Exception("No user returned");
                _TestUserId = userResult.Id;
            }
        }

        public static async Task TestReadUserAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                UserMaster? userResult = await admin.GetUserAsync(_TestUserId);
                if (userResult == null) throw new Exception("User not found");
                if (userResult.Email != "autotest@partio.test") throw new Exception("Email mismatch");
            }
        }

        public static async Task TestUpdateUserAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                UserMaster? updated = await admin.UpdateUserAsync(_TestUserId, new UserMaster
                {
                    TenantId = _TestTenantId,
                    Email = "autotest-updated@partio.test",
                    FirstName = "Updated"
                });
                if (updated == null) throw new Exception("Update failed");
            }
        }

        public static async Task TestUserExistsAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                bool exists = await admin.UserExistsAsync(_TestUserId);
                if (!exists) throw new Exception("User should exist");
            }
        }

        public static async Task TestEnumerateUsersAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EnumerationResult<UserMaster>? result = await admin.EnumerateUsersAsync(new EnumerationRequest { MaxResults = 10 });
                if (result == null || result.Data.Count == 0) throw new Exception("No users returned");
            }
        }

        // ===== Credential CRUD =====

        public static async Task TestCreateCredentialAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                Credential? cred = await admin.CreateCredentialAsync(new Credential
                {
                    TenantId = _TestTenantId,
                    UserId = _TestUserId,
                    Name = "Automated Test Key"
                });
                if (cred == null || string.IsNullOrEmpty(cred.Id)) throw new Exception("No credential returned");
                _TestCredId = cred.Id;
                _TestCredToken = cred.BearerToken ?? string.Empty;
            }
        }

        public static async Task TestReadCredentialAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                Credential? cred = await admin.GetCredentialAsync(_TestCredId);
                if (cred == null) throw new Exception("Credential not found");
                if (cred.Name != "Automated Test Key") throw new Exception("Name mismatch");
            }
        }

        public static async Task TestUpdateCredentialAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                Credential? updated = await admin.UpdateCredentialAsync(_TestCredId, new Credential
                {
                    TenantId = _TestTenantId,
                    UserId = _TestUserId,
                    Name = "Updated Test Key"
                });
                if (updated == null) throw new Exception("Update failed");
            }
        }

        public static async Task TestCredentialExistsAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                bool exists = await admin.CredentialExistsAsync(_TestCredId);
                if (!exists) throw new Exception("Credential should exist");
            }
        }

        public static async Task TestEnumerateCredentialsAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EnumerationResult<Credential>? result = await admin.EnumerateCredentialsAsync(new EnumerationRequest { MaxResults = 10 });
                if (result == null || result.Data.Count == 0) throw new Exception("No credentials returned");
            }
        }

        public static async Task TestAuthenticateWithNewCredentialAsync()
        {
            using (PartioClient credClient = new PartioClient(_Endpoint, _TestCredToken))
            {
                Dictionary<string, string>? result = await credClient.HealthAsync();
                if (result == null || result["Status"] != "Healthy") throw new Exception("Health check failed with new cred");
            }
        }

        // ===== Embedding Endpoint CRUD =====

        public static async Task TestCreateEmbeddingEndpointAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EmbeddingEndpoint? ep = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                {
                    TenantId = _TestTenantId,
                    Model = "test-model",
                    Endpoint = _OllamaEndpoint,
                    ApiFormat = "Ollama",
                    HealthCheckEnabled = false,
                    MaximumTimeoutMs = 61000,
                    MaxConcurrentRequests = 3
                });
                if (ep == null || string.IsNullOrEmpty(ep.Id)) throw new Exception("No endpoint returned");
                if (ep.MaximumTimeoutMs != 61000) throw new Exception("MaximumTimeoutMs mismatch on create");
                if (ep.MaxConcurrentRequests != 3) throw new Exception("MaxConcurrentRequests mismatch on create");
                _TestEpId = ep.Id;
            }
        }

        public static async Task TestReadEmbeddingEndpointAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EmbeddingEndpoint? ep = await admin.GetEndpointAsync(_TestEpId);
                if (ep == null) throw new Exception("Endpoint not found");
                if (ep.Model != "test-model") throw new Exception("Model mismatch");
                if (ep.MaximumTimeoutMs != 61000) throw new Exception("MaximumTimeoutMs mismatch");
                if (ep.MaxConcurrentRequests != 3) throw new Exception("MaxConcurrentRequests mismatch");
            }
        }

        public static async Task TestUpdateEmbeddingEndpointAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EmbeddingEndpoint? updated = await admin.UpdateEndpointAsync(_TestEpId, new EmbeddingEndpoint
                {
                    TenantId = _TestTenantId,
                    Model = "test-model-v2",
                    Endpoint = _OllamaEndpoint,
                    ApiFormat = "Ollama",
                    HealthCheckEnabled = false,
                    MaximumTimeoutMs = 91000,
                    MaxConcurrentRequests = 5
                });
                if (updated == null) throw new Exception("Update failed");
                if (updated.MaximumTimeoutMs != 91000) throw new Exception("MaximumTimeoutMs mismatch on update");
                if (updated.MaxConcurrentRequests != 5) throw new Exception("MaxConcurrentRequests mismatch on update");
            }
        }

        public static async Task TestEndpointExistsAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                bool exists = await admin.EndpointExistsAsync(_TestEpId);
                if (!exists) throw new Exception("Endpoint should exist");
            }
        }

        public static async Task TestEnumerateEndpointsAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EnumerationResult<EmbeddingEndpoint>? result = await admin.EnumerateEndpointsAsync(new EnumerationRequest { MaxResults = 10 });
                if (result == null || result.Data.Count == 0) throw new Exception("No endpoints returned");
            }
        }

        public static async Task TestCreateGeminiEmbeddingEndpointAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EmbeddingEndpoint? ep = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Gemini Embedding",
                    Model = "gemini-embedding-001",
                    Endpoint = "https://generativelanguage.googleapis.com",
                    ApiFormat = "Gemini",
                    ApiKey = "test-api-key",
                    HealthCheckEnabled = false
                });
                if (ep == null || string.IsNullOrEmpty(ep.Id)) throw new Exception("No endpoint returned");
                _GeminiEpId = ep.Id;
            }
        }

        public static async Task TestCreateVllmEmbeddingEndpointAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EmbeddingEndpoint? ep = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "vLLM Embedding",
                    Model = "intfloat/e5-small-v2",
                    Endpoint = "http://localhost:8000",
                    ApiFormat = "vLLM",
                    HealthCheckEnabled = false
                });
                if (ep == null || string.IsNullOrEmpty(ep.Id)) throw new Exception("No endpoint returned");
                _VllmEpId = ep.Id;
            }
        }

        // ===== Completion Endpoint CRUD =====

        public static async Task TestCreateCompletionEndpointAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                CompletionEndpoint? cep = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Test Inference",
                    Model = "test-model",
                    Endpoint = _OllamaEndpoint,
                    ApiFormat = "Ollama",
                    HealthCheckEnabled = false,
                    MaximumTimeoutMs = 61000,
                    MaxConcurrentRequests = 3
                });
                if (cep == null || string.IsNullOrEmpty(cep.Id)) throw new Exception("No endpoint returned");
                if (cep.MaximumTimeoutMs != 61000) throw new Exception("MaximumTimeoutMs mismatch on create");
                if (cep.MaxConcurrentRequests != 3) throw new Exception("MaxConcurrentRequests mismatch on create");
                _TestCepId = cep.Id;
            }
        }

        public static async Task TestReadCompletionEndpointAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                CompletionEndpoint? cep = await admin.GetCompletionEndpointAsync(_TestCepId);
                if (cep == null) throw new Exception("Endpoint not found");
                if (cep.Model != "test-model") throw new Exception("Model mismatch");
                if (cep.MaximumTimeoutMs != 61000) throw new Exception("MaximumTimeoutMs mismatch");
                if (cep.MaxConcurrentRequests != 3) throw new Exception("MaxConcurrentRequests mismatch");
            }
        }

        public static async Task TestUpdateCompletionEndpointAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                CompletionEndpoint? updated = await admin.UpdateCompletionEndpointAsync(_TestCepId, new CompletionEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Updated Inference",
                    Model = "test-model-v2",
                    Endpoint = _OllamaEndpoint,
                    ApiFormat = "Ollama",
                    HealthCheckEnabled = false,
                    MaximumTimeoutMs = 91000,
                    MaxConcurrentRequests = 5
                });
                if (updated == null) throw new Exception("Update failed");
                if (updated.MaximumTimeoutMs != 91000) throw new Exception("MaximumTimeoutMs mismatch on update");
                if (updated.MaxConcurrentRequests != 5) throw new Exception("MaxConcurrentRequests mismatch on update");
            }
        }

        public static async Task TestCompletionEndpointExistsAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                bool exists = await admin.CompletionEndpointExistsAsync(_TestCepId);
                if (!exists) throw new Exception("Endpoint should exist");
            }
        }

        public static async Task TestEnumerateCompletionEndpointsAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EnumerationResult<CompletionEndpoint>? result = await admin.EnumerateCompletionEndpointsAsync(new EnumerationRequest { MaxResults = 10 });
                if (result == null || result.Data.Count == 0) throw new Exception("No endpoints returned");
            }
        }

        public static async Task TestCreateGeminiCompletionEndpointAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                CompletionEndpoint? cep = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Gemini Inference",
                    Model = "gemini-2.5-flash",
                    Endpoint = "https://generativelanguage.googleapis.com",
                    ApiFormat = "Gemini",
                    ApiKey = "test-api-key",
                    HealthCheckEnabled = false
                });
                if (cep == null || string.IsNullOrEmpty(cep.Id)) throw new Exception("No endpoint returned");
                _GeminiCepId = cep.Id;
            }
        }

        public static async Task TestCreateVllmCompletionEndpointAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                CompletionEndpoint? cep = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "vLLM Inference",
                    Model = "Qwen/Qwen2.5-7B-Instruct",
                    Endpoint = "http://localhost:8000",
                    ApiFormat = "vLLM",
                    HealthCheckEnabled = false
                });
                if (cep == null || string.IsNullOrEmpty(cep.Id)) throw new Exception("No endpoint returned");
                _VllmCepId = cep.Id;
            }
        }

        // ===== Model Loading =====

        public static async Task TestLoadOllamaCompletionEndpointAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                ModelLoadResponse? result = await admin.LoadCompletionEndpointAsync(_TestCepId, new ModelLoadRequest
                {
                    KeepAlive = "30m"
                });

                if (result == null) throw new Exception("No load response");
                if (!result.Success) throw new Exception("Load failed: " + result.Message);
                if (result.EndpointId != _TestCepId) throw new Exception("Endpoint mismatch");
                if (result.Outcome != "Loaded") throw new Exception("Expected Loaded, got " + result.Outcome);
                if (result.Strategy != "NativeProviderLoad") throw new Exception("Expected NativeProviderLoad, got " + result.Strategy);
                if (result.CompletionCalls == null || result.CompletionCalls.Count == 0) throw new Exception("Expected upstream completion call details");
                if (!string.IsNullOrWhiteSpace(result.RequestHistoryId))
                {
                    RequestHistoryDetail? detail = await admin.GetRequestHistoryDetailAsync(result.RequestHistoryId).ConfigureAwait(false);
                    if (detail == null || detail.ModelLoad == null || !detail.ModelLoad.ContainsKey("Outcome"))
                        throw new Exception("Expected ModelLoad metadata in request history detail");
                }
            }
        }

        public static async Task TestLoadOllamaEmbeddingEndpointAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                ModelLoadResponse? result = await admin.LoadEndpointAsync(_TestEpId, new ModelLoadRequest
                {
                    KeepAlive = "30m"
                });

                if (result == null) throw new Exception("No load response");
                if (!result.Success) throw new Exception("Load failed: " + result.Message);
                if (result.EndpointId != _TestEpId) throw new Exception("Endpoint mismatch");
                if (result.Outcome != "Loaded") throw new Exception("Expected Loaded, got " + result.Outcome);
                if (result.Strategy != "NativeProviderLoad") throw new Exception("Expected NativeProviderLoad, got " + result.Strategy);
                if (result.EmbeddingCalls == null || result.EmbeddingCalls.Count == 0) throw new Exception("Expected upstream embedding call details");
            }
        }

        public static async Task TestWarmOpenAiCompletionEndpointAsync()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer();
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            CompletionEndpoint? endpoint = null;
            try
            {
                endpoint = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "OpenAI Warm Test",
                    Model = provider.CompletionModel,
                    Endpoint = provider.BaseUrl,
                    ApiFormat = "OpenAI",
                    ApiKey = "test-api-key",
                    HealthCheckEnabled = false
                });
                if (endpoint == null || string.IsNullOrEmpty(endpoint.Id)) throw new Exception("No endpoint returned");

                ModelLoadResponse? result = await admin.LoadCompletionEndpointAsync(endpoint.Id, new ModelLoadRequest
                {
                    Strategy = "WarmRequest",
                    SampleInput = "OpenAI warm probe",
                    MaxTokens = 1
                });

                if (result == null) throw new Exception("No load response");
                if (!result.Success) throw new Exception("Warm failed: " + result.Message);
                if (result.Outcome != "Warmed") throw new Exception("Expected Warmed, got " + result.Outcome);
                await provider.WaitForCompletionRequestCountAsync(1).ConfigureAwait(false);
            }
            finally
            {
                if (endpoint != null && !string.IsNullOrEmpty(endpoint.Id))
                    await admin.DeleteCompletionEndpointAsync(endpoint.Id).ConfigureAwait(false);
            }
        }

        public static async Task TestWarmOpenAiEmbeddingEndpointAsync()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer();
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            EmbeddingEndpoint? endpoint = null;
            try
            {
                endpoint = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "OpenAI Embedding Warm Test",
                    Model = provider.EmbeddingModel,
                    Endpoint = provider.BaseUrl,
                    ApiFormat = "OpenAI",
                    ApiKey = "test-api-key",
                    HealthCheckEnabled = false
                });
                if (endpoint == null || string.IsNullOrEmpty(endpoint.Id)) throw new Exception("No endpoint returned");

                ModelLoadResponse? result = await admin.LoadEndpointAsync(endpoint.Id, new ModelLoadRequest
                {
                    Strategy = "WarmRequest",
                    SampleInput = "OpenAI embedding warm probe"
                });

                if (result == null) throw new Exception("No load response");
                if (!result.Success) throw new Exception("Warm failed: " + result.Message);
                if (result.Outcome != "Warmed") throw new Exception("Expected Warmed, got " + result.Outcome);
                await provider.WaitForEmbeddingRequestCountAsync(1).ConfigureAwait(false);
            }
            finally
            {
                if (endpoint != null && !string.IsNullOrEmpty(endpoint.Id))
                    await admin.DeleteEndpointAsync(endpoint.Id).ConfigureAwait(false);
            }
        }

        public static async Task TestWarmVllmCompletionEndpointAsync()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer();
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            CompletionEndpoint? endpoint = null;
            try
            {
                endpoint = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "vLLM Warm Test",
                    Model = provider.CompletionModel,
                    Endpoint = provider.BaseUrl,
                    ApiFormat = "vLLM",
                    HealthCheckEnabled = false
                });
                if (endpoint == null || string.IsNullOrEmpty(endpoint.Id)) throw new Exception("No endpoint returned");

                ModelLoadResponse? result = await admin.LoadCompletionEndpointAsync(endpoint.Id, new ModelLoadRequest
                {
                    Strategy = "WarmRequest",
                    SampleInput = "vLLM warm probe",
                    MaxTokens = 1
                });

                if (result == null) throw new Exception("No load response");
                if (!result.Success) throw new Exception("Warm failed: " + result.Message);
                if (result.Outcome != "Warmed") throw new Exception("Expected Warmed, got " + result.Outcome);
                if (string.IsNullOrWhiteSpace(result.Message) || !result.Message.Contains("vLLM", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Expected vLLM limitation message");
                await provider.WaitForCompletionRequestCountAsync(1).ConfigureAwait(false);
            }
            finally
            {
                if (endpoint != null && !string.IsNullOrEmpty(endpoint.Id))
                    await admin.DeleteCompletionEndpointAsync(endpoint.Id).ConfigureAwait(false);
            }
        }

        public static async Task TestWarmVllmEmbeddingEndpointAsync()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer();
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            EmbeddingEndpoint? endpoint = null;
            try
            {
                endpoint = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "vLLM Embedding Warm Test",
                    Model = provider.EmbeddingModel,
                    Endpoint = provider.BaseUrl,
                    ApiFormat = "vLLM",
                    HealthCheckEnabled = false
                });
                if (endpoint == null || string.IsNullOrEmpty(endpoint.Id)) throw new Exception("No endpoint returned");

                ModelLoadResponse? result = await admin.LoadEndpointAsync(endpoint.Id, new ModelLoadRequest
                {
                    Strategy = "WarmRequest",
                    SampleInput = "vLLM embedding warm probe"
                });

                if (result == null) throw new Exception("No load response");
                if (!result.Success) throw new Exception("Warm failed: " + result.Message);
                if (result.Outcome != "Warmed") throw new Exception("Expected Warmed, got " + result.Outcome);
                if (string.IsNullOrWhiteSpace(result.Message) || !result.Message.Contains("vLLM", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Expected vLLM limitation message");
                await provider.WaitForEmbeddingRequestCountAsync(1).ConfigureAwait(false);
            }
            finally
            {
                if (endpoint != null && !string.IsNullOrEmpty(endpoint.Id))
                    await admin.DeleteEndpointAsync(endpoint.Id).ConfigureAwait(false);
            }
        }

        public static async Task TestWarmGeminiCompletionEndpointAsync()
        {
            using SlowGeminiCompatibleServer provider = new SlowGeminiCompatibleServer();
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            CompletionEndpoint? endpoint = null;
            try
            {
                endpoint = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Gemini Warm Test",
                    Model = provider.CompletionModel,
                    Endpoint = provider.BaseUrl,
                    ApiFormat = "Gemini",
                    ApiKey = "test-api-key",
                    HealthCheckEnabled = false
                });
                if (endpoint == null || string.IsNullOrEmpty(endpoint.Id)) throw new Exception("No endpoint returned");

                ModelLoadResponse? result = await admin.LoadCompletionEndpointAsync(endpoint.Id, new ModelLoadRequest
                {
                    Strategy = "WarmRequest",
                    SampleInput = "Gemini warm probe",
                    MaxTokens = 1
                });

                if (result == null) throw new Exception("No load response");
                if (!result.Success) throw new Exception("Warm failed: " + result.Message);
                if (result.Outcome != "Warmed") throw new Exception("Expected Warmed, got " + result.Outcome);
                await provider.WaitForCompletionRequestCountAsync(1).ConfigureAwait(false);
            }
            finally
            {
                if (endpoint != null && !string.IsNullOrEmpty(endpoint.Id))
                    await admin.DeleteCompletionEndpointAsync(endpoint.Id).ConfigureAwait(false);
            }
        }

        public static async Task TestWarmGeminiEmbeddingEndpointAsync()
        {
            using SlowGeminiCompatibleServer provider = new SlowGeminiCompatibleServer();
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            EmbeddingEndpoint? endpoint = null;
            try
            {
                endpoint = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Gemini Embedding Warm Test",
                    Model = provider.EmbeddingModel,
                    Endpoint = provider.BaseUrl,
                    ApiFormat = "Gemini",
                    ApiKey = "test-api-key",
                    HealthCheckEnabled = false
                });
                if (endpoint == null || string.IsNullOrEmpty(endpoint.Id)) throw new Exception("No endpoint returned");

                ModelLoadResponse? result = await admin.LoadEndpointAsync(endpoint.Id, new ModelLoadRequest
                {
                    Strategy = "WarmRequest",
                    SampleInput = "Gemini embedding warm probe"
                });

                if (result == null) throw new Exception("No load response");
                if (!result.Success) throw new Exception("Warm failed: " + result.Message);
                if (result.Outcome != "Warmed") throw new Exception("Expected Warmed, got " + result.Outcome);
                await provider.WaitForEmbeddingRequestCountAsync(1).ConfigureAwait(false);
            }
            finally
            {
                if (endpoint != null && !string.IsNullOrEmpty(endpoint.Id))
                    await admin.DeleteEndpointAsync(endpoint.Id).ConfigureAwait(false);
            }
        }

        public static async Task TestUnsupportedNativeCompletionLoadAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                try
                {
                    await admin.LoadCompletionEndpointAsync(_VllmCepId, new ModelLoadRequest
                    {
                        Strategy = "NativeProviderLoad",
                        RequireNativeLoad = true
                    });
                    throw new Exception("Expected PartioException with 409");
                }
                catch (PartioException ex) when (ex.StatusCode == 409)
                {
                    // Expected
                }
            }
        }

        public static async Task TestInvalidLoadKeepAliveAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                try
                {
                    await admin.LoadCompletionEndpointAsync(_TestCepId, new ModelLoadRequest
                    {
                        KeepAlive = "0"
                    });
                    throw new Exception("Expected PartioException with 400");
                }
                catch (PartioException ex) when (ex.StatusCode == 400)
                {
                    // Expected
                }
            }
        }

        public static async Task TestLoadMissingEndpointAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                try
                {
                    await admin.LoadCompletionEndpointAsync("cep_missing_load_test", new ModelLoadRequest());
                    throw new Exception("Expected PartioException with 404");
                }
                catch (PartioException ex) when (ex.StatusCode == 404)
                {
                    // Expected
                }
            }
        }

        public static async Task TestLoadInactiveEndpointAsync()
        {
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);
            CompletionEndpoint? endpoint = null;

            try
            {
                endpoint = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Inactive Load Test",
                    Model = "test-model",
                    Endpoint = _OllamaEndpoint,
                    ApiFormat = "Ollama",
                    Active = false,
                    HealthCheckEnabled = false
                });
                if (endpoint == null || string.IsNullOrEmpty(endpoint.Id)) throw new Exception("No endpoint returned");

                try
                {
                    await admin.LoadCompletionEndpointAsync(endpoint.Id, new ModelLoadRequest());
                    throw new Exception("Expected PartioException with 400");
                }
                catch (PartioException ex) when (ex.StatusCode == 400)
                {
                    // Expected
                }
            }
            finally
            {
                if (endpoint != null && !string.IsNullOrEmpty(endpoint.Id))
                    await admin.DeleteCompletionEndpointAsync(endpoint.Id).ConfigureAwait(false);
            }
        }

        public static async Task TestLoadCompletionTimeoutStatusAsync()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer(completionDelayMs: 1200);
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            CompletionEndpoint? endpoint = null;
            try
            {
                endpoint = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Load Timeout Test",
                    Model = provider.CompletionModel,
                    Endpoint = provider.BaseUrl,
                    ApiFormat = "OpenAI",
                    ApiKey = "test-api-key",
                    HealthCheckEnabled = false,
                    MaximumTimeoutMs = 200
                });
                if (endpoint == null || string.IsNullOrEmpty(endpoint.Id)) throw new Exception("No endpoint returned");

                try
                {
                    await admin.LoadCompletionEndpointAsync(endpoint.Id, new ModelLoadRequest
                    {
                        Strategy = "WarmRequest",
                        TimeoutMs = 200,
                        SampleInput = "Timeout load probe"
                    });
                    throw new Exception("Expected PartioException with 504");
                }
                catch (PartioException ex) when (ex.StatusCode == 504)
                {
                    // Expected
                }
            }
            finally
            {
                if (endpoint != null && !string.IsNullOrEmpty(endpoint.Id))
                    await admin.DeleteCompletionEndpointAsync(endpoint.Id).ConfigureAwait(false);
            }
        }

        public static async Task TestLoadCompletionConcurrencyLimitStatusAsync()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer(completionDelayMs: 8000);
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            CompletionEndpoint? endpoint = null;
            try
            {
                endpoint = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Load Concurrency Test",
                    Model = provider.CompletionModel,
                    Endpoint = provider.BaseUrl,
                    ApiFormat = "OpenAI",
                    ApiKey = "test-api-key",
                    HealthCheckEnabled = false,
                    MaximumTimeoutMs = 60000,
                    MaxConcurrentRequests = 1
                });
                if (endpoint == null || string.IsNullOrEmpty(endpoint.Id)) throw new Exception("No endpoint returned");

                Task<ModelLoadResponse?> firstTask = admin.LoadCompletionEndpointAsync(endpoint.Id, new ModelLoadRequest
                {
                    Strategy = "WarmRequest",
                    TimeoutMs = 15000,
                    SampleInput = "First load concurrency probe"
                });

                await provider.WaitForCompletionRequestCountAsync(1).ConfigureAwait(false);

                try
                {
                    await admin.LoadCompletionEndpointAsync(endpoint.Id, new ModelLoadRequest
                    {
                        Strategy = "WarmRequest",
                        TimeoutMs = 15000,
                        SampleInput = "Second load concurrency probe"
                    }).ConfigureAwait(false);
                    throw new Exception("Expected PartioException with 429");
                }
                catch (PartioException ex) when (ex.StatusCode == 429)
                {
                    // Expected
                }

                ModelLoadResponse? first = await firstTask.ConfigureAwait(false);
                if (first == null || !first.Success)
                    throw new Exception("First load request should have succeeded");
            }
            finally
            {
                if (endpoint != null && !string.IsNullOrEmpty(endpoint.Id))
                    await admin.DeleteCompletionEndpointAsync(endpoint.Id).ConfigureAwait(false);
            }
        }

        // ===== Request History =====

        public static async Task TestEnumerateRequestHistoryAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EnumerationResult<RequestHistoryEntry>? result = await admin.EnumerateRequestHistoryAsync(new EnumerationRequest { MaxResults = 10 });
                if (result == null) throw new Exception("No response");
            }
        }

        // ===== API Explorer =====

        public static async Task TestExploreEmbeddingEndpointAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EndpointExplorerEmbeddingResponse? result = await admin.ExploreEmbeddingEndpointAsync(new EndpointExplorerEmbeddingRequest
                {
                    EndpointId = _TestEpId,
                    Input = "Explorer embedding test payload"
                });
                if (result == null) throw new Exception("No response");
                if (result.EndpointId != _TestEpId) throw new Exception("Endpoint mismatch");
                if (result.EmbeddingCalls == null || result.EmbeddingCalls.Count == 0) throw new Exception("Expected upstream call details");
            }
        }

        public static async Task TestExploreCompletionEndpointAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EndpointExplorerCompletionResponse? result = await admin.ExploreCompletionEndpointAsync(new EndpointExplorerCompletionRequest
                {
                    EndpointId = _TestCepId,
                    Prompt = "Explorer completion test payload"
                });
                if (result == null) throw new Exception("No response");
                if (result.EndpointId != _TestCepId) throw new Exception("Endpoint mismatch");
                if (result.CompletionCalls == null || result.CompletionCalls.Count == 0) throw new Exception("Expected upstream call details");
            }
        }

        public static async Task TestExploreCompletionTimeoutStatusAsync()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer(completionDelayMs: 1200);
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            CompletionEndpoint? endpoint = null;

            try
            {
                endpoint = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Timeout Explorer Inference",
                    Model = "gpt-4.1-mini",
                    Endpoint = provider.BaseUrl,
                    ApiFormat = "OpenAI",
                    HealthCheckEnabled = false,
                    MaximumTimeoutMs = 250
                });

                if (endpoint == null || string.IsNullOrEmpty(endpoint.Id))
                    throw new Exception("No timeout test completion endpoint returned");

                EndpointExplorerCompletionResponse? result = await admin.ExploreCompletionEndpointAsync(new EndpointExplorerCompletionRequest
                {
                    EndpointId = endpoint.Id,
                    Prompt = "Timeout test prompt",
                    TimeoutMs = 5000
                });

                if (result == null) throw new Exception("No explorer response");
                if (result.Success) throw new Exception("Expected timeout explorer response");
                if (result.StatusCode != 504) throw new Exception("Expected explorer StatusCode=504, got " + result.StatusCode);
                if (string.IsNullOrWhiteSpace(result.Error) || result.Error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) < 0)
                    throw new Exception("Expected timeout error text");
                if (result.CompletionCalls == null || result.CompletionCalls.Count == 0)
                    throw new Exception("Expected recorded upstream completion call details");
            }
            finally
            {
                if (!string.IsNullOrEmpty(endpoint?.Id))
                    await admin.DeleteCompletionEndpointAsync(endpoint.Id).ConfigureAwait(false);
            }
        }

        public static async Task TestExploreEmbeddingConcurrencyLimitStatusAsync()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer(embeddingDelayMs: 8000);
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            EmbeddingEndpoint? endpoint = null;

            try
            {
                endpoint = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Concurrency Explorer Embedding",
                    Model = "text-embedding-3-small",
                    Endpoint = provider.BaseUrl,
                    ApiFormat = "OpenAI",
                    HealthCheckEnabled = false,
                    MaximumTimeoutMs = 60000,
                    MaxConcurrentRequests = 1
                });

                if (endpoint == null || string.IsNullOrEmpty(endpoint.Id))
                    throw new Exception("No concurrency test embedding endpoint returned");

                Task<EndpointExplorerEmbeddingResponse?> firstTask = admin.ExploreEmbeddingEndpointAsync(new EndpointExplorerEmbeddingRequest
                {
                    EndpointId = endpoint.Id,
                    Input = "First concurrency test payload"
                });

                await provider.WaitForEmbeddingRequestCountAsync(1).ConfigureAwait(false);

                try
                {
                    await admin.ExploreEmbeddingEndpointAsync(new EndpointExplorerEmbeddingRequest
                    {
                        EndpointId = endpoint.Id,
                        Input = "Second concurrency test payload"
                    }).ConfigureAwait(false);

                    throw new Exception("Expected PartioException with 429");
                }
                catch (PartioException ex) when (ex.StatusCode == 429)
                {
                    if (ex.Message.IndexOf("max concurrent request", StringComparison.OrdinalIgnoreCase) < 0)
                        throw new Exception("Expected concurrency error message, got: " + ex.Message);
                }

                EndpointExplorerEmbeddingResponse? first = await firstTask.ConfigureAwait(false);
                if (first == null || !first.Success)
                    throw new Exception("First explorer request should have succeeded");
            }
            finally
            {
                if (!string.IsNullOrEmpty(endpoint?.Id))
                    await admin.DeleteEndpointAsync(endpoint.Id).ConfigureAwait(false);
            }
        }

        public static async Task TestExploreCompletionConcurrencyLimitStatusAsync()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer(completionDelayMs: 8000);
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            CompletionEndpoint? endpoint = null;

            try
            {
                endpoint = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Concurrency Explorer Inference",
                    Model = "gpt-4.1-mini",
                    Endpoint = provider.BaseUrl,
                    ApiFormat = "OpenAI",
                    HealthCheckEnabled = false,
                    MaximumTimeoutMs = 60000,
                    MaxConcurrentRequests = 1
                });

                if (endpoint == null || string.IsNullOrEmpty(endpoint.Id))
                    throw new Exception("No concurrency test completion endpoint returned");

                Task<EndpointExplorerCompletionResponse?> firstTask = admin.ExploreCompletionEndpointAsync(new EndpointExplorerCompletionRequest
                {
                    EndpointId = endpoint.Id,
                    Prompt = "First concurrency completion payload",
                    TimeoutMs = 15000
                });

                await provider.WaitForCompletionRequestCountAsync(1).ConfigureAwait(false);

                try
                {
                    await admin.ExploreCompletionEndpointAsync(new EndpointExplorerCompletionRequest
                    {
                        EndpointId = endpoint.Id,
                        Prompt = "Second concurrency completion payload",
                        TimeoutMs = 15000
                    }).ConfigureAwait(false);

                    throw new Exception("Expected PartioException with 429");
                }
                catch (PartioException ex) when (ex.StatusCode == 429)
                {
                    if (ex.Message.IndexOf("max concurrent request", StringComparison.OrdinalIgnoreCase) < 0)
                        throw new Exception("Expected concurrency error message, got: " + ex.Message);
                }

                EndpointExplorerCompletionResponse? first = await firstTask.ConfigureAwait(false);
                if (first == null || !first.Success)
                    throw new Exception("First completion explorer request should have succeeded");
            }
            finally
            {
                if (!string.IsNullOrEmpty(endpoint?.Id))
                    await admin.DeleteCompletionEndpointAsync(endpoint.Id).ConfigureAwait(false);
            }
        }

        public static async Task TestProcessEmbeddingTimeoutAsync()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer(embeddingDelayMs: 1200);
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            EmbeddingEndpoint? endpoint = null;

            try
            {
                endpoint = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Timeout Process Embedding",
                    Model = "text-embedding-3-small",
                    Endpoint = provider.BaseUrl,
                    ApiFormat = "OpenAI",
                    HealthCheckEnabled = false,
                    MaximumTimeoutMs = 250
                });

                if (endpoint == null || string.IsNullOrEmpty(endpoint.Id))
                    throw new Exception("No timeout test embedding endpoint returned");

                try
                {
                    await admin.ProcessAsync(new SemanticCellRequest
                    {
                        Type = "Text",
                        Text = "Embedding timeout test payload.",
                        ChunkingConfiguration = new ChunkingConfiguration
                        {
                            Strategy = "FixedTokenCount",
                            FixedTokenCount = 512
                        },
                        EmbeddingConfiguration = new EmbeddingConfiguration
                        {
                            EmbeddingEndpointId = endpoint.Id
                        }
                    }).ConfigureAwait(false);

                    throw new Exception("Expected PartioException with 504");
                }
                catch (PartioException ex) when (ex.StatusCode == 504)
                {
                    if (ex.Message.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) < 0)
                        throw new Exception("Expected timeout message, got: " + ex.Message);
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(endpoint?.Id))
                    await admin.DeleteEndpointAsync(endpoint.Id).ConfigureAwait(false);
            }
        }

        public static async Task TestProcessSummarizationTimeoutAsync()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer(embeddingDelayMs: 0, completionDelayMs: 1200);
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            EmbeddingEndpoint? embeddingEndpoint = null;
            CompletionEndpoint? completionEndpoint = null;

            try
            {
                embeddingEndpoint = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Timeout Process Summary Embedding",
                    Model = "text-embedding-3-small",
                    Endpoint = provider.BaseUrl,
                    ApiFormat = "OpenAI",
                    HealthCheckEnabled = false,
                    MaximumTimeoutMs = 60000
                });

                completionEndpoint = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Timeout Process Summary Completion",
                    Model = "gpt-4.1-mini",
                    Endpoint = provider.BaseUrl,
                    ApiFormat = "OpenAI",
                    HealthCheckEnabled = false,
                    MaximumTimeoutMs = 250
                });

                if (embeddingEndpoint == null || string.IsNullOrEmpty(embeddingEndpoint.Id))
                    throw new Exception("No timeout test embedding endpoint returned");
                if (completionEndpoint == null || string.IsNullOrEmpty(completionEndpoint.Id))
                    throw new Exception("No timeout test completion endpoint returned");

                try
                {
                    await admin.ProcessAsync(new SemanticCellRequest
                    {
                        Type = "Text",
                        Text = "Summarization timeout test payload.",
                        ChunkingConfiguration = new ChunkingConfiguration
                        {
                            Strategy = "FixedTokenCount",
                            FixedTokenCount = 512
                        },
                        EmbeddingConfiguration = new EmbeddingConfiguration
                        {
                            EmbeddingEndpointId = embeddingEndpoint.Id
                        },
                        SummarizationConfiguration = new SummarizationConfiguration
                        {
                            CompletionEndpointId = completionEndpoint.Id,
                            TimeoutMs = 5000,
                            MaxSummaryTokens = 128,
                            MinCellLength = 0
                        }
                    }).ConfigureAwait(false);

                    throw new Exception("Expected PartioException with 504");
                }
                catch (PartioException ex) when (ex.StatusCode == 504)
                {
                    if (ex.Message.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) < 0)
                        throw new Exception("Expected timeout message, got: " + ex.Message);
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(completionEndpoint?.Id))
                    await admin.DeleteCompletionEndpointAsync(completionEndpoint.Id).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(embeddingEndpoint?.Id))
                    await admin.DeleteEndpointAsync(embeddingEndpoint.Id).ConfigureAwait(false);
            }
        }

        // ===== Process (RegexBased) =====

        public static async Task TestProcessTextRegexMarkdownHeadingsAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EnumerationResult<EmbeddingEndpoint>? eps = await admin.EnumerateEndpointsAsync();
                EmbeddingEndpoint? activeEp = eps?.Data?.FirstOrDefault(e => e.Active != false);
                if (activeEp == null) throw new Exception("SKIP: no active embedding endpoint");

                SemanticCellRequest req = new SemanticCellRequest
                {
                    Type = "Text",
                    Text = "# A\nText about A.\n\n# B\nText about B.\n\n# C\nText about C.",
                    EmbeddingConfiguration = new EmbeddingConfiguration { EmbeddingEndpointId = activeEp.Id },
                    ChunkingConfiguration = new ChunkingConfiguration
                    {
                        Strategy = "RegexBased",
                        RegexPattern = @"(?=^#{1,3}\s)",
                        FixedTokenCount = 512
                    }
                };

                SemanticCellResponse? result = await admin.ProcessAsync(req);
                if (result == null || result.Chunks == null || result.Chunks.Count != 3) throw new Exception("Expected 3 chunks, got " + (result?.Chunks?.Count ?? 0));
            }
        }

        public static async Task TestProcessTextRegexDoubleNewlineAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EnumerationResult<EmbeddingEndpoint>? eps = await admin.EnumerateEndpointsAsync();
                EmbeddingEndpoint? activeEp = eps?.Data?.FirstOrDefault(e => e.Active != false);
                if (activeEp == null) throw new Exception("SKIP: no active embedding endpoint");

                SemanticCellRequest req = new SemanticCellRequest
                {
                    Type = "Text",
                    Text = "First paragraph.\n\nSecond paragraph.\n\nThird paragraph.",
                    EmbeddingConfiguration = new EmbeddingConfiguration { EmbeddingEndpointId = activeEp.Id },
                    ChunkingConfiguration = new ChunkingConfiguration
                    {
                        Strategy = "RegexBased",
                        RegexPattern = @"\n\n+",
                        FixedTokenCount = 512
                    }
                };

                SemanticCellResponse? result = await admin.ProcessAsync(req);
                if (result == null || result.Chunks == null || result.Chunks.Count != 3) throw new Exception("Expected 3 chunks, got " + (result?.Chunks?.Count ?? 0));
            }
        }

        public static async Task TestProcessTextRegexSingleSegmentAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EnumerationResult<EmbeddingEndpoint>? eps = await admin.EnumerateEndpointsAsync();
                EmbeddingEndpoint? activeEp = eps?.Data?.FirstOrDefault(e => e.Active != false);
                if (activeEp == null) throw new Exception("SKIP: no active embedding endpoint");

                SemanticCellRequest req = new SemanticCellRequest
                {
                    Type = "Text",
                    Text = "No headings in this text whatsoever.",
                    EmbeddingConfiguration = new EmbeddingConfiguration { EmbeddingEndpointId = activeEp.Id },
                    ChunkingConfiguration = new ChunkingConfiguration
                    {
                        Strategy = "RegexBased",
                        RegexPattern = @"(?=^#{1,3}\s)",
                        FixedTokenCount = 512
                    }
                };

                SemanticCellResponse? result = await admin.ProcessAsync(req);
                if (result == null || result.Chunks == null || result.Chunks.Count != 1) throw new Exception("Expected 1 chunk, got " + (result?.Chunks?.Count ?? 0));
            }
        }

        public static async Task TestProcessCodeRegexBasedAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EnumerationResult<EmbeddingEndpoint>? eps = await admin.EnumerateEndpointsAsync();
                EmbeddingEndpoint? activeEp = eps?.Data?.FirstOrDefault(e => e.Active != false);
                if (activeEp == null) throw new Exception("SKIP: no active embedding endpoint");

                SemanticCellRequest req = new SemanticCellRequest
                {
                    Type = "Code",
                    Text = "def foo():\n    pass\n\ndef bar():\n    pass\n\ndef baz():\n    pass",
                    EmbeddingConfiguration = new EmbeddingConfiguration { EmbeddingEndpointId = activeEp.Id },
                    ChunkingConfiguration = new ChunkingConfiguration
                    {
                        Strategy = "RegexBased",
                        RegexPattern = @"(?=^def\s)",
                        FixedTokenCount = 512
                    }
                };

                SemanticCellResponse? result = await admin.ProcessAsync(req);
                if (result == null || result.Chunks == null || result.Chunks.Count == 0) throw new Exception("Expected chunks");
            }
        }

        public static async Task TestRegexStrategyMissingPatternAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EnumerationResult<EmbeddingEndpoint>? eps = await admin.EnumerateEndpointsAsync();
                EmbeddingEndpoint? activeEp = eps?.Data?.FirstOrDefault(e => e.Active != false);
                if (activeEp == null) throw new Exception("SKIP: no active embedding endpoint");

                try
                {
                    SemanticCellRequest req = new SemanticCellRequest
                    {
                        Type = "Text",
                        Text = "Some text.",
                        EmbeddingConfiguration = new EmbeddingConfiguration { EmbeddingEndpointId = activeEp.Id },
                        ChunkingConfiguration = new ChunkingConfiguration { Strategy = "RegexBased" }
                    };
                    await admin.ProcessAsync(req);
                    throw new Exception("Expected PartioException with 400");
                }
                catch (PartioException ex) when (ex.StatusCode == 400)
                {
                    // Expected
                }
            }
        }

        public static async Task TestRegexStrategyEmptyPatternAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EnumerationResult<EmbeddingEndpoint>? eps = await admin.EnumerateEndpointsAsync();
                EmbeddingEndpoint? activeEp = eps?.Data?.FirstOrDefault(e => e.Active != false);
                if (activeEp == null) throw new Exception("SKIP: no active embedding endpoint");

                try
                {
                    SemanticCellRequest req = new SemanticCellRequest
                    {
                        Type = "Text",
                        Text = "Some text.",
                        EmbeddingConfiguration = new EmbeddingConfiguration { EmbeddingEndpointId = activeEp.Id },
                        ChunkingConfiguration = new ChunkingConfiguration { Strategy = "RegexBased", RegexPattern = "" }
                    };
                    await admin.ProcessAsync(req);
                    throw new Exception("Expected PartioException with 400");
                }
                catch (PartioException ex) when (ex.StatusCode == 400)
                {
                    // Expected
                }
            }
        }

        public static async Task TestRegexStrategyInvalidPatternAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                EnumerationResult<EmbeddingEndpoint>? eps = await admin.EnumerateEndpointsAsync();
                EmbeddingEndpoint? activeEp = eps?.Data?.FirstOrDefault(e => e.Active != false);
                if (activeEp == null) throw new Exception("SKIP: no active embedding endpoint");

                try
                {
                    SemanticCellRequest req = new SemanticCellRequest
                    {
                        Type = "Text",
                        Text = "Some text.",
                        EmbeddingConfiguration = new EmbeddingConfiguration { EmbeddingEndpointId = activeEp.Id },
                        ChunkingConfiguration = new ChunkingConfiguration { Strategy = "RegexBased", RegexPattern = "([" }
                    };
                    await admin.ProcessAsync(req);
                    throw new Exception("Expected PartioException with 400");
                }
                catch (PartioException ex) when (ex.StatusCode == 400)
                {
                    // Expected
                }
            }
        }

        // ===== Error Cases =====

        public static async Task TestUnauthenticatedRequestAsync()
        {
            using (PartioClient badClient = new PartioClient(_Endpoint, "completely-invalid-token"))
            {
                try
                {
                    await badClient.EnumerateTenantsAsync();
                    throw new Exception("Expected PartioException with 401");
                }
                catch (PartioException ex) when (ex.StatusCode == 401)
                {
                    // Expected
                }
            }
        }

        public static async Task TestInvalidBearerTokenAsync()
        {
            using (PartioClient badClient = new PartioClient(_Endpoint, "another-bad-token-value"))
            {
                try
                {
                    await badClient.EnumerateTenantsAsync();
                    throw new Exception("Expected PartioException with 401");
                }
                catch (PartioException ex) when (ex.StatusCode == 401)
                {
                    // Expected
                }
            }
        }

        public static async Task TestNonExistentResourceAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                try
                {
                    await admin.GetTenantAsync("nonexistent-tenant-id-12345");
                    throw new Exception("Expected PartioException with 404");
                }
                catch (PartioException ex) when (ex.StatusCode == 404)
                {
                    // Expected
                }
            }
        }

        // ===== Cleanup =====

        public static async Task TestDeleteCompletionEndpointAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                await admin.DeleteCompletionEndpointAsync(_TestCepId);
                bool exists = await admin.CompletionEndpointExistsAsync(_TestCepId);
                if (exists) throw new Exception("Completion endpoint still exists after delete");
            }
        }

        public static async Task TestDeleteGeminiCompletionEndpointAsync()
        {
            if (string.IsNullOrEmpty(_GeminiCepId)) return;
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                await admin.DeleteCompletionEndpointAsync(_GeminiCepId);
            }
        }

        public static async Task TestDeleteVllmCompletionEndpointAsync()
        {
            if (string.IsNullOrEmpty(_VllmCepId)) return;
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                await admin.DeleteCompletionEndpointAsync(_VllmCepId);
            }
        }

        public static async Task TestDeleteEmbeddingEndpointAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                await admin.DeleteEndpointAsync(_TestEpId);
                bool exists = await admin.EndpointExistsAsync(_TestEpId);
                if (exists) throw new Exception("Endpoint still exists after delete");
            }
        }

        public static async Task TestDeleteGeminiEmbeddingEndpointAsync()
        {
            if (string.IsNullOrEmpty(_GeminiEpId)) return;
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                await admin.DeleteEndpointAsync(_GeminiEpId);
            }
        }

        public static async Task TestDeleteVllmEmbeddingEndpointAsync()
        {
            if (string.IsNullOrEmpty(_VllmEpId)) return;
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                await admin.DeleteEndpointAsync(_VllmEpId);
            }
        }

        public static async Task TestDeleteCredentialAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                await admin.DeleteCredentialAsync(_TestCredId);
                bool exists = await admin.CredentialExistsAsync(_TestCredId);
                if (exists) throw new Exception("Credential still exists after delete");
            }
        }

        public static async Task TestDeleteUserAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                await admin.DeleteUserAsync(_TestUserId);
                bool exists = await admin.UserExistsAsync(_TestUserId);
                if (exists) throw new Exception("User still exists after delete");
            }
        }

        public static async Task TestDeleteTenantAsync()
        {
            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            {
                await admin.DeleteTenantAsync(_TestTenantId);
                bool exists = await admin.TenantExistsAsync(_TestTenantId);
                if (exists) throw new Exception("Tenant still exists after delete");
            }
        }

        /// <summary>
        /// Returns all integration tests as SharedNamedTestCase instances, ordered for sequential execution.
        /// Tests are stateful and must run in the returned order.
        /// </summary>
        public static IReadOnlyList<SharedNamedTestCase> GetTests()
        {
            List<SharedNamedTestCase> tests = new List<SharedNamedTestCase>();

            // Health
            tests.Add(SharedNamedTestCase.CreateAsync("Health Check GET /", async () => await TestHealthCheckAsync()));

            // Tenant CRUD
            tests.Add(SharedNamedTestCase.CreateAsync("Create Tenant", async () => await TestCreateTenantAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Read Tenant", async () => await TestReadTenantAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Update Tenant", async () => await TestUpdateTenantAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Tenant Exists (HEAD)", async () => await TestTenantExistsAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Enumerate Tenants", async () => await TestEnumerateTenantsAsync()));

            // User CRUD
            tests.Add(SharedNamedTestCase.CreateAsync("Create User", async () => await TestCreateUserAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Read User", async () => await TestReadUserAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Update User", async () => await TestUpdateUserAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("User Exists (HEAD)", async () => await TestUserExistsAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Enumerate Users", async () => await TestEnumerateUsersAsync()));

            // Credential CRUD
            tests.Add(SharedNamedTestCase.CreateAsync("Create Credential", async () => await TestCreateCredentialAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Read Credential", async () => await TestReadCredentialAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Update Credential", async () => await TestUpdateCredentialAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Credential Exists (HEAD)", async () => await TestCredentialExistsAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Enumerate Credentials", async () => await TestEnumerateCredentialsAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Authenticate with New Credential", async () => await TestAuthenticateWithNewCredentialAsync()));

            // Embedding Endpoint CRUD
            tests.Add(SharedNamedTestCase.CreateAsync("Create Embedding Endpoint", async () => await TestCreateEmbeddingEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Read Embedding Endpoint", async () => await TestReadEmbeddingEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Update Embedding Endpoint", async () => await TestUpdateEmbeddingEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Endpoint Exists (HEAD)", async () => await TestEndpointExistsAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Enumerate Endpoints", async () => await TestEnumerateEndpointsAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Create Gemini Embedding Endpoint", async () => await TestCreateGeminiEmbeddingEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Create vLLM Embedding Endpoint", async () => await TestCreateVllmEmbeddingEndpointAsync()));

            // Completion Endpoint CRUD
            tests.Add(SharedNamedTestCase.CreateAsync("Create Completion Endpoint", async () => await TestCreateCompletionEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Read Completion Endpoint", async () => await TestReadCompletionEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Update Completion Endpoint", async () => await TestUpdateCompletionEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Completion Endpoint Exists (HEAD)", async () => await TestCompletionEndpointExistsAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Enumerate Completion Endpoints", async () => await TestEnumerateCompletionEndpointsAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Create Gemini Completion Endpoint", async () => await TestCreateGeminiCompletionEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Create vLLM Completion Endpoint", async () => await TestCreateVllmCompletionEndpointAsync()));

            // Model Loading
            tests.Add(SharedNamedTestCase.CreateAsync("Load Ollama Completion Endpoint Model", async () => await TestLoadOllamaCompletionEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Load Ollama Embedding Endpoint Model", async () => await TestLoadOllamaEmbeddingEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Warm OpenAI Completion Endpoint Model", async () => await TestWarmOpenAiCompletionEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Warm OpenAI Embedding Endpoint Model", async () => await TestWarmOpenAiEmbeddingEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Warm vLLM Completion Endpoint Model", async () => await TestWarmVllmCompletionEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Warm vLLM Embedding Endpoint Model", async () => await TestWarmVllmEmbeddingEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Warm Gemini Completion Endpoint Model", async () => await TestWarmGeminiCompletionEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Warm Gemini Embedding Endpoint Model", async () => await TestWarmGeminiEmbeddingEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Unsupported Native Completion Load (409)", async () => await TestUnsupportedNativeCompletionLoadAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Invalid Load KeepAlive (400)", async () => await TestInvalidLoadKeepAliveAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Load Missing Endpoint (404)", async () => await TestLoadMissingEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Load Inactive Endpoint (400)", async () => await TestLoadInactiveEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Load Completion Timeout (504)", async () => await TestLoadCompletionTimeoutStatusAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Load Completion Concurrency Limit (429)", async () => await TestLoadCompletionConcurrencyLimitStatusAsync()));

            // Request History
            tests.Add(SharedNamedTestCase.CreateAsync("Enumerate Request History", async () => await TestEnumerateRequestHistoryAsync()));

            // API Explorer
            tests.Add(SharedNamedTestCase.CreateAsync("Explore Embedding Endpoint", async () => await TestExploreEmbeddingEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Explore Completion Endpoint", async () => await TestExploreCompletionEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Explore Embedding Concurrency Limit (429)", async () => await TestExploreEmbeddingConcurrencyLimitStatusAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Explore Completion Concurrency Limit (429)", async () => await TestExploreCompletionConcurrencyLimitStatusAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Explore Completion Timeout Status (504)", async () => await TestExploreCompletionTimeoutStatusAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Process Embedding Timeout (504)", async () => await TestProcessEmbeddingTimeoutAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Process Summarization Timeout (504)", async () => await TestProcessSummarizationTimeoutAsync()));

            // Process (RegexBased)
            tests.Add(SharedNamedTestCase.CreateAsync("Process Text (RegexBased - Markdown Headings)", async () => await TestProcessTextRegexMarkdownHeadingsAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Process Text (RegexBased - Double Newline)", async () => await TestProcessTextRegexDoubleNewlineAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Process Text (RegexBased - Single Segment)", async () => await TestProcessTextRegexSingleSegmentAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Process Code (RegexBased)", async () => await TestProcessCodeRegexBasedAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Regex Strategy Missing Pattern (400)", async () => await TestRegexStrategyMissingPatternAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Regex Strategy Empty Pattern (400)", async () => await TestRegexStrategyEmptyPatternAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Regex Strategy Invalid Pattern (400)", async () => await TestRegexStrategyInvalidPatternAsync()));

            // Error Cases
            tests.Add(SharedNamedTestCase.CreateAsync("Unauthenticated Request (401)", async () => await TestUnauthenticatedRequestAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Invalid Bearer Token (401)", async () => await TestInvalidBearerTokenAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Non-existent Resource (404)", async () => await TestNonExistentResourceAsync()));

            // Cleanup
            tests.Add(SharedNamedTestCase.CreateAsync("Delete Completion Endpoint", async () => await TestDeleteCompletionEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Delete Gemini Completion Endpoint", async () => await TestDeleteGeminiCompletionEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Delete vLLM Completion Endpoint", async () => await TestDeleteVllmCompletionEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Delete Embedding Endpoint", async () => await TestDeleteEmbeddingEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Delete Gemini Embedding Endpoint", async () => await TestDeleteGeminiEmbeddingEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Delete vLLM Embedding Endpoint", async () => await TestDeleteVllmEmbeddingEndpointAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Delete Credential", async () => await TestDeleteCredentialAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Delete User", async () => await TestDeleteUserAsync()));
            tests.Add(SharedNamedTestCase.CreateAsync("Delete Tenant", async () => await TestDeleteTenantAsync()));

            return tests;
        }
    }
}
