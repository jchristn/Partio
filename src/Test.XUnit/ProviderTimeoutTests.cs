namespace Test.XUnit
{
    using System.Reflection;
    using Partio.Core.Exceptions;
    using Partio.Core.Models;
    using Partio.Core.Settings;
    using Partio.Core.ThirdParty;
    using SyslogLogging;
    using Test.Shared;
    using Xunit;

    public class ProviderTimeoutTests
    {
        [Fact]
        public void EndpointAndSettingsTimeoutsDefaultAndClamp()
        {
            EmbeddingEndpoint embeddingEndpoint = new EmbeddingEndpoint();
            CompletionEndpoint completionEndpoint = new CompletionEndpoint();
            DefaultEmbeddingEndpoint defaultEmbeddingEndpoint = new DefaultEmbeddingEndpoint();
            DefaultInferenceEndpoint defaultInferenceEndpoint = new DefaultInferenceEndpoint();

            Assert.Equal(60000, embeddingEndpoint.MaximumTimeoutMs);
            Assert.Equal(60000, completionEndpoint.MaximumTimeoutMs);
            Assert.Equal(60000, defaultEmbeddingEndpoint.MaximumTimeoutMs);
            Assert.Equal(60000, defaultInferenceEndpoint.MaximumTimeoutMs);

            embeddingEndpoint.MaximumTimeoutMs = 0;
            completionEndpoint.MaximumTimeoutMs = -42;
            defaultEmbeddingEndpoint.MaximumTimeoutMs = 0;
            defaultInferenceEndpoint.MaximumTimeoutMs = -1;

            Assert.Equal(1, embeddingEndpoint.MaximumTimeoutMs);
            Assert.Equal(1, completionEndpoint.MaximumTimeoutMs);
            Assert.Equal(1, defaultEmbeddingEndpoint.MaximumTimeoutMs);
            Assert.Equal(1, defaultInferenceEndpoint.MaximumTimeoutMs);
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
        public void PartioServerMapsProviderOperationTimeoutTo504()
        {
            MethodInfo? mapMethod = typeof(Partio.Server.PartioServer).GetMethod(
                "MapExceptionToStatusCode",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(mapMethod);

            int statusCode = (int)mapMethod!.Invoke(null, new object[] { new ProviderOperationTimeoutException("timed out", 123) })!;
            Assert.Equal(504, statusCode);
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
        }
    }
}
