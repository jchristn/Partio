namespace Test.Shared
{
    using Partio.Core.Chunking;
    using Partio.Core.Database.Sqlite;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using Partio.Core.Settings;
    using Partio.Core.ThirdParty;
    using Partio.Core.Tokenization;
    using SyslogLogging;

    public static class SharedTokenizerUnitTests
    {
        public static IReadOnlyList<SharedNamedTestCase> GetTests()
        {
            List<SharedNamedTestCase> tests = new List<SharedNamedTestCase>();

            tests.Add(SharedNamedTestCase.CreateAsync("Tokenization resolver: endpoint override beats capabilities and defaults", async () =>
            {
                ServerSettings settings = new ServerSettings();
                LoggingModule logging = CreateLogging();
                TokenizationProfileResolver resolver = new TokenizationProfileResolver(settings, logging);

                EmbeddingEndpoint endpoint = new EmbeddingEndpoint
                {
                    Id = "eep_override",
                    TenantId = "default",
                    Model = "all-minilm",
                    Endpoint = "http://localhost:11434",
                    ApiFormat = ApiFormatEnum.Ollama,
                    Tokenization = new EndpointTokenizationSettings
                    {
                        TokenizerKind = TokenizerKindEnum.BertWordPiece,
                        TokenizerModel = "bert-base-uncased",
                        MaxInputTokens = 384,
                        ReservedInputTokens = 16,
                        BatchLimitMode = BatchLimitModeEnum.WholeRequest,
                        AutoDetect = false
                    }
                };

                FakeEmbeddingClient client = new FakeEmbeddingClient(endpoint.Endpoint, endpoint.ApiKey, logging)
                {
                    Capabilities = new EmbeddingModelCapabilities
                    {
                        SourceHint = TokenizationProfileSourceEnum.ProviderProbe,
                        TokenizerKind = TokenizerKindEnum.Cl100kBase,
                        TokenizerModel = "cl100k_base",
                        MaxInputTokens = 8192,
                        ReservedInputTokens = 0,
                        BatchLimitMode = BatchLimitModeEnum.PerInput
                    }
                };

                ResolvedTokenizationProfile profile = await resolver.ResolveAsync(endpoint, endpoint.Model, client).ConfigureAwait(false);
                if (profile.ProfileSource != TokenizationProfileSourceEnum.EndpointOverride)
                    throw new Exception("Expected EndpointOverride source.");
                if (profile.TokenizerKind != TokenizerKindEnum.BertWordPiece)
                    throw new Exception("Expected BertWordPiece tokenizer kind.");
                if (profile.TokenizerModel != "bert-base-uncased")
                    throw new Exception("Expected bert-base-uncased tokenizer model.");
                if (profile.EffectiveInputBudget != 368)
                    throw new Exception("Expected effective budget 368, got " + profile.EffectiveInputBudget);
            }));

            tests.Add(SharedNamedTestCase.CreateAsync("Tokenization resolver: global fallback is used when no endpoint-specific resolution exists", async () =>
            {
                ServerSettings settings = new ServerSettings();
                settings.TokenizationDefaults.GlobalFallback = new EndpointTokenizationSettings
                {
                    TokenizerKind = TokenizerKindEnum.Cl100kBase,
                    TokenizerModel = "cl100k_base",
                    MaxInputTokens = 1234,
                    ReservedInputTokens = 34,
                    BatchLimitMode = BatchLimitModeEnum.PerInput,
                    AutoDetect = true
                };
                settings.TokenizationDefaults.Ollama = null;

                LoggingModule logging = CreateLogging();
                TokenizationProfileResolver resolver = new TokenizationProfileResolver(settings, logging);

                EmbeddingEndpoint endpoint = new EmbeddingEndpoint
                {
                    Id = "eep_fallback",
                    TenantId = "default",
                    Model = "unknown-local-embedder",
                    Endpoint = "http://localhost:11434",
                    ApiFormat = ApiFormatEnum.Ollama
                };

                FakeEmbeddingClient client = new FakeEmbeddingClient(endpoint.Endpoint, endpoint.ApiKey, logging);
                ResolvedTokenizationProfile profile = await resolver.ResolveAsync(endpoint, endpoint.Model, client).ConfigureAwait(false);

                if (profile.ProfileSource != TokenizationProfileSourceEnum.GlobalFallback)
                    throw new Exception("Expected GlobalFallback source.");
                if (profile.EffectiveInputBudget != 1200)
                    throw new Exception("Expected effective budget 1200, got " + profile.EffectiveInputBudget);
            }));

            tests.Add(SharedNamedTestCase.CreateAsync("Tokenization resolver: provider calibration discovers effective budget and per-input batch mode", async () =>
            {
                ServerSettings settings = new ServerSettings();
                LoggingModule logging = CreateLogging();
                TokenizationProfileResolver resolver = new TokenizationProfileResolver(settings, logging);

                EmbeddingEndpoint endpoint = new EmbeddingEndpoint
                {
                    Id = "eep_calibration",
                    TenantId = "default",
                    Model = "all-minilm",
                    Endpoint = "http://localhost:11434",
                    ApiFormat = ApiFormatEnum.Ollama
                };

                FakeEmbeddingClient client = new FakeEmbeddingClient(endpoint.Endpoint, endpoint.ApiKey, logging)
                {
                    Tokenizer = new BertWordPieceTokenizerAdapter(),
                    AcceptedPerInputTokenLimit = 250,
                    SimulatedBatchMode = BatchLimitModeEnum.PerInput,
                    Capabilities = new EmbeddingModelCapabilities
                    {
                        SourceHint = TokenizationProfileSourceEnum.ProviderProbe,
                        TokenizerKind = TokenizerKindEnum.BertWordPiece,
                        TokenizerModel = "bert-base-uncased",
                        MaxInputTokens = 256,
                        BatchLimitMode = BatchLimitModeEnum.Unknown
                    }
                };

                ResolvedTokenizationProfile profile = await resolver.ResolveAsync(endpoint, endpoint.Model, client).ConfigureAwait(false);
                if (profile.TokenizerKind != TokenizerKindEnum.BertWordPiece)
                    throw new Exception("Expected BertWordPiece tokenizer kind.");
                if (profile.MaxInputTokens != 256)
                    throw new Exception("Expected max input tokens 256, got " + profile.MaxInputTokens);
                if (profile.EffectiveInputBudget != 250)
                    throw new Exception("Expected calibrated effective budget 250, got " + profile.EffectiveInputBudget);
                if (profile.ReservedInputTokens != 6)
                    throw new Exception("Expected reserved token count 6, got " + profile.ReservedInputTokens);
                if (profile.BatchLimitMode != BatchLimitModeEnum.PerInput)
                    throw new Exception("Expected discovered PerInput batch limit mode.");
                if (!profile.ProviderMetadata.TryGetValue("CalibrationApplied", out string? calibrationApplied)
                    || !bool.TryParse(calibrationApplied, out bool parsed)
                    || !parsed)
                {
                    throw new Exception("Expected calibration metadata to indicate success.");
                }
            }));

            tests.Add(SharedNamedTestCase.CreateAsync("Tokenization resolver: endpoint override derives effective budget from reserved tokens", async () =>
            {
                ServerSettings settings = new ServerSettings();
                LoggingModule logging = CreateLogging();
                TokenizationProfileResolver resolver = new TokenizationProfileResolver(settings, logging);

                EmbeddingEndpoint endpoint = new EmbeddingEndpoint
                {
                    Id = "eep_override_derived_budget",
                    TenantId = "default",
                    Model = "all-minilm",
                    Endpoint = "http://localhost:11434",
                    ApiFormat = ApiFormatEnum.Ollama,
                    Tokenization = new EndpointTokenizationSettings
                    {
                        TokenizerKind = TokenizerKindEnum.BertWordPiece,
                        TokenizerModel = "bert-base-uncased",
                        MaxInputTokens = 256,
                        ReservedInputTokens = 6,
                        BatchLimitMode = BatchLimitModeEnum.PerInput,
                        AutoDetect = false
                    }
                };

                FakeEmbeddingClient client = new FakeEmbeddingClient(endpoint.Endpoint, endpoint.ApiKey, logging)
                {
                    Capabilities = new EmbeddingModelCapabilities
                    {
                        SourceHint = TokenizationProfileSourceEnum.ProviderProbe,
                        TokenizerKind = TokenizerKindEnum.BertWordPiece,
                        TokenizerModel = "bert-base-uncased",
                        MaxInputTokens = 256,
                        BatchLimitMode = BatchLimitModeEnum.Unknown
                    }
                };

                ResolvedTokenizationProfile profile = await resolver.ResolveAsync(endpoint, endpoint.Model, client).ConfigureAwait(false);
                if (profile.ProfileSource != TokenizationProfileSourceEnum.EndpointOverride)
                    throw new Exception("Expected EndpointOverride source.");
                if (profile.MaxInputTokens != 256)
                    throw new Exception("Expected max input tokens 256, got " + profile.MaxInputTokens);
                if (profile.ReservedInputTokens != 6)
                    throw new Exception("Expected reserved token count 6, got " + profile.ReservedInputTokens);
                if (profile.EffectiveInputBudget != 250)
                    throw new Exception("Expected derived effective budget 250, got " + profile.EffectiveInputBudget);
            }));

            tests.Add(SharedNamedTestCase.CreateSync("BERT token slicing: regression sample never exceeds requested token count", () =>
            {
                ITokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                string text = string.Join(" ", Enumerable.Repeat(
                    "1521-0081/69/2/200-235$25.00 HARMACOLOGICAL REVIEWS pharmacology section GL-1 GL-2 XEOMIN Placebo Week 4 mean change from baseline at maximum frown Units/kg (N=87) (N=176) Ashworth Scale LS Mean Difference versus placebo p<0.001 ophthalmology dosing table chronic sialorrhea upper limb spasticity postmarketing experience and contraindications.",
                    10));
                int totalTokens = tokenizer.CountTokens(text);
                int[] startPositions = new[] { 0, 19, 57, 101, 143 };
                int[] requestedCounts = new[] { 32, 64, 128, 192, 256 };

                foreach (int start in startPositions.Where(pos => pos < totalTokens))
                {
                    foreach (int requested in requestedCounts)
                    {
                        int available = Math.Min(requested, totalTokens - start);
                        string slice = tokenizer.SliceByTokenRange(text, start, available);
                        int actual = tokenizer.CountTokens(slice);
                        if (actual > available)
                        {
                            throw new Exception("Slice exceeded requested token count. Start=" + start
                                + ", Requested=" + available
                                + ", Actual=" + actual
                                + ", Slice=" + slice);
                        }
                    }
                }
            }));

            tests.Add(SharedNamedTestCase.CreateSync("Sentence chunker: oversized single sentence descends into in-budget token spans", () =>
            {
                ITokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                int budget = 12;
                SemanticCellRequest request = BuildTextRequest(
                    string.Join(" ", Enumerable.Repeat("electroencephalographically complex tokenization sentence", 16)) + ".",
                    ChunkStrategyEnum.SentenceBased,
                    budget);

                ChunkingEngine engine = new ChunkingEngine(CreateLogging());
                List<ChunkResult> chunks = engine.Chunk(request, tokenizer, budget);
                if (chunks.Count < 2)
                    throw new Exception("Expected multiple chunks for oversized sentence.");
                AssertChunksInBudget(chunks, tokenizer, budget);
            }));

            tests.Add(SharedNamedTestCase.CreateSync("Paragraph chunker: oversized single paragraph descends to sentence and token spans", () =>
            {
                ITokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                int budget = 18;
                string paragraph = string.Join(" ", Enumerable.Repeat("This paragraph keeps growing with uncommon terminology and repeated clauses.", 10));
                SemanticCellRequest request = BuildTextRequest(paragraph, ChunkStrategyEnum.ParagraphBased, budget);

                ChunkingEngine engine = new ChunkingEngine(CreateLogging());
                List<ChunkResult> chunks = engine.Chunk(request, tokenizer, budget);
                if (chunks.Count < 2)
                    throw new Exception("Expected multiple chunks for oversized paragraph.");
                AssertChunksInBudget(chunks, tokenizer, budget);
            }));

            tests.Add(SharedNamedTestCase.CreateSync("Regex chunker: oversized regex segment descends into token spans", () =>
            {
                ITokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                int budget = 10;
                SemanticCellRequest request = BuildTextRequest(
                    "alpha ### " + string.Join(" ", Enumerable.Repeat("hypercholesterolemia", 20)) + " ### omega",
                    ChunkStrategyEnum.RegexBased,
                    budget,
                    "###");

                ChunkingEngine engine = new ChunkingEngine(CreateLogging());
                List<ChunkResult> chunks = engine.Chunk(request, tokenizer, budget);
                if (chunks.Count < 3)
                    throw new Exception("Expected regex chunker to split oversized segment.");
                AssertChunksInBudget(chunks, tokenizer, budget);
            }));

            tests.Add(SharedNamedTestCase.CreateSync("List chunkers: oversized list items are reduced before emission", () =>
            {
                ITokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                int budget = 10;
                SemanticCellRequest request = new SemanticCellRequest
                {
                    Type = AtomTypeEnum.List,
                    UnorderedList = new List<string>
                    {
                        string.Join(" ", Enumerable.Repeat("microarchitecturalization", 12)),
                        "short item"
                    },
                    ChunkingConfiguration = new ChunkingConfiguration
                    {
                        Strategy = ChunkStrategyEnum.ListEntry,
                        FixedTokenCount = budget
                    }
                };

                ChunkingEngine engine = new ChunkingEngine(CreateLogging());
                List<ChunkResult> chunks = engine.Chunk(request, tokenizer, budget);
                if (chunks.Count < 2)
                    throw new Exception("Expected oversized list item to be split.");
                AssertChunksInBudget(chunks, tokenizer, budget);
            }));

            tests.Add(SharedNamedTestCase.CreateSync("Table chunkers: oversized grouped rows descend to row and cell boundaries", () =>
            {
                ITokenizerAdapter tokenizer = new BertWordPieceTokenizerAdapter();
                int budget = 14;
                SemanticCellRequest request = new SemanticCellRequest
                {
                    Type = AtomTypeEnum.Table,
                    Table = new List<List<string>>
                    {
                        new List<string> { "Name", "Details" },
                        new List<string> { "Row 1", string.Join(" ", Enumerable.Repeat("pseudopseudohypoparathyroidism", 10)) },
                        new List<string> { "Row 2", "brief" }
                    },
                    ChunkingConfiguration = new ChunkingConfiguration
                    {
                        Strategy = ChunkStrategyEnum.RowGroupWithHeaders,
                        RowGroupSize = 2,
                        FixedTokenCount = budget
                    }
                };

                ChunkingEngine engine = new ChunkingEngine(CreateLogging());
                List<ChunkResult> chunks = engine.Chunk(request, tokenizer, budget);
                if (chunks.Count < 2)
                    throw new Exception("Expected grouped row fallback to emit multiple chunks.");
                AssertChunksInBudget(chunks, tokenizer, budget);
            }));

            tests.Add(SharedNamedTestCase.CreateSync("Regression: text that fits cl100k assumptions still splits safely in Bert token space", () =>
            {
                ITokenizerAdapter cl100k = new SharpTokenTokenizerAdapter("cl100k_base");
                ITokenizerAdapter bert = new BertWordPieceTokenizerAdapter();
                string[] candidates = new[]
                {
                    string.Join(" ", Enumerable.Repeat("electroencephalographically hypercholesterolemia microarchitecturalization", 4)),
                    string.Join(" ", Enumerable.Repeat("pseudopseudohypoparathyroidism antidisestablishmentarianism characterization", 4)),
                    string.Join(" ", Enumerable.Repeat("counterrevolutionaries incomprehensibilities bioelectromagnetics", 4))
                };

                string? sample = candidates.FirstOrDefault(candidate => bert.CountTokens(candidate) > cl100k.CountTokens(candidate));
                if (sample == null)
                    throw new Exception("Expected at least one regression sample where Bert token count exceeds cl100k token count.");

                int budget = cl100k.CountTokens(sample);
                SemanticCellRequest request = BuildTextRequest(sample, ChunkStrategyEnum.FixedTokenCount, budget);
                ChunkingEngine engine = new ChunkingEngine(CreateLogging());
                List<ChunkResult> chunks = engine.Chunk(request, bert, budget);

                if (chunks.Count < 2)
                    throw new Exception("Expected Bert-token-aware chunking to split the regression sample.");
                AssertChunksInBudget(chunks, bert, budget);
            }));

            tests.Add(SharedNamedTestCase.CreateAsync("Embedding endpoint persistence: tokenization settings round-trip through SQLite", async () =>
            {
                string tempRoot = Path.Combine(Path.GetTempPath(), "partio-tokenizer-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);

                try
                {
                    ServerSettings settings = new ServerSettings();
                    settings.Database.Type = DatabaseTypeEnum.Sqlite;
                    settings.Database.Filename = Path.Combine(tempRoot, "partio.db");
                    settings.RequestHistory.Directory = Path.Combine(tempRoot, "history");

                    LoggingModule logging = CreateLogging();
                    SqliteDatabaseDriver driver = new SqliteDatabaseDriver(settings, logging);
                    await driver.InitializeAsync().ConfigureAwait(false);

                    EmbeddingEndpoint endpointWithTokenization = new EmbeddingEndpoint
                    {
                        Id = "eep_persist_1",
                        TenantId = "default",
                        Name = "Persisted",
                        Model = "text-embedding-3-small",
                        Endpoint = "https://api.openai.com",
                        ApiFormat = ApiFormatEnum.OpenAI,
                        Tokenization = new EndpointTokenizationSettings
                        {
                            TokenizerKind = TokenizerKindEnum.Cl100kBase,
                            TokenizerModel = "cl100k_base",
                            MaxInputTokens = 8192,
                            ReservedInputTokens = 8,
                            EffectiveInputBudget = 8184,
                            BatchLimitMode = BatchLimitModeEnum.PerInput,
                            AutoDetect = true
                        }
                    };

                    EmbeddingEndpoint endpointWithoutTokenization = new EmbeddingEndpoint
                    {
                        Id = "eep_persist_2",
                        TenantId = "default",
                        Name = "No Override",
                        Model = "all-minilm",
                        Endpoint = "http://localhost:11434",
                        ApiFormat = ApiFormatEnum.Ollama
                    };

                    await driver.EmbeddingEndpoint.CreateAsync(endpointWithTokenization).ConfigureAwait(false);
                    await driver.EmbeddingEndpoint.CreateAsync(endpointWithoutTokenization).ConfigureAwait(false);

                    EmbeddingEndpoint? readWithTokenization = await driver.EmbeddingEndpoint.ReadByIdAsync(endpointWithTokenization.Id).ConfigureAwait(false);
                    EmbeddingEndpoint? readWithoutTokenization = await driver.EmbeddingEndpoint.ReadByIdAsync(endpointWithoutTokenization.Id).ConfigureAwait(false);
                    if (readWithTokenization?.Tokenization == null)
                        throw new Exception("Expected tokenization settings to round-trip for populated endpoint.");
                    if (readWithTokenization.Tokenization.TokenizerKind != TokenizerKindEnum.Cl100kBase)
                        throw new Exception("Tokenizer kind did not round-trip.");
                    if (readWithTokenization.Tokenization.EffectiveInputBudget != 8184)
                        throw new Exception("EffectiveInputBudget did not round-trip.");
                    if (readWithoutTokenization?.Tokenization != null)
                        throw new Exception("Expected null tokenization settings for omitted endpoint.");

                    readWithTokenization.Tokenization.ReservedInputTokens = 12;
                    readWithTokenization.Tokenization.EffectiveInputBudget = 8180;
                    await driver.EmbeddingEndpoint.UpdateAsync(readWithTokenization).ConfigureAwait(false);

                    EnumerationResult<EmbeddingEndpoint> enumeration = await driver.EmbeddingEndpoint.EnumerateAsync("default", new EnumerationRequest { MaxResults = 10 }).ConfigureAwait(false);
                    if (enumeration.Data.Count < 2)
                        throw new Exception("Expected at least two endpoints from enumeration.");

                    EmbeddingEndpoint? updated = enumeration.Data.FirstOrDefault(e => e.Id == endpointWithTokenization.Id);
                    if (updated?.Tokenization?.ReservedInputTokens != 12)
                        throw new Exception("Updated tokenization settings were not persisted.");
                    if (updated?.Tokenization?.EffectiveInputBudget != 8180)
                        throw new Exception("Updated effective input budget was not persisted.");
                }
                finally
                {
                    try { Directory.Delete(tempRoot, true); } catch { }
                }
            }));

            return tests;
        }

        private static LoggingModule CreateLogging()
        {
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;
            return logging;
        }

        private static SemanticCellRequest BuildTextRequest(string text, ChunkStrategyEnum strategy, int budget, string? regexPattern = null)
        {
            return new SemanticCellRequest
            {
                Type = AtomTypeEnum.Text,
                Text = text,
                ChunkingConfiguration = new ChunkingConfiguration
                {
                    Strategy = strategy,
                    FixedTokenCount = budget,
                    RegexPattern = regexPattern
                }
            };
        }

        private static void AssertChunksInBudget(List<ChunkResult> chunks, ITokenizerAdapter tokenizer, int budget)
        {
            if (chunks == null || chunks.Count == 0) throw new Exception("Expected at least one chunk.");

            foreach (ChunkResult chunk in chunks)
            {
                int tokenCount = tokenizer.CountTokens(chunk.Text);
                if (tokenCount > budget)
                    throw new Exception("Chunk exceeded budget. Budget=" + budget + ", Tokens=" + tokenCount + ", Text=" + chunk.Text);
            }
        }

        private sealed class FakeEmbeddingClient : EmbeddingClientBase
        {
            public EmbeddingModelCapabilities? Capabilities { get; set; }
            public ITokenizerAdapter? Tokenizer { get; set; }
            public int AcceptedPerInputTokenLimit { get; set; } = int.MaxValue;
            public BatchLimitModeEnum SimulatedBatchMode { get; set; } = BatchLimitModeEnum.PerInput;

            public FakeEmbeddingClient(string endpoint, string? apiKey, LoggingModule logging)
                : base(endpoint, apiKey, logging)
            {
            }

            public override Task<List<float>> EmbedAsync(string text, string model, CancellationToken token = default)
            {
                return Task.FromResult(new List<float>());
            }

            public override Task<List<List<float>>> EmbedBatchAsync(List<string> texts, string model, CancellationToken token = default)
            {
                List<EmbeddingCallInputDetail>? inputs = null;
                int batchTokenCount = 0;
                if (Tokenizer != null)
                {
                    inputs = texts.Select((text, index) =>
                    {
                        int tokenCount = Tokenizer.CountTokens(text);
                        batchTokenCount += tokenCount;
                        return new EmbeddingCallInputDetail
                        {
                            Index = index,
                            CharacterCount = text.Length,
                            TokenCount = tokenCount,
                            ExceedsEffectiveInputBudget = tokenCount > AcceptedPerInputTokenLimit,
                            Preview = text.Length <= 80 ? text : text.Substring(0, 80)
                        };
                    }).ToList();
                }

                CallDetails.Add(new EmbeddingCallDetail
                {
                    Purpose = "EmbeddingRequest",
                    Url = _Endpoint.TrimEnd('/') + "/api/embed",
                    Method = "POST",
                    Success = true,
                    Inputs = inputs,
                    BatchTokenCount = batchTokenCount,
                    EffectiveInputBudget = AcceptedPerInputTokenLimit,
                    MaxInputTokens = Capabilities?.MaxInputTokens,
                    BatchLimitMode = SimulatedBatchMode
                });

                bool exceedsPerInputLimit = inputs != null && inputs.Any(input => input.TokenCount > AcceptedPerInputTokenLimit);
                bool exceedsWholeRequestLimit = SimulatedBatchMode == BatchLimitModeEnum.WholeRequest
                    && batchTokenCount > AcceptedPerInputTokenLimit;
                if (exceedsPerInputLimit || exceedsWholeRequestLimit)
                {
                    EmbeddingCallDetail detail = CallDetails[CallDetails.Count - 1];
                    detail.Success = false;
                    detail.StatusCode = 400;
                    detail.ResponseBody = "{\"error\":\"the input length exceeds the context length\"}";
                    throw new Exception("the input length exceeds the context length");
                }

                return Task.FromResult(texts.Select(_ => new List<float>()).ToList());
            }

            public override Task<EmbeddingModelCapabilities?> GetModelCapabilitiesAsync(string model, CancellationToken token = default)
            {
                return Task.FromResult(Capabilities);
            }
        }
    }
}
