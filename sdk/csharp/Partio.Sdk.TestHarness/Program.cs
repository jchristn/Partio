namespace Partio.Sdk.TestHarness
{
    using System.Diagnostics;
    using Partio.Sdk;
    using Partio.Sdk.Models;

    class Program
    {
        private static string _Endpoint = "http://localhost:8400";
        private static string _AdminKey = "partioadmin";
        private static string _TestToken = "default";
        private static string _ProviderEndpoint = "http://localhost:11434";
        private static string _EmbeddingModel = "nomic-embed-text";
        private static string _CompletionModel = "gemma3:4b";
        private static string? _ProviderBearer = null;
        private static bool _ProviderAvailable = false;
        private static int _Passed = 0;
        private static int _Failed = 0;
        private static int _Skipped = 0;
        private static List<string> _FailedTests = new List<string>();
        private static Stopwatch _TotalTimer = new Stopwatch();

        static async Task Main(string[] args)
        {
            if (args.Length >= 1) _Endpoint = args[0];
            if (args.Length >= 2) _AdminKey = args[1];
            if (args.Length >= 3) _TestToken = args[2];
            if (args.Length >= 4) _ProviderEndpoint = args[3];
            if (args.Length >= 5) _EmbeddingModel = args[4];
            if (args.Length >= 6) _CompletionModel = args[5];
            if (args.Length >= 7) _ProviderBearer = args[6];

            _ProviderEndpoint = Environment.GetEnvironmentVariable("PARTIO_TEST_PROVIDER_ENDPOINT") ?? _ProviderEndpoint;
            _EmbeddingModel = Environment.GetEnvironmentVariable("PARTIO_TEST_EMBEDDING_MODEL") ?? _EmbeddingModel;
            _CompletionModel = Environment.GetEnvironmentVariable("PARTIO_TEST_COMPLETION_MODEL") ?? _CompletionModel;
            _ProviderBearer = Environment.GetEnvironmentVariable("PARTIO_TEST_PROVIDER_BEARER") ?? _ProviderBearer;
            if (string.IsNullOrEmpty(_ProviderBearer)) _ProviderBearer = null;
            _ProviderAvailable = await IsProviderAvailableAsync(_ProviderEndpoint).ConfigureAwait(false);

            Console.WriteLine("Partio C# SDK Test Harness");
            Console.WriteLine("Endpoint: " + _Endpoint);
            Console.WriteLine("Admin Key: " + _AdminKey);
            Console.WriteLine("Provider Endpoint: " + _ProviderEndpoint + " (" + (_ProviderAvailable ? "available" : "unavailable; provider-dependent tests will skip") + ")");
            Console.WriteLine("Embedding Model: " + _EmbeddingModel);
            Console.WriteLine("Completion Model: " + _CompletionModel);
            Console.WriteLine();

            _TotalTimer.Start();

            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            using (PartioClient user = new PartioClient(_Endpoint, _TestToken))
            {
                // Health
                await RunTest("Health Check", async () =>
                {
                    Dictionary<string, string>? result = await admin.HealthAsync();
                    if (result == null || !result.ContainsKey("Status")) throw new Exception("No health response");
                    if (result["Status"] != "Healthy") throw new Exception("Not healthy: " + result["Status"]);
                });

                await RunTest("Who Am I", async () =>
                {
                    WhoAmIResponse? result = await admin.WhoAmIAsync();
                    if (result == null || string.IsNullOrEmpty(result.Role)) throw new Exception("No role");
                });

                // Tenant CRUD
                string testTenantId = "";
                await RunTest("Create Tenant", async () =>
                {
                    TenantMetadata? tenant = await admin.CreateTenantAsync(new TenantMetadata { Name = "Test Tenant", Labels = new List<string> { "test" } });
                    if (tenant == null) throw new Exception("No response");
                    testTenantId = tenant.Id ?? string.Empty;
                });

                await RunTest("Read Tenant", async () =>
                {
                    TenantMetadata? tenant = await admin.GetTenantAsync(testTenantId);
                    if (tenant == null || tenant.Name != "Test Tenant") throw new Exception("Tenant mismatch");
                });

                await RunTest("Update Tenant", async () =>
                {
                    TenantMetadata? updated = await admin.UpdateTenantAsync(testTenantId, new TenantMetadata { Name = "Updated Tenant" });
                    if (updated == null || updated.Name != "Updated Tenant") throw new Exception("Update failed");
                });

                await RunTest("Tenant Exists (HEAD)", async () =>
                {
                    bool exists = await admin.TenantExistsAsync(testTenantId);
                    if (!exists) throw new Exception("Tenant should exist");
                });

                await RunTest("Enumerate Tenants", async () =>
                {
                    EnumerationResult<TenantMetadata>? result = await admin.EnumerateTenantsAsync();
                    if (result == null || result.Data.Count == 0) throw new Exception("No tenants found");
                });

                // User CRUD
                string testUserId = "";
                await RunTest("Create User", async () =>
                {
                    UserMaster? userResult = await admin.CreateUserAsync(new UserMaster { TenantId = testTenantId, Email = "test@test.com", Password = "testpass", IsAdmin = false });
                    if (userResult == null) throw new Exception("No response");
                    testUserId = userResult.Id ?? string.Empty;
                });

                await RunTest("Read User", async () =>
                {
                    UserMaster? userResult = await admin.GetUserAsync(testUserId);
                    if (userResult == null || userResult.Email != "test@test.com") throw new Exception("User mismatch");
                });

                await RunTest("Update User", async () =>
                {
                    UserMaster? updated = await admin.UpdateUserAsync(testUserId, new UserMaster { Email = "updated@test.com", TenantId = testTenantId });
                    if (updated == null) throw new Exception("Update failed");
                });

                await RunTest("User Exists (HEAD)", async () =>
                {
                    bool exists = await admin.UserExistsAsync(testUserId);
                    if (!exists) throw new Exception("User should exist");
                });

                await RunTest("Enumerate Users", async () =>
                {
                    EnumerationResult<UserMaster>? result = await admin.EnumerateUsersAsync();
                    if (result == null || result.Data.Count == 0) throw new Exception("No users found");
                });

                // Credential CRUD
                string testCredId = "";
                await RunTest("Create Credential", async () =>
                {
                    Credential? cred = await admin.CreateCredentialAsync(new Credential { TenantId = testTenantId, UserId = testUserId, Name = "Test Key" });
                    if (cred == null) throw new Exception("No response");
                    testCredId = cred.Id ?? string.Empty;
                });

                await RunTest("Read Credential", async () =>
                {
                    Credential? cred = await admin.GetCredentialAsync(testCredId);
                    if (cred == null || cred.Name != "Test Key") throw new Exception("Credential mismatch");
                });

                await RunTest("Credential Exists (HEAD)", async () =>
                {
                    bool exists = await admin.CredentialExistsAsync(testCredId);
                    if (!exists) throw new Exception("Credential should exist");
                });

                await RunTest("Enumerate Credentials", async () =>
                {
                    EnumerationResult<Credential>? result = await admin.EnumerateCredentialsAsync();
                    if (result == null || result.Data.Count == 0) throw new Exception("No credentials found");
                });

                // Embedding Endpoint CRUD
                string testEpId = "";
                string geminiEpId = "";
                string vllmEpId = "";

                async Task<EmbeddingEndpoint> GetHarnessEmbeddingEndpointAsync(PartioClient adminClient)
                {
                    EmbeddingEndpoint? endpoint = await adminClient.GetEndpointAsync(testEpId).ConfigureAwait(false);
                    if (endpoint == null || endpoint.Active == false)
                        throw new SkipTestException("no active embedding endpoint");
                    return endpoint;
                }

                await RunTest("Create Endpoint", async () =>
                {
                    EmbeddingEndpoint? ep = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                    {
                        TenantId = testTenantId,
                        Name = "Test Embedding",
                        Model = _EmbeddingModel,
                        Endpoint = _ProviderEndpoint,
                        ApiFormat = "Ollama",
                        HealthCheckEnabled = false,
                        MaximumTimeoutMs = 61000,
                        MaxConcurrentRequests = 3,
                        MaxQueueDepth = 3,
                        Tokenization = new EndpointTokenizationSettings
                        {
                            TokenizerKind = "BertWordPiece",
                            TokenizerModel = "bert-base-uncased",
                            MaxInputTokens = 384,
                            ReservedInputTokens = 16,
                            BatchLimitMode = "PerInput",
                            AutoDetect = true
                        }
                    });
                    if (ep == null) throw new Exception("No response");
                    if (ep.MaximumTimeoutMs != 61000) throw new Exception("MaximumTimeoutMs mismatch");
                    if (ep.MaxConcurrentRequests != 3) throw new Exception("MaxConcurrentRequests mismatch");
                    if (ep.MaxQueueDepth != 3) throw new Exception("MaxQueueDepth mismatch");
                    testEpId = ep.Id ?? string.Empty;
                });

                await RunTest("Read Endpoint", async () =>
                {
                    EmbeddingEndpoint? ep = await admin.GetEndpointAsync(testEpId);
                    if (ep == null || ep.Model != _EmbeddingModel) throw new Exception("Endpoint mismatch");
                    if (ep.MaximumTimeoutMs != 61000) throw new Exception("MaximumTimeoutMs mismatch");
                    if (ep.MaxConcurrentRequests != 3) throw new Exception("MaxConcurrentRequests mismatch");
                    if (ep.MaxQueueDepth != 3) throw new Exception("MaxQueueDepth mismatch");
                    if (ep.Tokenization == null) throw new Exception("Expected tokenization override");
                    if (ep.Tokenization.TokenizerModel != "bert-base-uncased") throw new Exception("Tokenizer model mismatch");
                    if (ep.Tokenization.MaxInputTokens != 384) throw new Exception("MaxInputTokens mismatch");
                });

                await RunTest("Update Endpoint", async () =>
                {
                    EmbeddingEndpoint? updated = await admin.UpdateEndpointAsync(testEpId, new EmbeddingEndpoint
                    {
                        TenantId = testTenantId,
                        Name = "Updated Embedding",
                        Model = _EmbeddingModel,
                        Endpoint = _ProviderEndpoint,
                        ApiFormat = "Ollama",
                        HealthCheckEnabled = false,
                        MaximumTimeoutMs = 91000,
                        MaxConcurrentRequests = 5,
                        MaxQueueDepth = 4,
                        Tokenization = new EndpointTokenizationSettings
                        {
                            TokenizerKind = "Cl100kBase",
                            TokenizerModel = "cl100k_base",
                            MaxInputTokens = 2048,
                            ReservedInputTokens = 32,
                            BatchLimitMode = "WholeRequest",
                            AutoDetect = false
                        }
                    });
                    if (updated == null) throw new Exception("Update failed");
                    if (updated.MaximumTimeoutMs != 91000) throw new Exception("MaximumTimeoutMs mismatch");
                    if (updated.MaxConcurrentRequests != 5) throw new Exception("MaxConcurrentRequests mismatch");
                    if (updated.MaxQueueDepth != 4) throw new Exception("MaxQueueDepth mismatch");
                    if (updated.Tokenization == null) throw new Exception("Expected updated tokenization override");
                    if (updated.Tokenization.BatchLimitMode != "WholeRequest") throw new Exception("Batch limit mode mismatch");
                    if (updated.Tokenization.AutoDetect) throw new Exception("AutoDetect should be false");
                });

                await RunTest("Endpoint Exists (HEAD)", async () =>
                {
                    bool exists = await admin.EndpointExistsAsync(testEpId);
                    if (!exists) throw new Exception("Endpoint should exist");
                });

                await RunTest("Enumerate Endpoints", async () =>
                {
                    EnumerationResult<EmbeddingEndpoint>? result = await admin.EnumerateEndpointsAsync();
                    if (result == null || result.Data.Count == 0) throw new Exception("No endpoints found");
                });

                await RunTest("Create Gemini Embedding Endpoint", async () =>
                {
                    EmbeddingEndpoint? ep = await admin.CreateEndpointAsync(new EmbeddingEndpoint { TenantId = testTenantId, Name = "Gemini Embedding", Model = "gemini-embedding-001", Endpoint = "https://generativelanguage.googleapis.com", ApiFormat = "Gemini", ApiKey = "test-api-key", HealthCheckEnabled = false });
                    if (ep == null || string.IsNullOrEmpty(ep.Id)) throw new Exception("No response");
                    geminiEpId = ep.Id;
                });

                await RunTest("Create vLLM Embedding Endpoint", async () =>
                {
                    EmbeddingEndpoint? ep = await admin.CreateEndpointAsync(new EmbeddingEndpoint { TenantId = testTenantId, Name = "vLLM Embedding", Model = "intfloat/e5-small-v2", Endpoint = "http://localhost:8000", ApiFormat = "vLLM", HealthCheckEnabled = false });
                    if (ep == null || string.IsNullOrEmpty(ep.Id)) throw new Exception("No response");
                    vllmEpId = ep.Id;
                });

                // Request History
                await RunTest("Enumerate Request History", async () =>
                {
                    EnumerationResult<RequestHistoryEntry>? result = await admin.EnumerateRequestHistoryAsync();
                    if (result == null) throw new Exception("No response");
                });

                // Completion Endpoint CRUD
                string testCepId = "";
                string geminiCepId = "";
                string vllmCepId = "";
                await RunTest("Create Completion Endpoint", async () =>
                {
                    CompletionEndpoint? cep = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint { TenantId = testTenantId, Name = "Test Inference", Model = _CompletionModel, Endpoint = _ProviderEndpoint, ApiFormat = "Ollama", HealthCheckEnabled = false, MaximumTimeoutMs = 61000, MaxConcurrentRequests = 3, MaxQueueDepth = 5 });
                    if (cep == null) throw new Exception("No response");
                    if (cep.MaximumTimeoutMs != 61000) throw new Exception("MaximumTimeoutMs mismatch");
                    if (cep.MaxConcurrentRequests != 3) throw new Exception("MaxConcurrentRequests mismatch");
                    if (cep.MaxQueueDepth != 5) throw new Exception("MaxQueueDepth mismatch");
                    testCepId = cep.Id ?? string.Empty;
                });

                await RunTest("Read Completion Endpoint", async () =>
                {
                    CompletionEndpoint? cep = await admin.GetCompletionEndpointAsync(testCepId);
                    if (cep == null || cep.Model != _CompletionModel) throw new Exception("Endpoint mismatch");
                    if (cep.MaximumTimeoutMs != 61000) throw new Exception("MaximumTimeoutMs mismatch");
                    if (cep.MaxConcurrentRequests != 3) throw new Exception("MaxConcurrentRequests mismatch");
                    if (cep.MaxQueueDepth != 5) throw new Exception("MaxQueueDepth mismatch");
                });

                await RunTest("Update Completion Endpoint", async () =>
                {
                    CompletionEndpoint? updated = await admin.UpdateCompletionEndpointAsync(testCepId, new CompletionEndpoint { TenantId = testTenantId, Name = "Updated Inference", Model = _CompletionModel, Endpoint = _ProviderEndpoint, ApiFormat = "Ollama", HealthCheckEnabled = false, MaximumTimeoutMs = 91000, MaxConcurrentRequests = 5, MaxQueueDepth = 6 });
                    if (updated == null) throw new Exception("Update failed");
                    if (updated.MaximumTimeoutMs != 91000) throw new Exception("MaximumTimeoutMs mismatch");
                    if (updated.MaxConcurrentRequests != 5) throw new Exception("MaxConcurrentRequests mismatch");
                    if (updated.MaxQueueDepth != 6) throw new Exception("MaxQueueDepth mismatch");
                });

                await RunTest("Completion Endpoint Exists (HEAD)", async () =>
                {
                    bool exists = await admin.CompletionEndpointExistsAsync(testCepId);
                    if (!exists) throw new Exception("Endpoint should exist");
                });

                await RunTest("Enumerate Completion Endpoints", async () =>
                {
                    EnumerationResult<CompletionEndpoint>? result = await admin.EnumerateCompletionEndpointsAsync();
                    if (result == null || result.Data.Count == 0) throw new Exception("No endpoints found");
                });

                await RunTest("Create Gemini Completion Endpoint", async () =>
                {
                    CompletionEndpoint? cep = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint { TenantId = testTenantId, Name = "Gemini Inference", Model = "gemini-2.5-flash", Endpoint = "https://generativelanguage.googleapis.com", ApiFormat = "Gemini", ApiKey = "test-api-key", HealthCheckEnabled = false });
                    if (cep == null || string.IsNullOrEmpty(cep.Id)) throw new Exception("No response");
                    geminiCepId = cep.Id;
                });

                await RunTest("Create vLLM Completion Endpoint", async () =>
                {
                    CompletionEndpoint? cep = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint { TenantId = testTenantId, Name = "vLLM Inference", Model = "Qwen/Qwen2.5-7B-Instruct", Endpoint = "http://localhost:8000", ApiFormat = "vLLM", HealthCheckEnabled = false });
                    if (cep == null || string.IsNullOrEmpty(cep.Id)) throw new Exception("No response");
                    vllmCepId = cep.Id;
                });

                await RunTest("Create Ollama Cloud and xAI Endpoints", async () =>
                {
                    CompletionEndpoint? cloud = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint { TenantId = testTenantId, Name = "Ollama Cloud Inference", Model = "gpt-oss:20b", Endpoint = "http://ignored-host:11434", ApiFormat = "OllamaCloud", ApiKey = "test-api-key", HealthCheckEnabled = false });
                    try
                    {
                        if (cloud == null || string.IsNullOrEmpty(cloud.Id)) throw new Exception("No response");
                        if (cloud.Endpoint != "https://ollama.com") throw new Exception("Expected the fixed Ollama Cloud host, got " + cloud.Endpoint);
                    }
                    finally
                    {
                        if (!string.IsNullOrEmpty(cloud?.Id)) await admin.DeleteCompletionEndpointAsync(cloud.Id);
                    }

                    EmbeddingEndpoint? xai = await admin.CreateEndpointAsync(new EmbeddingEndpoint { TenantId = testTenantId, Name = "xAI Embedding", Model = "grok-embedding", Endpoint = "https://api.x.ai/v1", ApiFormat = "xAI", ApiKey = "test-api-key", HealthCheckEnabled = false });
                    try
                    {
                        if (xai == null || string.IsNullOrEmpty(xai.Id)) throw new Exception("No response");
                        if (xai.Endpoint != "https://api.x.ai") throw new Exception("Expected the /v1 suffix to be dropped, got " + xai.Endpoint);
                    }
                    finally
                    {
                        if (!string.IsNullOrEmpty(xai?.Id)) await admin.DeleteEndpointAsync(xai.Id);
                    }

                    try
                    {
                        await admin.CreateCompletionEndpointAsync(new CompletionEndpoint { TenantId = testTenantId, Name = "xAI Missing Key", Model = "grok-4", Endpoint = "https://api.x.ai", ApiFormat = "xAI", HealthCheckEnabled = false });
                        throw new Exception("Expected 400");
                    }
                    catch (PartioException ex) when (ex.StatusCode == 400)
                    {
                    }
                });

                // Provider-specific endpoints, secret masking, and reveal
                const string azureKey = "azure-test-key-1234567890";
                const string anthropicKey = "sk-ant-test-key-abcdef1234";
                const string bedrockSecret = "bedrock-secret-abcdefgh";
                const string bedrockSession = "short";
                const string voyageKey = "pa-voyage-test-key-9876";
                string serviceAccountJson = "{\"type\":\"service_account\",\"project_id\":\"partio-test\",\"client_email\":\"partio@partio-test.iam.gserviceaccount.com\",\"private_key\":\"-----BEGIN PRIVATE KEY-----\\nMIIBVQIBADANBgkqhkiG9w0BAQEFAASCAT8wggE7AgEAAkEA\\n-----END PRIVATE KEY-----\\n\"}";
                string azureCepId = "";
                string anthropicCepId = "";
                string bedrockEpId = "";
                string vertexEpId = "";
                string teiEpId = "";
                string voyageEpId = "";

                await RunTest("Create Provider-Specific Endpoints", async () =>
                {
                    CompletionEndpoint? azure = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                    {
                        TenantId = testTenantId, Name = "Azure Inference", Model = "gpt-4o-mini", Endpoint = "https://partio-test.openai.azure.com/openai",
                        ApiFormat = ApiFormats.AzureOpenAI, ApiKey = azureKey, HealthCheckEnabled = false,
                        ProviderSettings = new ProviderSettings { AzureOpenAI = new AzureOpenAiProviderSettings { ApiVersion = "2024-10-21", AuthMode = AzureAuthModes.ApiKey } }
                    });
                    if (azure == null || string.IsNullOrEmpty(azure.Id)) throw new Exception("No Azure response");
                    azureCepId = azure.Id;
                    if (azure.Endpoint != "https://partio-test.openai.azure.com") throw new Exception("Expected the /openai suffix to be dropped, got " + azure.Endpoint);
                    if (azure.ApiKey != "****7890") throw new Exception("Expected masked Azure ApiKey, got " + azure.ApiKey);
                    if (azure.ProviderSettings?.AzureOpenAI?.ApiVersion != "2024-10-21") throw new Exception("Azure ApiVersion mismatch");

                    CompletionEndpoint? anthropic = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                    {
                        TenantId = testTenantId, Name = "Anthropic Inference", Model = "claude-opus-4-8", Endpoint = "https://api.anthropic.com/v1",
                        ApiFormat = ApiFormats.Anthropic, ApiKey = anthropicKey, HealthCheckEnabled = false
                    });
                    if (anthropic == null || string.IsNullOrEmpty(anthropic.Id)) throw new Exception("No Anthropic response");
                    anthropicCepId = anthropic.Id;
                    if (anthropic.Endpoint != "https://api.anthropic.com") throw new Exception("Expected the /v1 suffix to be dropped, got " + anthropic.Endpoint);

                    EmbeddingEndpoint? bedrock = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                    {
                        TenantId = testTenantId, Name = "Bedrock Embedding", Model = "amazon.titan-embed-text-v2:0", Endpoint = "https://bedrock-runtime.us-east-1.amazonaws.com/",
                        ApiFormat = ApiFormats.Bedrock, HealthCheckEnabled = false,
                        ProviderSettings = new ProviderSettings { Bedrock = new BedrockProviderSettings { Region = "us-east-1", CredentialSource = AwsCredentialSources.Static, AccessKeyId = "AKIAPARTIOTEST", SecretAccessKey = bedrockSecret, SessionToken = bedrockSession } }
                    });
                    if (bedrock == null || string.IsNullOrEmpty(bedrock.Id)) throw new Exception("No Bedrock response");
                    bedrockEpId = bedrock.Id;
                    if (bedrock.Endpoint != "https://bedrock-runtime.us-east-1.amazonaws.com") throw new Exception("Expected the trailing slash to be dropped, got " + bedrock.Endpoint);

                    EmbeddingEndpoint? vertex = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                    {
                        TenantId = testTenantId, Name = "Vertex Embedding", Model = "text-embedding-005", Endpoint = "https://us-central1-aiplatform.googleapis.com/",
                        ApiFormat = ApiFormats.VertexAI, HealthCheckEnabled = false,
                        ProviderSettings = new ProviderSettings { VertexAI = new VertexAiProviderSettings { ProjectId = "partio-test", Region = "us-central1", CredentialSource = GcpCredentialSources.ServiceAccountJson, ServiceAccountJson = serviceAccountJson } }
                    });
                    if (vertex == null || string.IsNullOrEmpty(vertex.Id)) throw new Exception("No Vertex response");
                    vertexEpId = vertex.Id;
                    if (vertex.Endpoint != "https://us-central1-aiplatform.googleapis.com") throw new Exception("Expected the trailing slash to be dropped, got " + vertex.Endpoint);

                    EmbeddingEndpoint? tei = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                    {
                        TenantId = testTenantId, Name = "TEI Embedding", Model = "BAAI/bge-small-en-v1.5", Endpoint = "http://localhost:8080",
                        ApiFormat = ApiFormats.TEI, HealthCheckEnabled = false
                    });
                    if (tei == null || string.IsNullOrEmpty(tei.Id)) throw new Exception("No TEI response");
                    teiEpId = tei.Id;

                    EmbeddingEndpoint? voyage = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                    {
                        TenantId = testTenantId, Name = "Voyage Embedding", Model = "voyage-3.5", Endpoint = "https://api.voyageai.com/v1",
                        ApiFormat = ApiFormats.VoyageAI, ApiKey = voyageKey, HealthCheckEnabled = false,
                        ProviderSettings = new ProviderSettings { VoyageAI = new VoyageAiProviderSettings { InputType = "document", OutputDimension = 1024 } }
                    });
                    if (voyage == null || string.IsNullOrEmpty(voyage.Id)) throw new Exception("No Voyage response");
                    voyageEpId = voyage.Id;
                    if (voyage.ProviderSettings?.VoyageAI?.OutputDimension != 1024) throw new Exception("Voyage OutputDimension mismatch");
                });

                await RunTest("Read Masks Endpoint Secrets", async () =>
                {
                    CompletionEndpoint? azure = await admin.GetCompletionEndpointAsync(azureCepId);
                    if (azure?.ApiKey != "****7890") throw new Exception("Expected masked Azure ApiKey, got " + azure?.ApiKey);
                    EmbeddingEndpoint? bedrock = await admin.GetEndpointAsync(bedrockEpId);
                    BedrockProviderSettings? aws = bedrock?.ProviderSettings?.Bedrock;
                    if (aws == null) throw new Exception("Expected Bedrock settings");
                    if (aws.SecretAccessKey != "****efgh") throw new Exception("Expected masked SecretAccessKey, got " + aws.SecretAccessKey);
                    if (aws.SessionToken != "****") throw new Exception("Expected masked SessionToken, got " + aws.SessionToken);
                    if (aws.AccessKeyId != "AKIAPARTIOTEST") throw new Exception("AccessKeyId should not be masked");
                    EmbeddingEndpoint? vertex = await admin.GetEndpointAsync(vertexEpId);
                    if (vertex?.ProviderSettings?.VertexAI?.ServiceAccountJson != "****") throw new Exception("Expected masked ServiceAccountJson");
                    EnumerationResult<CompletionEndpoint>? list = await admin.EnumerateCompletionEndpointsAsync();
                    if (list == null) throw new Exception("No enumeration response");
                    foreach (CompletionEndpoint listed in list.Data)
                        if (!string.IsNullOrEmpty(listed.ApiKey) && !listed.ApiKey.StartsWith("****")) throw new Exception("Enumeration returned an unmasked ApiKey for " + listed.Id);
                    if ((await admin.GetCompletionEndpointAsync(anthropicCepId))?.ApiKey != "****1234") throw new Exception("Expected masked Anthropic ApiKey");
                });

                await RunTest("Reveal Endpoint Secrets", async () =>
                {
                    CompletionEndpoint? azure = await admin.RevealCompletionEndpointAsync(azureCepId);
                    if (azure?.ApiKey != azureKey) throw new Exception("Reveal did not return the Azure ApiKey");
                    EmbeddingEndpoint? bedrock = await admin.RevealEndpointAsync(bedrockEpId);
                    if (bedrock?.ProviderSettings?.Bedrock?.SecretAccessKey != bedrockSecret) throw new Exception("Reveal did not return the SecretAccessKey");
                    if (bedrock.ProviderSettings.Bedrock.SessionToken != bedrockSession) throw new Exception("Reveal did not return the SessionToken");
                    EmbeddingEndpoint? vertex = await admin.RevealEndpointAsync(vertexEpId);
                    if (vertex?.ProviderSettings?.VertexAI?.ServiceAccountJson != serviceAccountJson) throw new Exception("Reveal did not return the ServiceAccountJson");
                    using (PartioClient badClient = new PartioClient(_Endpoint, "invalid-token"))
                    {
                        try
                        {
                            await badClient.RevealEndpointAsync(bedrockEpId);
                            throw new Exception("Expected unauthenticated reveal to fail");
                        }
                        catch (PartioException ex) when (ex.StatusCode == 401)
                        {
                        }
                    }
                });

                await RunTest("Update Echoing Masked Secrets Keeps Them", async () =>
                {
                    CompletionEndpoint? azure = await admin.GetCompletionEndpointAsync(azureCepId);
                    if (azure == null) throw new Exception("Azure endpoint not found");
                    azure.Name = "Azure Inference Renamed";
                    CompletionEndpoint? updatedAzure = await admin.UpdateCompletionEndpointAsync(azureCepId, azure);
                    if (updatedAzure?.Name != "Azure Inference Renamed") throw new Exception("Azure rename failed");
                    if ((await admin.RevealCompletionEndpointAsync(azureCepId))?.ApiKey != azureKey) throw new Exception("Azure ApiKey was overwritten by the masked value");

                    EmbeddingEndpoint? bedrock = await admin.GetEndpointAsync(bedrockEpId);
                    if (bedrock == null) throw new Exception("Bedrock endpoint not found");
                    bedrock.Name = "Bedrock Embedding Renamed";
                    await admin.UpdateEndpointAsync(bedrockEpId, bedrock);
                    EmbeddingEndpoint? revealedBedrock = await admin.RevealEndpointAsync(bedrockEpId);
                    if (revealedBedrock?.ProviderSettings?.Bedrock?.SecretAccessKey != bedrockSecret) throw new Exception("SecretAccessKey was overwritten by the masked value");
                    if (revealedBedrock.ProviderSettings.Bedrock.SessionToken != bedrockSession) throw new Exception("SessionToken was overwritten by the masked value");

                    EmbeddingEndpoint? vertex = await admin.GetEndpointAsync(vertexEpId);
                    if (vertex == null) throw new Exception("Vertex endpoint not found");
                    await admin.UpdateEndpointAsync(vertexEpId, vertex);
                    if ((await admin.RevealEndpointAsync(vertexEpId))?.ProviderSettings?.VertexAI?.ServiceAccountJson != serviceAccountJson) throw new Exception("ServiceAccountJson was overwritten by the masked value");

                    EmbeddingEndpoint? voyage = await admin.GetEndpointAsync(voyageEpId);
                    if (voyage == null) throw new Exception("Voyage endpoint not found");
                    voyage.ApiKey = "pa-voyage-rotated-key-0000";
                    await admin.UpdateEndpointAsync(voyageEpId, voyage);
                    if ((await admin.RevealEndpointAsync(voyageEpId))?.ApiKey != "pa-voyage-rotated-key-0000") throw new Exception("A new ApiKey should replace the stored one");
                });

                await RunTest("Provider Validation Errors (400)", async () =>
                {
                    async Task Expect400(Func<Task> call, string label)
                    {
                        try
                        {
                            await call();
                            throw new Exception("Expected 400 for " + label);
                        }
                        catch (PartioException ex) when (ex.StatusCode == 400)
                        {
                        }
                    }

                    await Expect400(() => admin.CreateCompletionEndpointAsync(new CompletionEndpoint { TenantId = testTenantId, Name = "TEI Inference", Model = "bge", Endpoint = "http://localhost:8080", ApiFormat = ApiFormats.TEI, HealthCheckEnabled = false }), "TEI completion");
                    await Expect400(() => admin.CreateEndpointAsync(new EmbeddingEndpoint { TenantId = testTenantId, Name = "Anthropic Embedding", Model = "claude", Endpoint = "https://api.anthropic.com", ApiFormat = ApiFormats.Anthropic, ApiKey = anthropicKey, HealthCheckEnabled = false }), "Anthropic embedding");
                    await Expect400(() => admin.CreateEndpointAsync(new EmbeddingEndpoint
                    {
                        TenantId = testTenantId, Name = "Bedrock No Region", Model = "amazon.titan-embed-text-v2:0", Endpoint = "https://bedrock-runtime.us-east-1.amazonaws.com",
                        ApiFormat = ApiFormats.Bedrock, HealthCheckEnabled = false,
                        ProviderSettings = new ProviderSettings { Bedrock = new BedrockProviderSettings { AccessKeyId = "AKIAPARTIOTEST", SecretAccessKey = bedrockSecret } }
                    }), "Bedrock without Region");
                    await Expect400(() => admin.CreateCompletionEndpointAsync(new CompletionEndpoint { TenantId = testTenantId, Name = "Azure No Key", Model = "gpt-4o-mini", Endpoint = "https://partio-test.openai.azure.com", ApiFormat = ApiFormats.AzureOpenAI, HealthCheckEnabled = false }), "Azure without ApiKey");
                });

                await RunTest("Delete Provider-Specific Endpoints", async () =>
                {
                    foreach (string id in new[] { azureCepId, anthropicCepId })
                        if (!string.IsNullOrEmpty(id)) await admin.DeleteCompletionEndpointAsync(id);
                    foreach (string id in new[] { bedrockEpId, vertexEpId, teiEpId, voyageEpId })
                        if (!string.IsNullOrEmpty(id)) await admin.DeleteEndpointAsync(id);
                    if (!string.IsNullOrEmpty(bedrockEpId) && await admin.EndpointExistsAsync(bedrockEpId)) throw new Exception("Bedrock endpoint still exists");
                });

                await RunTest("Load Completion Endpoint", async () =>
                {
                    SkipIfProviderUnavailable();
                    ModelLoadResponse? result = await admin.LoadCompletionEndpointAsync(testCepId, new ModelLoadRequest { KeepAlive = "30m" });
                    if (result == null) throw new Exception("No response");
                    if (!result.Success) throw new Exception("Load failed: " + result.Message);
                    if (result.EndpointId != testCepId) throw new Exception("Endpoint mismatch");
                    if (result.Outcome != "Loaded") throw new Exception("Expected Loaded, got " + result.Outcome);
                });

                await RunTest("Load Embedding Endpoint", async () =>
                {
                    SkipIfProviderUnavailable();
                    ModelLoadResponse? result = await admin.LoadEndpointAsync(testEpId, new ModelLoadRequest { KeepAlive = "30m" });
                    if (result == null) throw new Exception("No response");
                    if (!result.Success) throw new Exception("Load failed: " + result.Message);
                    if (result.EndpointId != testEpId) throw new Exception("Endpoint mismatch");
                    if (result.Outcome != "Loaded") throw new Exception("Expected Loaded, got " + result.Outcome);
                });

                await RunTest("Unsupported Native Load", async () =>
                {
                    try
                    {
                        await admin.LoadCompletionEndpointAsync(vllmCepId, new ModelLoadRequest { Strategy = "NativeProviderLoad", RequireNativeLoad = true });
                        throw new Exception("Expected PartioException with 409");
                    }
                    catch (PartioException ex) when (ex.StatusCode == 409)
                    {
                        // Expected
                    }
                });

                await RunTest("Explore Embedding Endpoint", async () =>
                {
                    SkipIfProviderUnavailable();
                    EndpointExplorerEmbeddingResponse? result = await admin.ExploreEmbeddingEndpointAsync(new EndpointExplorerEmbeddingRequest
                    {
                        EndpointId = testEpId,
                        Input = "SDK embedding explorer test payload"
                    });
                    if (result == null) throw new Exception("No response");
                    if (result.EndpointId != testEpId) throw new Exception("Endpoint mismatch");
                    if (result.EmbeddingCalls == null || result.EmbeddingCalls.Count == 0) throw new Exception("Expected upstream call details");
                    if (result.TokenizationProfile == null) throw new Exception("Expected tokenization profile");
                    if (result.TokenizationProfile.EffectiveInputBudget < 1) throw new Exception("Invalid effective token budget");
                    if (!string.IsNullOrEmpty(result.RequestHistoryId))
                    {
                        RequestHistoryDetail? detail = await admin.GetRequestHistoryDetailAsync(result.RequestHistoryId);
                        if (detail?.TokenizationProfile == null) throw new Exception("Expected tokenization profile in request history detail");
                    }
                });

                await RunTest("Explore Completion Endpoint", async () =>
                {
                    SkipIfProviderUnavailable();
                    EndpointExplorerCompletionResponse? result = await admin.ExploreCompletionEndpointAsync(new EndpointExplorerCompletionRequest
                    {
                        EndpointId = testCepId,
                        Prompt = "SDK completion explorer test payload"
                    });
                    if (result == null) throw new Exception("No response");
                    if (result.EndpointId != testCepId) throw new Exception("Endpoint mismatch");
                    if (result.CompletionCalls == null || result.CompletionCalls.Count == 0) throw new Exception("Expected upstream call details");
                });

                // Proxy (transparent passthrough) — relay native Ollama requests through a dedicated
                // Ollama endpoint pointing at the provider (the upstream bearer, if any, is injected).
                string proxyCepId = "";
                await RunTest("Proxy Create Endpoint", async () =>
                {
                    CompletionEndpoint? cep = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                    {
                        TenantId = testTenantId,
                        Name = "Proxy Passthrough",
                        Model = _CompletionModel,
                        Endpoint = _ProviderEndpoint,
                        ApiFormat = "Ollama",
                        ApiKey = _ProviderBearer,
                        HealthCheckEnabled = false,
                        MaximumTimeoutMs = 120000
                    });
                    if (cep == null || string.IsNullOrEmpty(cep.Id)) throw new Exception("No proxy endpoint returned");
                    proxyCepId = cep.Id;
                });

                await RunTest("Proxy GET api/tags", async () =>
                {
                    SkipIfProviderUnavailable();
                    ProxyResponse r = await admin.ProxyGetAsync(proxyCepId, "api/tags");
                    if (r.StatusCode != 200) throw new Exception("Expected 200, got " + r.StatusCode + ": " + r.Body);
                });

                await RunTest("Proxy POST api/chat", async () =>
                {
                    SkipIfProviderUnavailable();
                    string body = "{\"model\":\"" + _CompletionModel + "\",\"messages\":[{\"role\":\"user\",\"content\":\"Reply with exactly: OK\"}],\"stream\":false}";
                    ProxyResponse r = await admin.ProxyPostAsync(proxyCepId, "api/chat", body);
                    if (r.StatusCode != 200) throw new Exception("Expected 200, got " + r.StatusCode + ": " + r.Body);
                });

                await RunTest("Proxy Disallowed Sub-path (404)", async () =>
                {
                    // An OpenAI path is not permitted for an Ollama endpoint — rejected by policy, not forwarded.
                    ProxyResponse r = await admin.ProxyPostAsync(proxyCepId, "v1/chat/completions", "{}");
                    if (r.StatusCode != 404) throw new Exception("Expected 404 for a disallowed sub-path, got " + r.StatusCode);
                });

                await RunTest("Proxy Delete Endpoint", async () =>
                {
                    if (!string.IsNullOrEmpty(proxyCepId)) await admin.DeleteCompletionEndpointAsync(proxyCepId);
                });

                // Process Single Cell (requires an active embedding endpoint)
                await RunTest("Process Single Cell", async () =>
                {
                    SkipIfProviderUnavailable();
                    EmbeddingEndpoint activeEp = await GetHarnessEmbeddingEndpointAsync(admin).ConfigureAwait(false);

                    SemanticCellRequest req = new SemanticCellRequest
                    {
                        Type = "Text",
                        Text = "Partio is a multi-tenant embedding platform.",
                        EmbeddingConfiguration = new EmbeddingConfiguration { EmbeddingEndpointId = activeEp.Id },
                        Labels = new List<string> { "test" },
                        Tags = new Dictionary<string, string> { { "source", "sdk-test" } }
                    };

                    SemanticCellResponse? result = await admin.ProcessAsync(req);
                    if (result == null) throw new Exception("No response");
                    if (string.IsNullOrEmpty(result.Text)) throw new Exception("Missing Text");
                    if (result.Chunks == null || result.Chunks.Count == 0) throw new Exception("No chunks");
                    if (result.Chunks[0].Embeddings == null || result.Chunks[0].Embeddings!.Count == 0) throw new Exception("No embeddings");
                    if (result.Chunks[0].Labels == null || result.Chunks[0].Labels!.Count == 0) throw new Exception("No labels on chunk");
                    if (result.Chunks[0].Tags == null || result.Chunks[0].Tags!.Count == 0) throw new Exception("No tags on chunk");
                });

                // Process Table strategies
                await RunTest("Process Table (Row)", async () =>
                {
                    SkipIfProviderUnavailable();
                    EmbeddingEndpoint activeEp = await GetHarnessEmbeddingEndpointAsync(admin).ConfigureAwait(false);

                    SemanticCellRequest req = new SemanticCellRequest
                    {
                        Type = "Table",
                        Table = new List<List<string>>
                        {
                            new List<string> { "id", "firstname", "lastname" },
                            new List<string> { "1", "george", "bush" },
                            new List<string> { "2", "barack", "obama" }
                        },
                        EmbeddingConfiguration = new EmbeddingConfiguration { EmbeddingEndpointId = activeEp.Id },
                        ChunkingConfiguration = new ChunkingConfiguration { Strategy = "Row" }
                    };

                    SemanticCellResponse? result = await admin.ProcessAsync(req);
                    if (result == null || result.Chunks == null || result.Chunks.Count != 2) throw new Exception("Expected 2 chunks");
                });

                await RunTest("Process Table (RowWithHeaders)", async () =>
                {
                    SkipIfProviderUnavailable();
                    EmbeddingEndpoint activeEp = await GetHarnessEmbeddingEndpointAsync(admin).ConfigureAwait(false);

                    SemanticCellRequest req = new SemanticCellRequest
                    {
                        Type = "Table",
                        Table = new List<List<string>>
                        {
                            new List<string> { "id", "firstname", "lastname" },
                            new List<string> { "1", "george", "bush" },
                            new List<string> { "2", "barack", "obama" }
                        },
                        EmbeddingConfiguration = new EmbeddingConfiguration { EmbeddingEndpointId = activeEp.Id },
                        ChunkingConfiguration = new ChunkingConfiguration { Strategy = "RowWithHeaders" }
                    };

                    SemanticCellResponse? result = await admin.ProcessAsync(req);
                    if (result == null || result.Chunks == null || result.Chunks.Count != 2) throw new Exception("Expected 2 chunks");
                });

                await RunTest("Process Table (RowGroupWithHeaders)", async () =>
                {
                    SkipIfProviderUnavailable();
                    EmbeddingEndpoint activeEp = await GetHarnessEmbeddingEndpointAsync(admin).ConfigureAwait(false);

                    SemanticCellRequest req = new SemanticCellRequest
                    {
                        Type = "Table",
                        Table = new List<List<string>>
                        {
                            new List<string> { "id", "firstname", "lastname" },
                            new List<string> { "1", "george", "bush" },
                            new List<string> { "2", "barack", "obama" },
                            new List<string> { "3", "donald", "trump" }
                        },
                        EmbeddingConfiguration = new EmbeddingConfiguration { EmbeddingEndpointId = activeEp.Id },
                        ChunkingConfiguration = new ChunkingConfiguration { Strategy = "RowGroupWithHeaders", RowGroupSize = 2 }
                    };

                    SemanticCellResponse? result = await admin.ProcessAsync(req);
                    if (result == null || result.Chunks == null || result.Chunks.Count != 2) throw new Exception("Expected 2 chunks (groups of 2)");
                });

                await RunTest("Process Table (KeyValuePairs)", async () =>
                {
                    SkipIfProviderUnavailable();
                    EmbeddingEndpoint activeEp = await GetHarnessEmbeddingEndpointAsync(admin).ConfigureAwait(false);

                    SemanticCellRequest req = new SemanticCellRequest
                    {
                        Type = "Table",
                        Table = new List<List<string>>
                        {
                            new List<string> { "id", "firstname", "lastname" },
                            new List<string> { "1", "george", "bush" }
                        },
                        EmbeddingConfiguration = new EmbeddingConfiguration { EmbeddingEndpointId = activeEp.Id },
                        ChunkingConfiguration = new ChunkingConfiguration { Strategy = "KeyValuePairs" }
                    };

                    SemanticCellResponse? result = await admin.ProcessAsync(req);
                    if (result == null || result.Chunks == null || result.Chunks.Count != 1) throw new Exception("Expected 1 chunk");
                });

                await RunTest("Process Table (WholeTable)", async () =>
                {
                    SkipIfProviderUnavailable();
                    EmbeddingEndpoint activeEp = await GetHarnessEmbeddingEndpointAsync(admin).ConfigureAwait(false);

                    SemanticCellRequest req = new SemanticCellRequest
                    {
                        Type = "Table",
                        Table = new List<List<string>>
                        {
                            new List<string> { "id", "firstname", "lastname" },
                            new List<string> { "1", "george", "bush" },
                            new List<string> { "2", "barack", "obama" }
                        },
                        EmbeddingConfiguration = new EmbeddingConfiguration { EmbeddingEndpointId = activeEp.Id },
                        ChunkingConfiguration = new ChunkingConfiguration { Strategy = "WholeTable" }
                    };

                    SemanticCellResponse? result = await admin.ProcessAsync(req);
                    if (result == null || result.Chunks == null || result.Chunks.Count != 1) throw new Exception("Expected 1 chunk");
                });

                await RunTest("Process Text (RegexBased)", async () =>
                {
                    SkipIfProviderUnavailable();
                    EmbeddingEndpoint activeEp = await GetHarnessEmbeddingEndpointAsync(admin).ConfigureAwait(false);

                    SemanticCellRequest req = new SemanticCellRequest
                    {
                        Type = "Text",
                        Text = "# Intro\nSome text.\n\n# Body\nMore text.\n\n# End\nFinal text.",
                        EmbeddingConfiguration = new EmbeddingConfiguration { EmbeddingEndpointId = activeEp.Id },
                        ChunkingConfiguration = new ChunkingConfiguration
                        {
                            Strategy = "RegexBased",
                            RegexPattern = @"(?=^#{1,3}\s)",
                            FixedTokenCount = 512
                        }
                    };

                    SemanticCellResponse? result = await admin.ProcessAsync(req);
                    if (result == null) throw new Exception("No response");
                    if (result.Chunks == null || result.Chunks.Count == 0) throw new Exception("No chunks");
                });

                await RunTest("Regex Strategy Missing Pattern (400)", async () =>
                {
                    SkipIfProviderUnavailable();
                    EmbeddingEndpoint activeEp = await GetHarnessEmbeddingEndpointAsync(admin).ConfigureAwait(false);

                    try
                    {
                        SemanticCellRequest req = new SemanticCellRequest
                        {
                            Type = "Text",
                            Text = "Some text here.",
                            EmbeddingConfiguration = new EmbeddingConfiguration { EmbeddingEndpointId = activeEp.Id },
                            ChunkingConfiguration = new ChunkingConfiguration { Strategy = "RegexBased" }
                        };
                        await admin.ProcessAsync(req);
                        throw new Exception("Expected 400");
                    }
                    catch (PartioException ex) when (ex.StatusCode == 400)
                    {
                        // Expected
                    }
                });

                await RunTest("Table Strategy on Text (400)", async () =>
                {
                    SkipIfProviderUnavailable();
                    EmbeddingEndpoint activeEp = await GetHarnessEmbeddingEndpointAsync(admin).ConfigureAwait(false);

                    try
                    {
                        SemanticCellRequest req = new SemanticCellRequest
                        {
                            Type = "Text",
                            Text = "This is text, not a table.",
                            EmbeddingConfiguration = new EmbeddingConfiguration { EmbeddingEndpointId = activeEp.Id },
                            ChunkingConfiguration = new ChunkingConfiguration { Strategy = "Row" }
                        };
                        await admin.ProcessAsync(req);
                        throw new Exception("Expected 400");
                    }
                    catch (PartioException ex) when (ex.StatusCode == 400)
                    {
                        // Expected
                    }
                });

                // Error cases
                await RunTest("Unauthenticated Request (401)", async () =>
                {
                    using (PartioClient noAuth = new PartioClient(_Endpoint, "invalid-token"))
                    {
                        try
                        {
                            await noAuth.EnumerateTenantsAsync();
                            throw new Exception("Expected 401");
                        }
                        catch (PartioException ex) when (ex.StatusCode == 401)
                        {
                            // Expected
                        }
                    }
                });

                await RunTest("Non-existent Resource (404)", async () =>
                {
                    try
                    {
                        await admin.GetTenantAsync("nonexistent-id-12345");
                        throw new Exception("Expected 404");
                    }
                    catch (PartioException ex) when (ex.StatusCode == 404)
                    {
                        // Expected
                    }
                });

                // Cleanup
                await RunTest("Delete Completion Endpoint", async () =>
                {
                    await admin.DeleteCompletionEndpointAsync(testCepId);
                    bool exists = await admin.CompletionEndpointExistsAsync(testCepId);
                    if (exists) throw new Exception("Completion endpoint still exists after delete");
                });

                await RunTest("Delete Gemini Completion Endpoint", async () =>
                {
                    if (!string.IsNullOrEmpty(geminiCepId)) await admin.DeleteCompletionEndpointAsync(geminiCepId);
                });

                await RunTest("Delete vLLM Completion Endpoint", async () =>
                {
                    if (!string.IsNullOrEmpty(vllmCepId)) await admin.DeleteCompletionEndpointAsync(vllmCepId);
                });

                await RunTest("Delete Endpoint", async () =>
                {
                    await admin.DeleteEndpointAsync(testEpId);
                    bool exists = await admin.EndpointExistsAsync(testEpId);
                    if (exists) throw new Exception("Endpoint still exists after delete");
                });

                await RunTest("Delete Gemini Embedding Endpoint", async () =>
                {
                    if (!string.IsNullOrEmpty(geminiEpId)) await admin.DeleteEndpointAsync(geminiEpId);
                });

                await RunTest("Delete vLLM Embedding Endpoint", async () =>
                {
                    if (!string.IsNullOrEmpty(vllmEpId)) await admin.DeleteEndpointAsync(vllmEpId);
                });

                await RunTest("Delete Credential", async () =>
                {
                    await admin.DeleteCredentialAsync(testCredId);
                });

                await RunTest("Delete User", async () =>
                {
                    await admin.DeleteUserAsync(testUserId);
                });

                await RunTest("Delete Tenant", async () =>
                {
                    await admin.DeleteTenantAsync(testTenantId);
                    bool exists = await admin.TenantExistsAsync(testTenantId);
                    if (exists) throw new Exception("Tenant still exists after delete");
                });
            }

            _TotalTimer.Stop();

            // Summary
            Console.WriteLine();
            Console.WriteLine("=== SUMMARY ===");
            Console.WriteLine($"Total: {_Passed + _Failed + _Skipped}  Passed: {_Passed}  Failed: {_Failed}  Skipped: {_Skipped}");
            Console.WriteLine($"Runtime: {_TotalTimer.ElapsedMilliseconds}ms");
            Console.WriteLine($"Result: {(_Failed == 0 ? "PASS" : "FAIL")}");

            if (_FailedTests.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Failed tests:");
                foreach (string name in _FailedTests)
                    Console.WriteLine("  - " + name);
            }

            Console.WriteLine("================");
            Environment.Exit(_Failed == 0 ? 0 : 1);
        }

        private static async Task RunTest(string name, Func<Task> test)
        {
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                await test();
                sw.Stop();
                Console.WriteLine($"  PASS  {name} ({sw.ElapsedMilliseconds}ms)");
                _Passed++;
            }
            catch (SkipTestException ex)
            {
                sw.Stop();
                Console.WriteLine($"  SKIP  {name} ({sw.ElapsedMilliseconds}ms) - {ex.Message}");
                _Skipped++;
            }
            catch (Exception ex)
            {
                sw.Stop();
                Console.WriteLine($"  FAIL  {name} ({sw.ElapsedMilliseconds}ms) - {ex.Message}");
                _Failed++;
                _FailedTests.Add(name);
            }
        }

        private static void SkipIfProviderUnavailable()
        {
            if (!_ProviderAvailable)
                throw new SkipTestException("requires reachable Ollama-compatible provider at " + _ProviderEndpoint);
        }

        private static async Task<bool> IsProviderAvailableAsync(string endpoint)
        {
            try
            {
                using HttpClient http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(2);
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint.TrimEnd('/') + "/api/tags");
                if (!string.IsNullOrEmpty(_ProviderBearer))
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _ProviderBearer);
                using HttpResponseMessage response = await http.SendAsync(request).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private sealed class SkipTestException : Exception
        {
            public SkipTestException(string message) : base(message)
            {
            }
        }
    }
}
