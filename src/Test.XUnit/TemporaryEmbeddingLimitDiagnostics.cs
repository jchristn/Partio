namespace Test.XUnit
{
    using System.Reflection;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using Partio.Core.Settings;
    using Partio.Core.ThirdParty;
    using Partio.Core.Tokenization;
    using SyslogLogging;
    using Xunit;

    public class TemporaryEmbeddingLimitDiagnostics
    {
        [Fact]
        public async Task BatchLimitCalibrationDetectsWholeRequestEndpoints()
        {
            ServerSettings settings = new ServerSettings();
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;

            EmbeddingEndpoint endpoint = new EmbeddingEndpoint
            {
                Id = "eep_diag_limit",
                TenantId = "default",
                Model = "all-minilm",
                Endpoint = "http://localhost:11434",
                ApiFormat = ApiFormatEnum.Ollama
            };

            TokenizationProfileResolver resolver = new TokenizationProfileResolver(settings, logging);
            SimpleWhitespaceTokenizer tokenizer = new SimpleWhitespaceTokenizer();
            DiagnosticEmbeddingClient client = new DiagnosticEmbeddingClient(endpoint.Endpoint, endpoint.ApiKey, logging)
            {
                Tokenizer = tokenizer,
                AcceptedTokenLimit = 8,
                SimulatedBatchMode = BatchLimitModeEnum.WholeRequest
            };

            MethodInfo? discoverBatchModeMethod = typeof(TokenizationProfileResolver).GetMethod(
                "DiscoverBatchLimitModeAsync",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(discoverBatchModeMethod);

            Task<BatchLimitModeEnum> discoverTask = (Task<BatchLimitModeEnum>)discoverBatchModeMethod!.Invoke(
                resolver,
                new object[]
                {
                    client,
                    endpoint.Model,
                    tokenizer,
                    20,
                    8,
                    BatchLimitModeEnum.PerInput,
                    CancellationToken.None
                })!;

            Task completedDiscoverTask = await Task.WhenAny(discoverTask, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(discoverTask, completedDiscoverTask);
            BatchLimitModeEnum discoveredBatchMode = await discoverTask;
            Assert.Equal(BatchLimitModeEnum.WholeRequest, discoveredBatchMode);
        }

        [Fact]
        public async Task EmbedTextsFallsBackWhenOptimisticBatchingExceedsWholeRequestLimit()
        {
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;

            EmbeddingEndpoint endpoint = new EmbeddingEndpoint
            {
                Id = "eep_diag_retry",
                TenantId = "default",
                Model = "all-minilm",
                Endpoint = "http://localhost:11434",
                ApiFormat = ApiFormatEnum.Ollama
            };

            SimpleWhitespaceTokenizer tokenizer = new SimpleWhitespaceTokenizer();
            DiagnosticEmbeddingClient client = new DiagnosticEmbeddingClient(endpoint.Endpoint, endpoint.ApiKey, logging)
            {
                Tokenizer = tokenizer,
                AcceptedTokenLimit = 8,
                SimulatedBatchMode = BatchLimitModeEnum.WholeRequest
            };

            ResolvedTokenizationProfile profile = new ResolvedTokenizationProfile
            {
                TokenizerKind = TokenizerKindEnum.Cl100kBase,
                TokenizerModel = "simple-whitespace",
                MaxInputTokens = 20,
                EffectiveInputBudget = 8,
                ReservedInputTokens = 12,
                BatchLimitMode = BatchLimitModeEnum.PerInput
            };

            List<string> textsToEmbed = new List<string>
            {
                BuildTextWithTokenCount(6),
                BuildTextWithTokenCount(6)
            };

            MethodInfo? embedTextsMethod = typeof(Partio.Server.PartioServer).GetMethod(
                "EmbedTextsAsync",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(embedTextsMethod);

            Task<List<List<float>>> task = (Task<List<List<float>>>)embedTextsMethod!.Invoke(
                null,
                new object[] { textsToEmbed, client, endpoint.Model, profile, tokenizer })!;

            Task completedEmbedTask = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(task, completedEmbedTask);
            List<List<float>> embeddings = await task;
            Assert.Equal(2, embeddings.Count);
            Assert.Equal(new[] { 12, 6, 6 }, client.ObservedBatchTokenCounts);
        }

        [Fact]
        public async Task EmbedTextsSplitsRejectedSingleInputUntilItFits()
        {
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;

            EmbeddingEndpoint endpoint = new EmbeddingEndpoint
            {
                Id = "eep_diag_single_retry",
                TenantId = "default",
                Model = "all-minilm",
                Endpoint = "http://localhost:11434",
                ApiFormat = ApiFormatEnum.Ollama
            };

            SimpleWhitespaceTokenizer tokenizer = new SimpleWhitespaceTokenizer();
            DiagnosticEmbeddingClient client = new DiagnosticEmbeddingClient(endpoint.Endpoint, endpoint.ApiKey, logging)
            {
                Tokenizer = tokenizer,
                AcceptedTokenLimit = 4,
                SimulatedBatchMode = BatchLimitModeEnum.WholeRequest
            };

            ResolvedTokenizationProfile profile = new ResolvedTokenizationProfile
            {
                TokenizerKind = TokenizerKindEnum.Cl100kBase,
                TokenizerModel = "simple-whitespace",
                MaxInputTokens = 20,
                EffectiveInputBudget = 8,
                ReservedInputTokens = 12,
                BatchLimitMode = BatchLimitModeEnum.WholeRequest
            };

            List<string> textsToEmbed = new List<string> { BuildTextWithTokenCount(10) };

            MethodInfo? embedTextsMethod = typeof(Partio.Server.PartioServer).GetMethod(
                "EmbedTextsAsync",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(embedTextsMethod);

            Task<List<List<float>>> task = (Task<List<List<float>>>)embedTextsMethod!.Invoke(
                null,
                new object[] { textsToEmbed, client, endpoint.Model, profile, tokenizer })!;

            Task completedEmbedTask = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(task, completedEmbedTask);
            List<List<float>> embeddings = await task;
            Assert.Single(embeddings);
            Assert.Equal(10, client.ObservedBatchTokenCounts[0]);
            Assert.Contains(client.ObservedBatchTokenCounts, tokenCount => tokenCount <= client.AcceptedTokenLimit);
        }

        private static string BuildTextWithTokenCount(int tokenCount)
        {
            return string.Join(' ', Enumerable.Range(1, tokenCount).Select(i => "token" + i));
        }

        private sealed class DiagnosticEmbeddingClient : EmbeddingClientBase
        {
            public DiagnosticEmbeddingClient(string endpoint, string? apiKey, LoggingModule logging)
                : base(endpoint, apiKey, logging)
            {
            }

            public ITokenizerAdapter? Tokenizer { get; set; }

            public int AcceptedTokenLimit { get; set; } = int.MaxValue;

            public BatchLimitModeEnum SimulatedBatchMode { get; set; } = BatchLimitModeEnum.PerInput;

            public List<int> ObservedBatchTokenCounts { get; } = new List<int>();

            public override Task<List<float>> EmbedAsync(string text, string model, CancellationToken token = default)
            {
                return Task.FromResult(new List<float>());
            }

            public override Task<List<List<float>>> EmbedBatchAsync(List<string> texts, string model, CancellationToken token = default)
            {
                int batchTokenCount = 0;
                foreach (string text in texts)
                {
                    int tokenCount = Tokenizer?.CountTokens(text) ?? 0;
                    batchTokenCount += tokenCount;

                    if (SimulatedBatchMode != BatchLimitModeEnum.WholeRequest && tokenCount > AcceptedTokenLimit)
                        throw new Exception("the input length exceeds the context length");
                }

                ObservedBatchTokenCounts.Add(batchTokenCount);

                if (SimulatedBatchMode == BatchLimitModeEnum.WholeRequest && batchTokenCount > AcceptedTokenLimit)
                    throw new Exception("the input length exceeds the context length");

                return Task.FromResult(texts.Select(_ => new List<float>()).ToList());
            }
        }

        private sealed class SimpleWhitespaceTokenizer : ITokenizerAdapter
        {
            public int CountTokens(string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return 0;
                return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            }

            public IReadOnlyList<int> Encode(string text)
            {
                return Enumerable.Range(0, CountTokens(text)).ToArray();
            }

            public string Decode(IEnumerable<int> tokenIds)
            {
                return string.Join(' ', tokenIds.Select(id => "token" + id));
            }

            public string SliceByTokenRange(string text, int startTokenIndex, int tokenCount)
            {
                string[] tokens = string.IsNullOrWhiteSpace(text)
                    ? Array.Empty<string>()
                    : text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                return string.Join(' ', tokens.Skip(startTokenIndex).Take(tokenCount));
            }
        }
    }
}
