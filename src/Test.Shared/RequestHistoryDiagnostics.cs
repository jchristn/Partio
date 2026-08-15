namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Partio.Core.Database.Sqlite;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using Partio.Core.Settings;
    using Partio.Server.Services;
    using SyslogLogging;
    using Touchstone.Core;

    /// <summary>
    /// White-box tests for the server-side request-history persistence path, verifying that
    /// embedding call details, tokenization profiles, and chunk diagnostics round-trip through
    /// the detail document. Backed by temporary SQLite; requires no Partio server.
    /// </summary>
    public static class RequestHistoryDiagnostics
    {
        /// <summary>
        /// Build the Touchstone suite of request-history persistence tests.
        /// </summary>
        /// <returns>A suite descriptor exposing every case.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                "RequestHistory",
                "Request history persistence unit tests",
                BuildCases());
        }

        private static List<TestCaseDescriptor> BuildCases()
        {
            List<TestCaseDescriptor> tests = new List<TestCaseDescriptor>();

            tests.Add(TestCaseFactory.Async("RequestHistory", "UpdateWithResponse persists embedding calls and tokenization profile", async () =>
            {
                string tempRoot = Path.Combine(Path.GetTempPath(), "partio-request-history-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);

                try
                {
                    ServerSettings settings = new ServerSettings();
                    settings.Database.Type = DatabaseTypeEnum.Sqlite;
                    settings.Database.Filename = Path.Combine(tempRoot, "partio.db");
                    settings.RequestHistory.Directory = Path.Combine(tempRoot, "history");

                    LoggingModule logging = new LoggingModule();
                    logging.Settings.EnableConsole = false;

                    SqliteDatabaseDriver driver = new SqliteDatabaseDriver(settings, logging);
                    await driver.InitializeAsync();

                    RequestHistoryService service = new RequestHistoryService(settings, driver, logging);
                    RequestHistoryEntry entry = await service.CreateEntryAsync("POST", "/v1.0/process", "127.0.0.1", null);

                    List<EmbeddingCallDetail> embeddingCalls = new List<EmbeddingCallDetail>
                    {
                        new EmbeddingCallDetail
                        {
                            Purpose = "EmbeddingRequest",
                            Url = "http://localhost:11434/api/embed",
                            Method = "POST",
                            RequestBody = "{\"model\":\"all-minilm\"}",
                            ResponseBody = "{\"error\":\"context length exceeded\"}",
                            StatusCode = 400,
                            Success = false,
                            EffectiveInputBudget = 250,
                            MaxInputTokens = 256,
                            BatchLimitMode = BatchLimitModeEnum.PerInput,
                            FailedInputIndices = new List<int> { 0 },
                            FailureReasonHint = "Inputs 0 exceeded the effective per-input budget of 250 tokens.",
                            Inputs = new List<EmbeddingCallInputDetail>
                            {
                                new EmbeddingCallInputDetail
                                {
                                    Index = 0,
                                    CharacterCount = 1024,
                                    TokenCount = 251,
                                    ExceedsEffectiveInputBudget = true,
                                    Preview = "oversized chunk"
                                }
                            }
                        }
                    };

                    ResolvedTokenizationProfile profile = new ResolvedTokenizationProfile
                    {
                        TokenizerKind = TokenizerKindEnum.BertWordPiece,
                        TokenizerModel = "bert-base-uncased",
                        MaxInputTokens = 512,
                        ReservedInputTokens = 6,
                        EffectiveInputBudget = 506,
                        BatchLimitMode = BatchLimitModeEnum.PerInput,
                        ProfileSource = TokenizationProfileSourceEnum.ProviderProbe
                    };

                    await service.UpdateWithResponseAsync(
                        entry,
                        500,
                        12.5,
                        "{\"Input\":\"test\"}",
                        "context length exceeded",
                        new Dictionary<string, string> { { "Authorization", "Bearer test" } },
                        new Dictionary<string, string> { { "X-Partio-Tokenizer-Source", "ProviderProbe" } },
                        embeddingCalls,
                        null,
                        new Dictionary<string, object?>
                        {
                            { "TokenizationProfile", profile },
                            { "ChunkDiagnostics", new List<ChunkProcessingDiagnostic>
                                {
                                    new ChunkProcessingDiagnostic
                                    {
                                        CellGuid = Guid.NewGuid(),
                                        ChunkIndex = 0,
                                        ChunkCharacterCount = 1000,
                                        ChunkTokenCount = 245,
                                        EmbeddingCharacterCount = 1012,
                                        EmbeddingTokenCount = 251,
                                        ExceedsEffectiveInputBudget = true,
                                        Preview = "oversized chunk"
                                    }
                                }
                            }
                        });

                    Check.False(string.IsNullOrEmpty(entry.ObjectKey));

                    string? detailJson = await service.ReadDetailAsync(entry.ObjectKey!);
                    Check.False(string.IsNullOrEmpty(detailJson));

                    using JsonDocument doc = JsonDocument.Parse(detailJson!);
                    Check.True(doc.RootElement.TryGetProperty("EmbeddingCalls", out JsonElement callsElement));
                    Check.Equal(1, callsElement.GetArrayLength());
                    Check.Equal("http://localhost:11434/api/embed", callsElement[0].GetProperty("Url").GetString());
                    Check.Equal("EmbeddingRequest", callsElement[0].GetProperty("Purpose").GetString());
                    Check.Equal(250, callsElement[0].GetProperty("EffectiveInputBudget").GetInt32());
                    Check.Equal(1, callsElement[0].GetProperty("Inputs").GetArrayLength());
                    Check.Equal(251, callsElement[0].GetProperty("Inputs")[0].GetProperty("TokenCount").GetInt32());
                    Check.True(doc.RootElement.TryGetProperty("TokenizationProfile", out JsonElement profileElement));
                    Check.Equal("BertWordPiece", profileElement.GetProperty("TokenizerKind").GetString());
                    Check.Equal("ProviderProbe", profileElement.GetProperty("ProfileSource").GetString());
                    Check.Equal(506, profileElement.GetProperty("EffectiveInputBudget").GetInt32());
                    Check.True(doc.RootElement.TryGetProperty("ChunkDiagnostics", out JsonElement chunkDiagnosticsElement));
                    Check.Equal(1, chunkDiagnosticsElement.GetArrayLength());
                    Check.Equal(251, chunkDiagnosticsElement[0].GetProperty("EmbeddingTokenCount").GetInt32());
                }
                finally
                {
                    try { Directory.Delete(tempRoot, true); } catch { }
                }
            }));

            return tests;
        }
    }
}
