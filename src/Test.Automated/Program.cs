namespace Test.Automated
{
    using System.Diagnostics;
    using Partio.Sdk;
    using Partio.Sdk.Models;

    class Program
    {
        private static string _Endpoint = "http://localhost:8400";
        private static string _AdminKey = "partioadmin";
        private static string _TestToken = "default";
        private static int _Passed = 0;
        private static int _Failed = 0;
        private static List<string> _FailedTests = new List<string>();
        private static Stopwatch _TotalTimer = new Stopwatch();

        static async Task Main(string[] args)
        {
            if (args.Length >= 1) _Endpoint = args[0];
            if (args.Length >= 2) _AdminKey = args[1];
            if (args.Length >= 3) _TestToken = args[2];

            Console.WriteLine("Partio Automated Test Suite");
            Console.WriteLine("Endpoint: " + _Endpoint);
            Console.WriteLine("Admin Key: " + _AdminKey);
            Console.WriteLine("Test Token: " + _TestToken);
            Console.WriteLine();

            _TotalTimer.Start();

            using (PartioClient admin = new PartioClient(_Endpoint, _AdminKey))
            using (PartioClient user = new PartioClient(_Endpoint, _TestToken))
            {
                // ===== Health =====

                await RunTest("Health Check GET /", async () =>
                {
                    Dictionary<string, string>? result = await admin.HealthAsync();
                    if (result == null || !result.ContainsKey("Status")) throw new Exception("No health response");
                    if (result["Status"] != "Healthy") throw new Exception("Not healthy: " + result["Status"]);
                });

                // ===== Tenant CRUD =====

                string testTenantId = "";
                await RunTest("Create Tenant", async () =>
                {
                    TenantMetadata? tenant = await admin.CreateTenantAsync(new TenantMetadata
                    {
                        Name = "Automated Test Tenant",
                        Labels = new List<string> { "automated", "test" },
                        Tags = new Dictionary<string, string> { { "env", "test" } }
                    });
                    if (tenant == null || string.IsNullOrEmpty(tenant.Id)) throw new Exception("No tenant returned");
                    testTenantId = tenant.Id;
                });

                await RunTest("Read Tenant", async () =>
                {
                    TenantMetadata? tenant = await admin.GetTenantAsync(testTenantId);
                    if (tenant == null) throw new Exception("Tenant not found");
                    if (tenant.Name != "Automated Test Tenant") throw new Exception("Name mismatch: " + tenant.Name);
                });

                await RunTest("Update Tenant", async () =>
                {
                    TenantMetadata? updated = await admin.UpdateTenantAsync(testTenantId, new TenantMetadata
                    {
                        Name = "Updated Test Tenant"
                    });
                    if (updated == null || updated.Name != "Updated Test Tenant") throw new Exception("Update failed");
                });

                await RunTest("Tenant Exists (HEAD)", async () =>
                {
                    bool exists = await admin.TenantExistsAsync(testTenantId);
                    if (!exists) throw new Exception("Tenant should exist");
                });

                await RunTest("Enumerate Tenants", async () =>
                {
                    EnumerationResult<TenantMetadata>? result = await admin.EnumerateTenantsAsync(new EnumerationRequest { MaxResults = 10 });
                    if (result == null || result.Data.Count == 0) throw new Exception("No tenants returned");
                });

                // ===== User CRUD =====

                string testUserId = "";
                await RunTest("Create User", async () =>
                {
                    UserMaster? userResult = await admin.CreateUserAsync(new UserMaster
                    {
                        TenantId = testTenantId,
                        Email = "autotest@partio.test",
                        Password = "TestPassword123",
                        FirstName = "Auto",
                        LastName = "Tester",
                        IsAdmin = false
                    });
                    if (userResult == null || string.IsNullOrEmpty(userResult.Id)) throw new Exception("No user returned");
                    testUserId = userResult.Id;
                });

                await RunTest("Read User", async () =>
                {
                    UserMaster? userResult = await admin.GetUserAsync(testUserId);
                    if (userResult == null) throw new Exception("User not found");
                    if (userResult.Email != "autotest@partio.test") throw new Exception("Email mismatch");
                });

                await RunTest("Update User", async () =>
                {
                    UserMaster? updated = await admin.UpdateUserAsync(testUserId, new UserMaster
                    {
                        TenantId = testTenantId,
                        Email = "autotest-updated@partio.test",
                        FirstName = "Updated"
                    });
                    if (updated == null) throw new Exception("Update failed");
                });

                await RunTest("User Exists (HEAD)", async () =>
                {
                    bool exists = await admin.UserExistsAsync(testUserId);
                    if (!exists) throw new Exception("User should exist");
                });

                await RunTest("Enumerate Users", async () =>
                {
                    EnumerationResult<UserMaster>? result = await admin.EnumerateUsersAsync(new EnumerationRequest { MaxResults = 10 });
                    if (result == null || result.Data.Count == 0) throw new Exception("No users returned");
                });

                // ===== Credential CRUD =====

                string testCredId = "";
                string testCredToken = "";
                await RunTest("Create Credential", async () =>
                {
                    Credential? cred = await admin.CreateCredentialAsync(new Credential
                    {
                        TenantId = testTenantId,
                        UserId = testUserId,
                        Name = "Automated Test Key"
                    });
                    if (cred == null || string.IsNullOrEmpty(cred.Id)) throw new Exception("No credential returned");
                    testCredId = cred.Id;
                    testCredToken = cred.BearerToken;
                });

                await RunTest("Read Credential", async () =>
                {
                    Credential? cred = await admin.GetCredentialAsync(testCredId);
                    if (cred == null) throw new Exception("Credential not found");
                    if (cred.Name != "Automated Test Key") throw new Exception("Name mismatch");
                });

                await RunTest("Update Credential", async () =>
                {
                    Credential? updated = await admin.UpdateCredentialAsync(testCredId, new Credential
                    {
                        TenantId = testTenantId,
                        UserId = testUserId,
                        Name = "Updated Test Key"
                    });
                    if (updated == null) throw new Exception("Update failed");
                });

                await RunTest("Credential Exists (HEAD)", async () =>
                {
                    bool exists = await admin.CredentialExistsAsync(testCredId);
                    if (!exists) throw new Exception("Credential should exist");
                });

                await RunTest("Enumerate Credentials", async () =>
                {
                    EnumerationResult<Credential>? result = await admin.EnumerateCredentialsAsync(new EnumerationRequest { MaxResults = 10 });
                    if (result == null || result.Data.Count == 0) throw new Exception("No credentials returned");
                });

                await RunTest("Authenticate with New Credential", async () =>
                {
                    using (PartioClient credClient = new PartioClient(_Endpoint, testCredToken))
                    {
                        Dictionary<string, string>? result = await credClient.HealthAsync();
                        if (result == null || result["Status"] != "Healthy") throw new Exception("Health check failed with new cred");
                    }
                });

                // ===== Embedding Endpoint CRUD =====

                string testEpId = "";
                await RunTest("Create Embedding Endpoint", async () =>
                {
                    EmbeddingEndpoint? ep = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                    {
                        TenantId = testTenantId,
                        Model = "test-model",
                        Endpoint = "http://localhost:11434",
                        ApiFormat = "Ollama"
                    });
                    if (ep == null || string.IsNullOrEmpty(ep.Id)) throw new Exception("No endpoint returned");
                    testEpId = ep.Id;
                });

                await RunTest("Read Embedding Endpoint", async () =>
                {
                    EmbeddingEndpoint? ep = await admin.GetEndpointAsync(testEpId);
                    if (ep == null) throw new Exception("Endpoint not found");
                    if (ep.Model != "test-model") throw new Exception("Model mismatch");
                });

                await RunTest("Update Embedding Endpoint", async () =>
                {
                    EmbeddingEndpoint? updated = await admin.UpdateEndpointAsync(testEpId, new EmbeddingEndpoint
                    {
                        TenantId = testTenantId,
                        Model = "test-model-v2",
                        Endpoint = "http://localhost:11434",
                        ApiFormat = "Ollama"
                    });
                    if (updated == null) throw new Exception("Update failed");
                });

                await RunTest("Endpoint Exists (HEAD)", async () =>
                {
                    bool exists = await admin.EndpointExistsAsync(testEpId);
                    if (!exists) throw new Exception("Endpoint should exist");
                });

                await RunTest("Enumerate Endpoints", async () =>
                {
                    EnumerationResult<EmbeddingEndpoint>? result = await admin.EnumerateEndpointsAsync(new EnumerationRequest { MaxResults = 10 });
                    if (result == null || result.Data.Count == 0) throw new Exception("No endpoints returned");
                });

                // ===== Request History =====

                await RunTest("Enumerate Request History", async () =>
                {
                    EnumerationResult<RequestHistoryEntry>? result = await admin.EnumerateRequestHistoryAsync(new EnumerationRequest { MaxResults = 10 });
                    if (result == null) throw new Exception("No response");
                });

                // ===== Process (RegexBased) =====

                await RunTest("Process Text (RegexBased - Markdown Headings)", async () =>
                {
                    EnumerationResult<EmbeddingEndpoint>? eps = await admin.EnumerateEndpointsAsync();
                    EmbeddingEndpoint? activeEp = eps?.Data?.FirstOrDefault(e => e.Active != false);
                    if (activeEp == null) throw new Exception("SKIP: no active embedding endpoint");

                    SemanticCellRequest req = new SemanticCellRequest
                    {
                        Type = "Text",
                        Text = "# A\nText about A.\n\n# B\nText about B.\n\n# C\nText about C.",
                        ChunkingConfiguration = new ChunkingConfiguration
                        {
                            Strategy = "RegexBased",
                            RegexPattern = @"(?=^#{1,3}\s)",
                            FixedTokenCount = 512
                        }
                    };

                    SemanticCellResponse? result = await admin.ProcessAsync(activeEp.Id, req);
                    if (result == null || result.Chunks == null || result.Chunks.Count != 3) throw new Exception("Expected 3 chunks, got " + (result?.Chunks?.Count ?? 0));
                });

                await RunTest("Process Text (RegexBased - Double Newline)", async () =>
                {
                    EnumerationResult<EmbeddingEndpoint>? eps = await admin.EnumerateEndpointsAsync();
                    EmbeddingEndpoint? activeEp = eps?.Data?.FirstOrDefault(e => e.Active != false);
                    if (activeEp == null) throw new Exception("SKIP: no active embedding endpoint");

                    SemanticCellRequest req = new SemanticCellRequest
                    {
                        Type = "Text",
                        Text = "First paragraph.\n\nSecond paragraph.\n\nThird paragraph.",
                        ChunkingConfiguration = new ChunkingConfiguration
                        {
                            Strategy = "RegexBased",
                            RegexPattern = @"\n\n+",
                            FixedTokenCount = 512
                        }
                    };

                    SemanticCellResponse? result = await admin.ProcessAsync(activeEp.Id, req);
                    if (result == null || result.Chunks == null || result.Chunks.Count != 3) throw new Exception("Expected 3 chunks, got " + (result?.Chunks?.Count ?? 0));
                });

                await RunTest("Process Text (RegexBased - Single Segment)", async () =>
                {
                    EnumerationResult<EmbeddingEndpoint>? eps = await admin.EnumerateEndpointsAsync();
                    EmbeddingEndpoint? activeEp = eps?.Data?.FirstOrDefault(e => e.Active != false);
                    if (activeEp == null) throw new Exception("SKIP: no active embedding endpoint");

                    SemanticCellRequest req = new SemanticCellRequest
                    {
                        Type = "Text",
                        Text = "No headings in this text whatsoever.",
                        ChunkingConfiguration = new ChunkingConfiguration
                        {
                            Strategy = "RegexBased",
                            RegexPattern = @"(?=^#{1,3}\s)",
                            FixedTokenCount = 512
                        }
                    };

                    SemanticCellResponse? result = await admin.ProcessAsync(activeEp.Id, req);
                    if (result == null || result.Chunks == null || result.Chunks.Count != 1) throw new Exception("Expected 1 chunk, got " + (result?.Chunks?.Count ?? 0));
                });

                await RunTest("Process Code (RegexBased)", async () =>
                {
                    EnumerationResult<EmbeddingEndpoint>? eps = await admin.EnumerateEndpointsAsync();
                    EmbeddingEndpoint? activeEp = eps?.Data?.FirstOrDefault(e => e.Active != false);
                    if (activeEp == null) throw new Exception("SKIP: no active embedding endpoint");

                    SemanticCellRequest req = new SemanticCellRequest
                    {
                        Type = "Code",
                        Text = "def foo():\n    pass\n\ndef bar():\n    pass\n\ndef baz():\n    pass",
                        ChunkingConfiguration = new ChunkingConfiguration
                        {
                            Strategy = "RegexBased",
                            RegexPattern = @"(?=^def\s)",
                            FixedTokenCount = 512
                        }
                    };

                    SemanticCellResponse? result = await admin.ProcessAsync(activeEp.Id, req);
                    if (result == null || result.Chunks == null || result.Chunks.Count == 0) throw new Exception("Expected chunks");
                });

                await RunTest("Regex Strategy Missing Pattern (400)", async () =>
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
                            ChunkingConfiguration = new ChunkingConfiguration { Strategy = "RegexBased" }
                        };
                        await admin.ProcessAsync(activeEp.Id, req);
                        throw new Exception("Expected PartioException with 400");
                    }
                    catch (PartioException ex) when (ex.StatusCode == 400)
                    {
                        // Expected
                    }
                });

                await RunTest("Regex Strategy Empty Pattern (400)", async () =>
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
                            ChunkingConfiguration = new ChunkingConfiguration { Strategy = "RegexBased", RegexPattern = "" }
                        };
                        await admin.ProcessAsync(activeEp.Id, req);
                        throw new Exception("Expected PartioException with 400");
                    }
                    catch (PartioException ex) when (ex.StatusCode == 400)
                    {
                        // Expected
                    }
                });

                await RunTest("Regex Strategy Invalid Pattern (400)", async () =>
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
                            ChunkingConfiguration = new ChunkingConfiguration { Strategy = "RegexBased", RegexPattern = "([" }
                        };
                        await admin.ProcessAsync(activeEp.Id, req);
                        throw new Exception("Expected PartioException with 400");
                    }
                    catch (PartioException ex) when (ex.StatusCode == 400)
                    {
                        // Expected
                    }
                });

                // ===== Error Cases =====

                await RunTest("Unauthenticated Request (401)", async () =>
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
                });

                await RunTest("Invalid Bearer Token (401)", async () =>
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
                });

                await RunTest("Non-existent Resource (404)", async () =>
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
                });

                // ===== Cleanup =====

                await RunTest("Delete Embedding Endpoint", async () =>
                {
                    await admin.DeleteEndpointAsync(testEpId);
                    bool exists = await admin.EndpointExistsAsync(testEpId);
                    if (exists) throw new Exception("Endpoint still exists after delete");
                });

                await RunTest("Delete Credential", async () =>
                {
                    await admin.DeleteCredentialAsync(testCredId);
                    bool exists = await admin.CredentialExistsAsync(testCredId);
                    if (exists) throw new Exception("Credential still exists after delete");
                });

                await RunTest("Delete User", async () =>
                {
                    await admin.DeleteUserAsync(testUserId);
                    bool exists = await admin.UserExistsAsync(testUserId);
                    if (exists) throw new Exception("User still exists after delete");
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
            Console.WriteLine("Total: " + (_Passed + _Failed) + "  Passed: " + _Passed + "  Failed: " + _Failed);
            Console.WriteLine("Runtime: " + _TotalTimer.ElapsedMilliseconds + "ms");
            Console.WriteLine("Result: " + (_Failed == 0 ? "PASS" : "FAIL"));

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
                await test().ConfigureAwait(false);
                sw.Stop();
                Console.WriteLine("  PASS  " + name + " (" + sw.ElapsedMilliseconds + "ms)");
                _Passed++;
            }
            catch (Exception ex)
            {
                sw.Stop();
                Console.WriteLine("  FAIL  " + name + " (" + sw.ElapsedMilliseconds + "ms) - " + ex.Message);
                _Failed++;
                _FailedTests.Add(name);
            }
        }
    }
}
