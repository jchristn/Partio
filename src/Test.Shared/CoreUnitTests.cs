namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Partio.Core;
    using Partio.Core.Chunking;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using Partio.Core.Proxy;
    using Partio.Core.Serialization;
    using Partio.Core.ThirdParty;
    using Partio.Core.Tokenization;
    using SyslogLogging;
    using Touchstone.Core;

    /// <summary>
    /// Pure, offline unit tests for server-independent Partio.Core building blocks: identifier
    /// generation, user password hashing/redaction, JSON serialization round-tripping, and the
    /// chunking engine. These require neither a Partio server nor any network access.
    /// </summary>
    public static class CoreUnitTests
    {
        /// <summary>
        /// Build the Touchstone suite of core library unit tests.
        /// </summary>
        /// <returns>A suite descriptor exposing every case.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                "Core",
                "Partio.Core building-block unit tests",
                BuildCases());
        }

        private static List<TestCaseDescriptor> BuildCases()
        {
            List<TestCaseDescriptor> tests = new List<TestCaseDescriptor>();

            // ===== IdGenerator =====

            tests.Add(TestCaseFactory.Sync("Core", "IdGenerator: identifiers carry their semantic prefixes", () =>
            {
                Check.True(IdGenerator.NewTenantId().StartsWith("ten_", StringComparison.Ordinal), "Tenant id prefix");
                Check.True(IdGenerator.NewUserId().StartsWith("usr_", StringComparison.Ordinal), "User id prefix");
                Check.True(IdGenerator.NewCredentialId().StartsWith("cred_", StringComparison.Ordinal), "Credential id prefix");
                Check.True(IdGenerator.NewEmbeddingEndpointId().StartsWith("eep_", StringComparison.Ordinal), "Embedding endpoint id prefix");
                Check.True(IdGenerator.NewCompletionEndpointId().StartsWith("cep_", StringComparison.Ordinal), "Completion endpoint id prefix");
                Check.True(IdGenerator.NewRequestHistoryId().StartsWith("req_", StringComparison.Ordinal), "Request history id prefix");
            }));

            tests.Add(TestCaseFactory.Sync("Core", "IdGenerator: successive identifiers are unique", () =>
            {
                HashSet<string> generated = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < 100; i++)
                {
                    if (!generated.Add(IdGenerator.NewTenantId()))
                        throw new Exception("Duplicate tenant id generated.");
                }
            }));

            // ===== SDK model timestamps =====

            tests.Add(TestCaseFactory.Sync("Core", "SDK models default timestamps to UtcNow, never DateTime.MinValue", () =>
            {
                DateTime before = DateTime.UtcNow;
                List<SdkModelTimestamps> models = new List<SdkModelTimestamps>();

                Partio.Sdk.Models.TenantMetadata tenant = new Partio.Sdk.Models.TenantMetadata();
                models.Add(new SdkModelTimestamps("TenantMetadata", tenant, tenant.CreatedUtc, tenant.LastUpdateUtc));
                Partio.Sdk.Models.UserMaster user = new Partio.Sdk.Models.UserMaster();
                models.Add(new SdkModelTimestamps("UserMaster", user, user.CreatedUtc, user.LastUpdateUtc));
                Partio.Sdk.Models.Credential credential = new Partio.Sdk.Models.Credential();
                models.Add(new SdkModelTimestamps("Credential", credential, credential.CreatedUtc, credential.LastUpdateUtc));
                Partio.Sdk.Models.EmbeddingEndpoint embedding = new Partio.Sdk.Models.EmbeddingEndpoint();
                models.Add(new SdkModelTimestamps("EmbeddingEndpoint", embedding, embedding.CreatedUtc, embedding.LastUpdateUtc));
                Partio.Sdk.Models.CompletionEndpoint completion = new Partio.Sdk.Models.CompletionEndpoint();
                models.Add(new SdkModelTimestamps("CompletionEndpoint", completion, completion.CreatedUtc, completion.LastUpdateUtc));

                DateTime after = DateTime.UtcNow;
                foreach (SdkModelTimestamps entry in models)
                {
                    string name = entry.Name;
                    object model = entry.Model;
                    DateTime created = entry.Created;
                    DateTime updated = entry.Updated;
                    Check.True(created >= before && created <= after, name + ".CreatedUtc should default to UtcNow but was " + created.ToString("o"));
                    Check.True(updated >= before && updated <= after, name + ".LastUpdateUtc should default to UtcNow but was " + updated.ToString("o"));
                    Check.Equal(DateTimeKind.Utc, created.Kind, name + ".CreatedUtc should be UTC.");

                    string json = System.Text.Json.JsonSerializer.Serialize(model, model.GetType());
                    Check.False(json.Contains("0001-01-01"), name + " must not serialize DateTime.MinValue: " + json);
                }
            }));

            tests.Add(TestCaseFactory.Sync("Core", "IdGenerator: identifiers are unique across all entity types", () =>
            {
                HashSet<string> generated = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < 50; i++)
                {
                    foreach (string id in new[]
                    {
                        IdGenerator.NewTenantId(),
                        IdGenerator.NewUserId(),
                        IdGenerator.NewCredentialId(),
                        IdGenerator.NewEmbeddingEndpointId(),
                        IdGenerator.NewCompletionEndpointId(),
                        IdGenerator.NewRequestHistoryId()
                    })
                    {
                        if (!generated.Add(id))
                            throw new Exception("Duplicate id generated across entity types: " + id);
                    }
                }
            }));

            tests.Add(TestCaseFactory.Sync("Core", "IdGenerator: bearer token uses only the alphanumeric alphabet", () =>
            {
                const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
                for (int i = 0; i < 20; i++)
                {
                    string token = IdGenerator.NewBearerToken();
                    Check.Equal(64, token.Length);
                    Check.All(token, c => Check.True(alphabet.IndexOf(c) >= 0, "Unexpected character in bearer token: '" + c + "'"));
                }
            }));

            tests.Add(TestCaseFactory.Sync("Core", "IdGenerator: bearer token is 64 alphanumeric characters", () =>
            {
                string token = IdGenerator.NewBearerToken();
                Check.Equal(64, token.Length);
                Check.True(token.All(char.IsLetterOrDigit), "Bearer token should be alphanumeric.");
                Check.False(string.Equals(token, IdGenerator.NewBearerToken(), StringComparison.Ordinal), "Bearer tokens should differ.");
            }));

            // ===== UserMaster password handling =====

            tests.Add(TestCaseFactory.Sync("Core", "UserMaster: correct password verifies", () =>
            {
                UserMaster user = new UserMaster();
                user.SetPassword("Sup3rSecret!");
                Check.True(user.VerifyPassword("Sup3rSecret!"));
            }));

            tests.Add(TestCaseFactory.Sync("Core", "UserMaster: wrong password does not verify", () =>
            {
                UserMaster user = new UserMaster();
                user.SetPassword("Sup3rSecret!");
                Check.False(user.VerifyPassword("wrong-password"));
                Check.False(user.VerifyPassword(string.Empty));
            }));

            tests.Add(TestCaseFactory.Sync("Core", "UserMaster: ComputePasswordHash is deterministic 64-char lowercase hex", () =>
            {
                string first = UserMaster.ComputePasswordHash("password");
                string second = UserMaster.ComputePasswordHash("password");
                Check.Equal(first, second);
                Check.Equal(64, first.Length);
                Check.True(first.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')), "Hash should be lowercase hex.");
                Check.False(string.Equals(first, UserMaster.ComputePasswordHash("Password"), StringComparison.Ordinal), "Hash should be case-sensitive on input.");
            }));

            tests.Add(TestCaseFactory.Sync("Core", "UserMaster: Redact masks the password and preserves identity", () =>
            {
                UserMaster user = new UserMaster
                {
                    Id = "usr_test",
                    TenantId = "ten_test",
                    Email = "person@partio.test",
                    FirstName = "Given",
                    LastName = "Family"
                };
                user.SetPassword("Sup3rSecret!");

                UserMaster redacted = UserMaster.Redact(user);
                Check.Equal("********", redacted.PasswordSha256);
                Check.Equal("person@partio.test", redacted.Email);
                Check.Equal("usr_test", redacted.Id);
                Check.False(user.PasswordSha256 == "********", "Original user should be untouched.");
            }));

            tests.Add(TestCaseFactory.Sync("Core", "UserMaster: Redact rejects null", () =>
            {
                try
                {
                    UserMaster.Redact(null!);
                    throw new Exception("Expected ArgumentNullException.");
                }
                catch (ArgumentNullException)
                {
                    // Expected
                }
            }));

            tests.Add(TestCaseFactory.Sync("Core", "UserMaster: Redact copies labels and tags without aliasing the original", () =>
            {
                UserMaster user = new UserMaster
                {
                    TenantId = "ten_test",
                    Email = "person@partio.test",
                    Labels = new List<string> { "alpha" },
                    Tags = new Dictionary<string, string> { { "env", "test" } }
                };
                user.SetPassword("Sup3rSecret!");

                UserMaster redacted = UserMaster.Redact(user);
                redacted.Labels.Add("beta");
                redacted.Tags["env"] = "mutated";

                Check.Single(user.Labels);
                Check.Equal("test", user.Tags["env"]);
            }));

            tests.Add(TestCaseFactory.Sync("Core", "UserMaster: SetPassword rejects null or empty input", () =>
            {
                UserMaster user = new UserMaster();
                try
                {
                    user.SetPassword(string.Empty);
                    throw new Exception("Expected ArgumentException for empty password.");
                }
                catch (ArgumentException)
                {
                    // Expected
                }

                try
                {
                    user.SetPassword(null!);
                    throw new Exception("Expected ArgumentException for null password.");
                }
                catch (ArgumentException)
                {
                    // Expected
                }
            }));

            tests.Add(TestCaseFactory.Sync("Core", "UserMaster: VerifyPassword is false when no password has been set", () =>
            {
                UserMaster user = new UserMaster();
                Check.False(user.VerifyPassword("anything"));
                Check.False(user.VerifyPassword(string.Empty));
            }));

            tests.Add(TestCaseFactory.Sync("Core", "UserMaster: Email setter rejects null or empty", () =>
            {
                UserMaster user = new UserMaster();
                try
                {
                    user.Email = string.Empty;
                    throw new Exception("Expected ArgumentException for empty email.");
                }
                catch (ArgumentException)
                {
                    // Expected
                }

                try
                {
                    user.Email = null!;
                    throw new Exception("Expected ArgumentException for null email.");
                }
                catch (ArgumentException)
                {
                    // Expected
                }
            }));

            // ===== Serialization round-trip =====

            tests.Add(TestCaseFactory.Sync("Core", "PartioSerializer: embedding endpoint round-trips through JSON", () =>
            {
                PartioSerializer serializer = new PartioSerializer();
                EmbeddingEndpoint original = new EmbeddingEndpoint
                {
                    Id = "eep_serialize",
                    TenantId = "default",
                    Name = "Round Trip",
                    Model = "text-embedding-3-small",
                    Endpoint = "https://api.openai.com",
                    ApiFormat = ApiFormatEnum.OpenAI,
                    MaximumTimeoutMs = 45000,
                    MaxConcurrentRequests = 4,
                    MaxQueueDepth = 4
                };

                string json = serializer.SerializeJson(original, false);
                Check.NotNull(json);
                Check.Contains("text-embedding-3-small", json);

                EmbeddingEndpoint restored = serializer.DeserializeJson<EmbeddingEndpoint>(json);
                Check.NotNull(restored);
                Check.Equal(original.Model, restored.Model);
                Check.Equal(original.MaximumTimeoutMs, restored.MaximumTimeoutMs);
                Check.Equal(original.MaxConcurrentRequests, restored.MaxConcurrentRequests);
                Check.Equal(original.MaxQueueDepth, restored.MaxQueueDepth);
                Check.Equal(ApiFormatEnum.OpenAI, restored.ApiFormat);
            }));

            tests.Add(TestCaseFactory.Sync("Core", "PartioSerializer: completion endpoint round-trips through JSON", () =>
            {
                PartioSerializer serializer = new PartioSerializer();
                CompletionEndpoint original = new CompletionEndpoint
                {
                    Id = "cep_serialize",
                    TenantId = "default",
                    Name = "Round Trip Inference",
                    Model = "gpt-4.1-mini",
                    Endpoint = "https://api.openai.com",
                    ApiFormat = ApiFormatEnum.OpenAI,
                    MaximumTimeoutMs = 90000,
                    MaxConcurrentRequests = 5,
                    MaxQueueDepth = 7,
                    Labels = new List<string> { "prod" },
                    Tags = new Dictionary<string, string> { { "region", "us-east" } }
                };

                string json = serializer.SerializeJson(original, true);
                Check.NotNull(json);
                Check.Contains("gpt-4.1-mini", json);

                CompletionEndpoint restored = serializer.DeserializeJson<CompletionEndpoint>(json);
                Check.NotNull(restored);
                Check.Equal(original.Id, restored.Id);
                Check.Equal(original.Model, restored.Model);
                Check.Equal(original.MaximumTimeoutMs, restored.MaximumTimeoutMs);
                Check.Equal(original.MaxConcurrentRequests, restored.MaxConcurrentRequests);
                Check.Equal(original.MaxQueueDepth, restored.MaxQueueDepth);
                Check.Equal(ApiFormatEnum.OpenAI, restored.ApiFormat);
                Check.Contains("prod", restored.Labels);
                Check.Equal("us-east", restored.Tags["region"]);
            }));

            tests.Add(TestCaseFactory.Sync("Core", "PartioSerializer: deserializing malformed JSON throws", () =>
            {
                PartioSerializer serializer = new PartioSerializer();
                bool threw = false;
                try
                {
                    serializer.DeserializeJson<EmbeddingEndpoint>("{ this is not valid json ");
                }
                catch
                {
                    // Expected: the serializer surfaces a parse failure rather than returning silently.
                    threw = true;
                }

                Check.True(threw, "Expected deserialization of malformed JSON to throw.");
            }));

            // ===== Chunking engine =====

            tests.Add(TestCaseFactory.Sync("Core", "ChunkingEngine: short in-budget text yields a single chunk", () =>
            {
                ITokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                SemanticCellRequest request = new SemanticCellRequest
                {
                    Type = AtomTypeEnum.Text,
                    Text = "A short sentence that fits comfortably within the budget.",
                    ChunkingConfiguration = new ChunkingConfiguration
                    {
                        Strategy = ChunkStrategyEnum.FixedTokenCount,
                        FixedTokenCount = 512
                    }
                };

                ChunkingEngine engine = new ChunkingEngine(CreateLogging());
                List<ChunkResult> chunks = engine.Chunk(request, tokenizer, 512);

                Check.Single(chunks);
                Check.Contains("short sentence", chunks[0].Text);
            }));

            tests.Add(TestCaseFactory.Sync("Core", "ChunkingEngine: oversized fixed-token text splits into in-budget chunks", () =>
            {
                ITokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                int budget = 16;
                string text = string.Join(" ", Enumerable.Repeat("tokenization budget exceeded verification sentence", 20));
                SemanticCellRequest request = new SemanticCellRequest
                {
                    Type = AtomTypeEnum.Text,
                    Text = text,
                    ChunkingConfiguration = new ChunkingConfiguration
                    {
                        Strategy = ChunkStrategyEnum.FixedTokenCount,
                        FixedTokenCount = budget
                    }
                };

                ChunkingEngine engine = new ChunkingEngine(CreateLogging());
                List<ChunkResult> chunks = engine.Chunk(request, tokenizer, budget);

                Check.True(chunks.Count >= 2, "Expected multiple chunks for oversized text.");
                Check.All(chunks, chunk => Check.True(tokenizer.CountTokens(chunk.Text) <= budget, "Chunk exceeded budget."));
            }));

            // ===== ProxyPathPolicy =====

            tests.Add(TestCaseFactory.Sync("Core", "ProxyPathPolicy: normalizes surrounding slashes and whitespace", () =>
            {
                Check.True(ProxyPathPolicy.Normalize("/v1/chat/completions/") == "v1/chat/completions", "Trim slashes");
                Check.True(ProxyPathPolicy.Normalize("  api/chat  ") == "api/chat", "Trim whitespace");
                Check.True(ProxyPathPolicy.Normalize(null) == string.Empty, "Null -> empty");
                Check.True(ProxyPathPolicy.Normalize("") == string.Empty, "Empty -> empty");
            }));

            tests.Add(TestCaseFactory.Sync("Core", "ProxyPathPolicy: OpenAI and vLLM allow only v1 completion paths", () =>
            {
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.OpenAI, "v1/chat/completions"), "OpenAI chat");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.OpenAI, "/v1/embeddings/"), "OpenAI embeddings (slashes)");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.OpenAI, "v1/models"), "OpenAI models");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.OpenAI, "v1/models/gpt-4.1-mini"), "OpenAI model by id");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.vLLM, "v1/chat/completions"), "vLLM chat");
                Check.True(!ProxyPathPolicy.IsAllowed(ApiFormatEnum.OpenAI, "api/chat"), "OpenAI rejects Ollama path");
                Check.True(!ProxyPathPolicy.IsAllowed(ApiFormatEnum.OpenAI, "v1/../secrets"), "OpenAI rejects traversal-like path");
            }));

            tests.Add(TestCaseFactory.Sync("Core", "ProxyPathPolicy: Ollama allows only api sub-paths", () =>
            {
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.Ollama, "api/chat"), "Ollama chat");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.Ollama, "api/generate"), "Ollama generate");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.Ollama, "api/tags"), "Ollama tags");
                Check.True(!ProxyPathPolicy.IsAllowed(ApiFormatEnum.Ollama, "v1/chat/completions"), "Ollama rejects OpenAI path");
                Check.True(!ProxyPathPolicy.IsAllowed(ApiFormatEnum.Ollama, ""), "Ollama rejects empty");
            }));

            tests.Add(TestCaseFactory.Sync("Core", "ProxyPathPolicy: Gemini allows v1beta model paths by prefix", () =>
            {
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.Gemini, "v1beta/models"), "Gemini models list");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.Gemini, "v1beta/models/gemini-1.5-flash:generateContent"), "Gemini generate");
                Check.True(!ProxyPathPolicy.IsAllowed(ApiFormatEnum.Gemini, "v1/chat/completions"), "Gemini rejects OpenAI path");
            }));

            tests.Add(TestCaseFactory.Sync("Core", "ProxyPathPolicy: builds upstream URL joining base, sub-path, and query", () =>
            {
                Check.True(ProxyPathPolicy.BuildUpstreamUrl("http://host:11434/", "/api/chat", null) == "http://host:11434/api/chat", "Join + trim");
                Check.True(ProxyPathPolicy.BuildUpstreamUrl("http://host:11434", "api/chat", "?stream=true") == "http://host:11434/api/chat?stream=true", "Query with '?'");
                Check.True(ProxyPathPolicy.BuildUpstreamUrl("http://host", "v1/models", "limit=5") == "http://host/v1/models?limit=5", "Query without '?'");
            }));

            tests.Add(TestCaseFactory.Sync("Core", "ProxyPathPolicy: resolves the correct upstream auth header per dialect", () =>
            {
                ProxyAuthHeader? openai = ProxyPathPolicy.ResolveAuthHeader(ApiFormatEnum.OpenAI, "sk-123");
                Check.True(openai != null && openai.Value.Name == "Authorization" && openai.Value.Value == "Bearer sk-123", "OpenAI bearer");
                ProxyAuthHeader? gemini = ProxyPathPolicy.ResolveAuthHeader(ApiFormatEnum.Gemini, "g-123");
                Check.True(gemini != null && gemini.Value.Name == "x-goog-api-key" && gemini.Value.Value == "g-123", "Gemini key header");
                Check.True(ProxyPathPolicy.ResolveAuthHeader(ApiFormatEnum.Ollama, null) == null, "No key -> no header");
                Check.True(ProxyPathPolicy.ResolveAuthHeader(ApiFormatEnum.Ollama, "") == null, "Empty key -> no header");
            }));

            tests.Add(TestCaseFactory.Sync("Core", "ProxyPathPolicy: Ollama Cloud shares the Ollama api allow-list", () =>
            {
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.OllamaCloud, "api/chat"), "Ollama Cloud chat");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.OllamaCloud, "api/embed"), "Ollama Cloud embed");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.OllamaCloud, "api/tags"), "Ollama Cloud tags");
                Check.True(!ProxyPathPolicy.IsAllowed(ApiFormatEnum.OllamaCloud, "v1/chat/completions"), "Ollama Cloud rejects OpenAI path");
                Check.Equal(ProxyPathPolicy.DescribeAllowed(ApiFormatEnum.Ollama), ProxyPathPolicy.DescribeAllowed(ApiFormatEnum.OllamaCloud));
            }));

            tests.Add(TestCaseFactory.Sync("Core", "ProxyPathPolicy: xAI allows Responses, chat, embeddings, and model catalog paths", () =>
            {
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.xAI, "v1/responses"), "xAI responses");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.xAI, "v1/chat/completions"), "xAI chat completions");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.xAI, "v1/embeddings"), "xAI embeddings");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.xAI, "v1/models/grok-4"), "xAI model by id");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.xAI, "v1/language-models"), "xAI language models");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.xAI, "v1/embedding-models"), "xAI embedding models");
                Check.True(!ProxyPathPolicy.IsAllowed(ApiFormatEnum.xAI, "api/chat"), "xAI rejects Ollama path");
                Check.True(!ProxyPathPolicy.IsAllowed(ApiFormatEnum.OpenAI, "v1/responses"), "OpenAI dialect does not relay xAI Responses");
                Check.Contains("v1/responses", ProxyPathPolicy.DescribeAllowed(ApiFormatEnum.xAI));
            }));

            tests.Add(TestCaseFactory.Sync("Core", "ProxyPathPolicy: Ollama Cloud and xAI use bearer auth", () =>
            {
                ProxyAuthHeader? cloud = ProxyPathPolicy.ResolveAuthHeader(ApiFormatEnum.OllamaCloud, "oc-123");
                Check.True(cloud != null && cloud.Value.Name == "Authorization" && cloud.Value.Value == "Bearer oc-123", "Ollama Cloud bearer");
                ProxyAuthHeader? xai = ProxyPathPolicy.ResolveAuthHeader(ApiFormatEnum.xAI, "xai-123");
                Check.True(xai != null && xai.Value.Name == "Authorization" && xai.Value.Value == "Bearer xai-123", "xAI bearer");
            }));

            // ===== ProviderDefaults =====

            tests.Add(TestCaseFactory.Sync("Core", "ProviderDefaults: normalizes Ollama Cloud and xAI endpoints", () =>
            {
                Check.Equal("https://ollama.com", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.OllamaCloud, null));
                Check.Equal("https://ollama.com", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.OllamaCloud, "http://localhost:11434"));
                Check.Equal("https://api.x.ai", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.xAI, ""));
                Check.Equal("https://api.x.ai", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.xAI, "https://api.x.ai/v1/"));
                Check.Equal("http://gateway:8080/xai", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.xAI, " http://gateway:8080/xai/ "));
                Check.Equal("http://localhost:11434", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.Ollama, "http://localhost:11434"));
                Check.Equal("https://api.openai.com/v1", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.OpenAI, "https://api.openai.com/v1"));
            }));

            tests.Add(TestCaseFactory.Sync("Core", "ProviderDefaults: API key requirements, dialects, and service labels", () =>
            {
                Check.True(ProviderDefaults.RequiresApiKey(ApiFormatEnum.OllamaCloud), "Ollama Cloud requires a key");
                Check.True(ProviderDefaults.RequiresApiKey(ApiFormatEnum.xAI), "xAI requires a key");
                Check.False(ProviderDefaults.RequiresApiKey(ApiFormatEnum.Ollama), "local Ollama does not require a key");
                Check.False(ProviderDefaults.RequiresApiKey(ApiFormatEnum.OpenAI), "OpenAI-compatible servers may be keyless");
                Check.True(ProviderDefaults.IsOllamaDialect(ApiFormatEnum.OllamaCloud), "Ollama Cloud speaks the Ollama API");
                Check.False(ProviderDefaults.IsOllamaDialect(ApiFormatEnum.xAI), "xAI is not the Ollama API");
                Check.Equal("ollamacloud", ProviderDefaults.ServiceLabel(ApiFormatEnum.OllamaCloud));
                Check.Equal("xai", ProviderDefaults.ServiceLabel(ApiFormatEnum.xAI));
                ArgumentException ex = Check.Throws<ArgumentException>(() => ProviderDefaults.ValidateApiKey(ApiFormatEnum.xAI, " "));
                Check.Contains("xAI", ex.Message);
                ProviderDefaults.ValidateApiKey(ApiFormatEnum.Ollama, null);
            }));

            tests.Add(TestCaseFactory.Sync("Core", "Endpoint defaults: Ollama Cloud pins the host and authenticates health checks", () =>
            {
                EmbeddingEndpoint embedding = new EmbeddingEndpoint { Model = "embeddinggemma", Endpoint = "http://wrong-host:11434", ApiFormat = ApiFormatEnum.OllamaCloud, ApiKey = "oc-key" };
                EmbeddingEndpoint.ApplyHealthCheckDefaults(embedding);
                Check.Equal("https://ollama.com", embedding.Endpoint);
                Check.Equal("https://ollama.com/api/tags", embedding.HealthCheckUrl);
                Check.True(embedding.HealthCheckUseAuth, "Ollama Cloud health checks should send the bearer token");

                CompletionEndpoint completion = new CompletionEndpoint { Model = "gpt-oss:20b", ApiFormat = ApiFormatEnum.OllamaCloud, ApiKey = "oc-key" };
                completion.HealthCheckIntervalMs = 0;
                completion.HealthCheckTimeoutMs = 0;
                CompletionEndpoint.ApplyHealthCheckDefaults(completion);
                Check.Equal("https://ollama.com", completion.Endpoint);
                Check.Equal("https://ollama.com/api/tags", completion.HealthCheckUrl);
                Check.True(completion.HealthCheckUseAuth, "Ollama Cloud completion health checks should send the bearer token");
            }));

            tests.Add(TestCaseFactory.Sync("Core", "Endpoint defaults: xAI defaults the API root and model-list health check", () =>
            {
                CompletionEndpoint completion = new CompletionEndpoint { Model = "grok-4", ApiFormat = ApiFormatEnum.xAI, ApiKey = "xai-key" };
                CompletionEndpoint.ApplyHealthCheckDefaults(completion);
                Check.Equal("https://api.x.ai", completion.Endpoint);
                Check.Equal("https://api.x.ai/v1/models", completion.HealthCheckUrl);
                Check.True(completion.HealthCheckUseAuth, "xAI health checks should send the bearer token");

                EmbeddingEndpoint embedding = new EmbeddingEndpoint { Model = "grok-embedding", Endpoint = "https://api.x.ai/v1", ApiFormat = ApiFormatEnum.xAI, ApiKey = "xai-key" };
                EmbeddingEndpoint.ApplyHealthCheckDefaults(embedding);
                Check.Equal("https://api.x.ai", embedding.Endpoint);
                Check.Equal("https://api.x.ai/v1/models", embedding.HealthCheckUrl);
            }));

            return tests;
        }

        private static LoggingModule CreateLogging()
        {
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;
            return logging;
        }

        private sealed class SdkModelTimestamps
        {
            public string Name { get; }
            public object Model { get; }
            public DateTime Created { get; }
            public DateTime Updated { get; }

            public SdkModelTimestamps(string name, object model, DateTime created, DateTime updated)
            {
                Name = name;
                Model = model;
                Created = created;
                Updated = updated;
            }
        }
    }
}
