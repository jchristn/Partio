namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Partio.Core;
    using Partio.Core.Chunking;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using Partio.Core.Serialization;
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
                    MaxConcurrentRequests = 4
                };

                string json = serializer.SerializeJson(original, false);
                Check.NotNull(json);
                Check.Contains("text-embedding-3-small", json);

                EmbeddingEndpoint restored = serializer.DeserializeJson<EmbeddingEndpoint>(json);
                Check.NotNull(restored);
                Check.Equal(original.Model, restored.Model);
                Check.Equal(original.MaximumTimeoutMs, restored.MaximumTimeoutMs);
                Check.Equal(original.MaxConcurrentRequests, restored.MaxConcurrentRequests);
                Check.Equal(ApiFormatEnum.OpenAI, restored.ApiFormat);
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

            return tests;
        }

        private static LoggingModule CreateLogging()
        {
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;
            return logging;
        }
    }
}
