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

            _ProviderEndpoint = Environment.GetEnvironmentVariable("PARTIO_TEST_PROVIDER_ENDPOINT") ?? _ProviderEndpoint;
            _EmbeddingModel = Environment.GetEnvironmentVariable("PARTIO_TEST_EMBEDDING_MODEL") ?? _EmbeddingModel;
            _CompletionModel = Environment.GetEnvironmentVariable("PARTIO_TEST_COMPLETION_MODEL") ?? _CompletionModel;
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
                    testEpId = ep.Id ?? string.Empty;
                });

                await RunTest("Read Endpoint", async () =>
                {
                    EmbeddingEndpoint? ep = await admin.GetEndpointAsync(testEpId);
                    if (ep == null || ep.Model != _EmbeddingModel) throw new Exception("Endpoint mismatch");
                    if (ep.MaximumTimeoutMs != 61000) throw new Exception("MaximumTimeoutMs mismatch");
                    if (ep.MaxConcurrentRequests != 3) throw new Exception("MaxConcurrentRequests mismatch");
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
                    CompletionEndpoint? cep = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint { TenantId = testTenantId, Name = "Test Inference", Model = _CompletionModel, Endpoint = _ProviderEndpoint, ApiFormat = "Ollama", HealthCheckEnabled = false, MaximumTimeoutMs = 61000, MaxConcurrentRequests = 3 });
                    if (cep == null) throw new Exception("No response");
                    if (cep.MaximumTimeoutMs != 61000) throw new Exception("MaximumTimeoutMs mismatch");
                    if (cep.MaxConcurrentRequests != 3) throw new Exception("MaxConcurrentRequests mismatch");
                    testCepId = cep.Id ?? string.Empty;
                });

                await RunTest("Read Completion Endpoint", async () =>
                {
                    CompletionEndpoint? cep = await admin.GetCompletionEndpointAsync(testCepId);
                    if (cep == null || cep.Model != _CompletionModel) throw new Exception("Endpoint mismatch");
                    if (cep.MaximumTimeoutMs != 61000) throw new Exception("MaximumTimeoutMs mismatch");
                    if (cep.MaxConcurrentRequests != 3) throw new Exception("MaxConcurrentRequests mismatch");
                });

                await RunTest("Update Completion Endpoint", async () =>
                {
                    CompletionEndpoint? updated = await admin.UpdateCompletionEndpointAsync(testCepId, new CompletionEndpoint { TenantId = testTenantId, Name = "Updated Inference", Model = _CompletionModel, Endpoint = _ProviderEndpoint, ApiFormat = "Ollama", HealthCheckEnabled = false, MaximumTimeoutMs = 91000, MaxConcurrentRequests = 5 });
                    if (updated == null) throw new Exception("Update failed");
                    if (updated.MaximumTimeoutMs != 91000) throw new Exception("MaximumTimeoutMs mismatch");
                    if (updated.MaxConcurrentRequests != 5) throw new Exception("MaxConcurrentRequests mismatch");
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
                using HttpResponseMessage response = await http.GetAsync(endpoint.TrimEnd('/') + "/api/tags").ConfigureAwait(false);
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
