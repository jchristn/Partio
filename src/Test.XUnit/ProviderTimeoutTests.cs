namespace Test.XUnit
{
    using System.Reflection;
    using Partio.Core.Enums;
    using Partio.Core.Exceptions;
    using Partio.Core.Models;
    using Partio.Core.Settings;
    using Partio.Core.Summarization;
    using Partio.Core.ThirdParty;
    using SyslogLogging;
    using Test.Shared;
    using Xunit;

    public class ProviderTimeoutTests
    {
        [Fact]
        public void EndpointAndSettingsProviderLimitsDefaultAndClamp()
        {
            EmbeddingEndpoint embeddingEndpoint = new EmbeddingEndpoint();
            CompletionEndpoint completionEndpoint = new CompletionEndpoint();
            DefaultEmbeddingEndpoint defaultEmbeddingEndpoint = new DefaultEmbeddingEndpoint();
            DefaultInferenceEndpoint defaultInferenceEndpoint = new DefaultInferenceEndpoint();

            Assert.Equal(60000, embeddingEndpoint.MaximumTimeoutMs);
            Assert.Equal(60000, completionEndpoint.MaximumTimeoutMs);
            Assert.Equal(60000, defaultEmbeddingEndpoint.MaximumTimeoutMs);
            Assert.Equal(60000, defaultInferenceEndpoint.MaximumTimeoutMs);
            Assert.Equal(2, embeddingEndpoint.MaxConcurrentRequests);
            Assert.Equal(2, completionEndpoint.MaxConcurrentRequests);
            Assert.Equal(2, defaultEmbeddingEndpoint.MaxConcurrentRequests);
            Assert.Equal(2, defaultInferenceEndpoint.MaxConcurrentRequests);

            embeddingEndpoint.MaximumTimeoutMs = 0;
            completionEndpoint.MaximumTimeoutMs = -42;
            defaultEmbeddingEndpoint.MaximumTimeoutMs = 0;
            defaultInferenceEndpoint.MaximumTimeoutMs = -1;
            embeddingEndpoint.MaxConcurrentRequests = 0;
            completionEndpoint.MaxConcurrentRequests = -42;
            defaultEmbeddingEndpoint.MaxConcurrentRequests = 0;
            defaultInferenceEndpoint.MaxConcurrentRequests = -1;

            Assert.Equal(1, embeddingEndpoint.MaximumTimeoutMs);
            Assert.Equal(1, completionEndpoint.MaximumTimeoutMs);
            Assert.Equal(1, defaultEmbeddingEndpoint.MaximumTimeoutMs);
            Assert.Equal(1, defaultInferenceEndpoint.MaximumTimeoutMs);
            Assert.Equal(1, embeddingEndpoint.MaxConcurrentRequests);
            Assert.Equal(1, completionEndpoint.MaxConcurrentRequests);
            Assert.Equal(1, defaultEmbeddingEndpoint.MaxConcurrentRequests);
            Assert.Equal(1, defaultInferenceEndpoint.MaxConcurrentRequests);
        }

        [Fact]
        public async Task OpenAiEmbeddingClientThrowsProviderOperationTimeoutExceptionWhenTimedOut()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer(embeddingDelayMs: 200);
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;

            using TimeoutPostEmbeddingClient client = new TimeoutPostEmbeddingClient(provider.BaseUrl, logging, 50);

            ProviderOperationTimeoutException ex = await Assert.ThrowsAsync<ProviderOperationTimeoutException>(
                () => client.EmbedAsync("Timeout me", "text-embedding-3-small"));

            Assert.Equal(50, ex.TimeoutMs);
            Assert.True(ex.Message.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.NotEmpty(client.CallDetails);
            Assert.True(client.CallDetails[0].Error?.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public async Task OpenAiCompletionClientClampsRequestedTimeoutToEndpointMaximum()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer(completionDelayMs: 200);
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;

            using TimeoutPostCompletionClient client = new TimeoutPostCompletionClient(provider.BaseUrl, logging, 75);

            ProviderOperationTimeoutException ex = await Assert.ThrowsAsync<ProviderOperationTimeoutException>(
                () => client.GenerateCompletionAsync("Timeout me", "gpt-4.1-mini", 64, 5000));

            Assert.Equal(75, ex.TimeoutMs);
            Assert.True(ex.Message.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.NotEmpty(client.CallDetails);
            Assert.True(client.CallDetails[0].Error?.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public async Task PartioOwnedHttpClientsReturnCopiedResponseDataWithoutRetainingResponses()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer();
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;

            using TimeoutPostEmbeddingClient embeddingClient = new TimeoutPostEmbeddingClient(provider.BaseUrl, logging, 5000);
            using TimeoutPostCompletionClient completionClient = new TimeoutPostCompletionClient(provider.BaseUrl, logging, 5000);

            EmbeddingHttpResult embeddingResult = await embeddingClient.PostProbeAsync();
            CompletionHttpResult completionResult = await completionClient.PostProbeAsync();

            Assert.Null(embeddingResult.Response);
            Assert.Equal(200, embeddingResult.StatusCode);
            Assert.True(embeddingResult.IsSuccessStatusCode);
            Assert.Contains("Content-Type", embeddingResult.ResponseHeaders.Keys);
            Assert.Contains("\"data\"", embeddingResult.ResponseBody);

            Assert.Null(completionResult.Response);
            Assert.Equal(200, completionResult.StatusCode);
            Assert.True(completionResult.IsSuccessStatusCode);
            Assert.Contains("Content-Type", completionResult.ResponseHeaders.Keys);
            Assert.Contains("chatcmpl_stub", completionResult.ResponseBody);
        }

        [Fact]
        public async Task OpenAiEmbeddingClientPropagatesCallerCancellationAndReleasesConcurrencySlot()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer(embeddingDelayMs: 500);
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;

            using OpenAiEmbeddingClient client = new OpenAiEmbeddingClient(provider.BaseUrl, null, logging, 5000, "eep_cancel_test", 1);
            using CancellationTokenSource cts = new CancellationTokenSource(50);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => client.EmbedBatchAsync(new List<string> { "cancel this request" }, "text-embedding-3-small", cts.Token));

            List<List<float>> secondResult = await client.EmbedBatchAsync(new List<string> { "slot should be free" }, "text-embedding-3-small");

            Assert.Single(secondResult);
            Assert.DoesNotContain(client.CallDetails, detail => detail.Error?.IndexOf("max concurrent request", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public async Task SummarizationCapsParallelCallsToCompletionEndpointLimit()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer(completionDelayMs: 150);
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;

            using OpenAiCompletionClient client = new OpenAiCompletionClient(provider.BaseUrl, null, logging, 5000, "cep_summary_limit", 1);
            SummarizationEngine engine = new SummarizationEngine(logging);
            SummarizationConfiguration config = new SummarizationConfiguration
            {
                CompletionEndpointId = "cep_summary_limit",
                MinCellLength = 0,
                MaxParallelTasks = 4,
                MaxRetries = 4,
                MaxRetriesPerSummary = 0,
                MaxSummaryTokens = 128,
                TimeoutMs = 5000
            };

            List<SemanticCellRequest> cells = Enumerable.Range(0, 4)
                .Select(index => new SemanticCellRequest
                {
                    Type = AtomTypeEnum.Text,
                    Text = "Document section " + index + " has enough text to be summarized by the upstream test provider."
                })
                .ToList();

            List<SemanticCellRequest> summarized = await engine.SummarizeAsync(cells, config, client, "gpt-4.1-mini");

            await provider.WaitForCompletionRequestCountAsync(4);

            Assert.Equal(4, provider.CompletionRequestCount);
            Assert.Equal(1, provider.MaxActiveCompletionRequests);
            Assert.All(summarized, cell =>
                Assert.Contains(cell.Children ?? new List<SemanticCellRequest>(), child => child.Type == AtomTypeEnum.Summary));
        }

        [Fact]
        public void PartioServerMapsProviderOperationTimeoutTo504()
        {
            MethodInfo? mapMethod = typeof(Partio.Server.PartioServer).GetMethod(
                "MapExceptionToStatusCode",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(mapMethod);

            int statusCode = (int)mapMethod!.Invoke(null, new object[] { new ProviderOperationTimeoutException("timed out", 123) })!;
            Assert.Equal(504, statusCode);
        }

        [Fact]
        public void PartioServerMapsProviderConcurrencyLimitTo429()
        {
            MethodInfo? mapMethod = typeof(Partio.Server.PartioServer).GetMethod(
                "MapExceptionToStatusCode",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(mapMethod);

            int statusCode = (int)mapMethod!.Invoke(null, new object[] { new ProviderConcurrencyLimitException("eep_test", 1, "too many requests") })!;
            Assert.Equal(429, statusCode);
        }

        [Fact]
        public async Task EmbeddingClientsRejectWhenConcurrentLimitReached()
        {
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;

            using ConcurrencyProbeEmbeddingClient first = new ConcurrencyProbeEmbeddingClient("http://localhost", logging, "eep_test", 1);
            using ConcurrencyProbeEmbeddingClient second = new ConcurrencyProbeEmbeddingClient("http://localhost", logging, "eep_test", 1);

            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            Task firstTask = first.HoldSlotAsync(entered, release.Task);
            await entered.Task;

            ProviderConcurrencyLimitException ex = await Assert.ThrowsAsync<ProviderConcurrencyLimitException>(
                () => second.TryAcquireAndReleaseAsync());

            Assert.Equal(1, ex.MaxConcurrentRequests);
            Assert.NotEmpty(second.CallDetails);
            Assert.True(second.CallDetails[0].Error?.IndexOf("max concurrent request", StringComparison.OrdinalIgnoreCase) >= 0);

            release.TrySetResult(true);
            await firstTask;
        }

        [Fact]
        public async Task OllamaEmbeddingClientCanBeReusedAcrossMultipleCalls()
        {
            using SlowOllamaCompatibleServer provider = new SlowOllamaCompatibleServer();
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;

            using OllamaEmbeddingClient client = new OllamaEmbeddingClient(provider.BaseUrl, null, logging, 5000);

            List<List<float>> first = await client.EmbedBatchAsync(new List<string> { "first input" }, "all-minilm");
            List<List<float>> second = await client.EmbedBatchAsync(new List<string> { "second input" }, "all-minilm");

            await provider.WaitForEmbeddingRequestCountAsync(2);

            Assert.Single(first);
            Assert.Single(second);
            Assert.Equal(2, provider.EmbeddingRequestCount);
            Assert.Equal(2, client.CallDetails.Count(d => d.Success));
        }

        [Fact]
        public async Task OllamaCompletionClientSupportsParallelCallsOnSharedInstance()
        {
            using SlowOllamaCompatibleServer provider = new SlowOllamaCompatibleServer(chatDelayMs: 200);
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;

            using OllamaCompletionClient client = new OllamaCompletionClient(provider.BaseUrl, null, logging, 5000, "cep_test", 2);

            Task<string?> firstTask = client.GenerateCompletionAsync("first prompt", "gemma3:4b", 64, 5000);
            await provider.WaitForChatRequestCountAsync(1);

            Task<string?> secondTask = client.GenerateCompletionAsync("second prompt", "gemma3:4b", 64, 5000);
            string?[] results = await Task.WhenAll(firstTask, secondTask);

            await provider.WaitForChatRequestCountAsync(2);

            Assert.All(results, result => Assert.Equal("Stub Ollama response.", result));
            Assert.Equal(2, provider.ChatRequestCount);
            Assert.Equal(2, client.CallDetails.Count(d => d.Success));
        }

        [Fact]
        public async Task OpenAiEmbeddingClientCanBeReusedAcrossMultipleCalls()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer();
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;

            using OpenAiEmbeddingClient client = new OpenAiEmbeddingClient(provider.BaseUrl, null, logging, 5000);

            List<List<float>> first = await client.EmbedBatchAsync(new List<string> { "first input" }, "text-embedding-3-small");
            List<List<float>> second = await client.EmbedBatchAsync(new List<string> { "second input" }, "text-embedding-3-small");

            await provider.WaitForEmbeddingRequestCountAsync(2);

            Assert.Single(first);
            Assert.Single(second);
            Assert.Equal(2, provider.EmbeddingRequestCount);
            Assert.Equal(2, client.CallDetails.Count(d => d.Success));
        }

        [Fact]
        public async Task OpenAiCompletionClientSupportsParallelCallsOnSharedInstance()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer(completionDelayMs: 200);
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;

            using OpenAiCompletionClient client = new OpenAiCompletionClient(provider.BaseUrl, null, logging, 5000, "cep_test", 2);

            Task<string?> firstTask = client.GenerateCompletionAsync("first prompt", "gpt-4.1-mini", 64, 5000);
            await provider.WaitForCompletionRequestCountAsync(1);

            Task<string?> secondTask = client.GenerateCompletionAsync("second prompt", "gpt-4.1-mini", 64, 5000);
            string?[] results = await Task.WhenAll(firstTask, secondTask);

            await provider.WaitForCompletionRequestCountAsync(2);

            Assert.All(results, result => Assert.Equal("Stub completion response.", result));
            Assert.Equal(2, provider.CompletionRequestCount);
            Assert.Equal(2, client.CallDetails.Count(d => d.Success));
        }

        [Fact]
        public async Task GeminiEmbeddingClientCanBeReusedAcrossMultipleCalls()
        {
            using SlowGeminiCompatibleServer provider = new SlowGeminiCompatibleServer();
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;

            using GeminiEmbeddingClient client = new GeminiEmbeddingClient(provider.BaseUrl, "test-key", logging, 5000);

            List<List<float>> first = await client.EmbedBatchAsync(new List<string> { "first input" }, "text-embedding-004");
            List<List<float>> second = await client.EmbedBatchAsync(new List<string> { "second input" }, "text-embedding-004");

            await provider.WaitForEmbeddingRequestCountAsync(2);

            Assert.Single(first);
            Assert.Single(second);
            Assert.Equal(2, provider.EmbeddingRequestCount);
            Assert.Equal(2, client.CallDetails.Count(d => d.Success));
        }

        [Fact]
        public async Task GeminiCompletionClientSupportsParallelCallsOnSharedInstance()
        {
            using SlowGeminiCompatibleServer provider = new SlowGeminiCompatibleServer(completionDelayMs: 200);
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;

            using GeminiCompletionClient client = new GeminiCompletionClient(provider.BaseUrl, "test-key", logging, 5000, "cep_test", 2);

            Task<string?> firstTask = client.GenerateCompletionAsync("first prompt", "gemini-2.5-flash", 64, 5000);
            await provider.WaitForCompletionRequestCountAsync(1);

            Task<string?> secondTask = client.GenerateCompletionAsync("second prompt", "gemini-2.5-flash", 64, 5000);
            string?[] results = await Task.WhenAll(firstTask, secondTask);

            await provider.WaitForCompletionRequestCountAsync(2);

            Assert.All(results, result => Assert.Equal("Stub Gemini response.", result));
            Assert.Equal(2, provider.CompletionRequestCount);
            Assert.Equal(2, client.CallDetails.Count(d => d.Success));
        }

        private sealed class TimeoutPostEmbeddingClient : EmbeddingClientBase
        {
            public TimeoutPostEmbeddingClient(string endpoint, LoggingModule logging, int maximumTimeoutMs)
                : base(endpoint, null, logging, maximumTimeoutMs)
            {
            }

            public override async Task<List<float>> EmbedAsync(string text, string model, CancellationToken token = default)
            {
                using StringContent content = new StringContent("{\"input\":\"" + text + "\"}", System.Text.Encoding.UTF8, "application/json");
                await PostAndRecordAsync(_Endpoint.TrimEnd('/') + "/v1/embeddings", content, "{\"input\":\"" + text + "\"}", "EmbeddingRequest", token).ConfigureAwait(false);
                return new List<float>();
            }

            public async Task<EmbeddingHttpResult> PostProbeAsync()
            {
                string body = "{\"input\":\"probe\",\"model\":\"text-embedding-3-small\"}";
                using StringContent content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
                return await PostAndRecordAsync(_Endpoint.TrimEnd('/') + "/v1/embeddings", content, body, "EmbeddingRequest", CancellationToken.None).ConfigureAwait(false);
            }

            public override Task<List<List<float>>> EmbedBatchAsync(List<string> texts, string model, CancellationToken token = default)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class TimeoutPostCompletionClient : CompletionClientBase
        {
            public TimeoutPostCompletionClient(string endpoint, LoggingModule logging, int maximumTimeoutMs)
                : base(endpoint, null, logging, maximumTimeoutMs)
            {
            }

            public override async Task<string?> GenerateCompletionAsync(
                string prompt,
                string model,
                int maxTokens,
                int timeoutMs,
                CancellationToken token = default,
                string? systemPrompt = null)
            {
                using StringContent content = new StringContent("{\"prompt\":\"" + prompt + "\"}", System.Text.Encoding.UTF8, "application/json");
                CompletionHttpResult result = await PostAndRecordAsync(
                    _Endpoint.TrimEnd('/') + "/v1/chat/completions",
                    content,
                    "{\"prompt\":\"" + prompt + "\"}",
                    timeoutMs,
                    token).ConfigureAwait(false);
                return result.ResponseBody;
            }

            public async Task<CompletionHttpResult> PostProbeAsync()
            {
                string body = "{\"prompt\":\"probe\"}";
                using StringContent content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
                return await PostAndRecordAsync(
                    _Endpoint.TrimEnd('/') + "/v1/chat/completions",
                    content,
                    body,
                    5000,
                    CancellationToken.None).ConfigureAwait(false);
            }
        }

        private sealed class ConcurrencyProbeEmbeddingClient : EmbeddingClientBase
        {
            public ConcurrencyProbeEmbeddingClient(string endpoint, LoggingModule logging, string concurrencyKey, int maxConcurrentRequests)
                : base(endpoint, null, logging, 60000, concurrencyKey, maxConcurrentRequests)
            {
            }

            public async Task HoldSlotAsync(TaskCompletionSource<bool> entered, Task releaseTask)
            {
                using IDisposable lease = AcquireRequestSlot();
                entered.TrySetResult(true);
                await releaseTask.ConfigureAwait(false);
            }

            public Task TryAcquireAndReleaseAsync()
            {
                try
                {
                    using IDisposable lease = AcquireRequestSlot();
                    return Task.CompletedTask;
                }
                catch (ProviderConcurrencyLimitException ex)
                {
                    RecordRejectedCall("EmbeddingRequest", _Endpoint, "POST", ex.Message);
                    return Task.FromException(ex);
                }
            }

            public override Task<List<float>> EmbedAsync(string text, string model, CancellationToken token = default)
            {
                throw new NotSupportedException();
            }

            public override Task<List<List<float>>> EmbedBatchAsync(List<string> texts, string model, CancellationToken token = default)
            {
                throw new NotSupportedException();
            }
        }
    }
}
