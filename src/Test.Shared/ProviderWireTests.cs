namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using Partio.Core.ThirdParty;
    using SyslogLogging;
    using Touchstone.Core;

    /// <summary>
    /// Wire-level tests for the PolyPrompt-backed provider wrappers added for Azure OpenAI, Anthropic, Cohere,
    /// AWS Bedrock, Google Vertex AI, Hugging Face TEI, and VoyageAI. Each wrapper is pointed at an in-process
    /// <see cref="ScriptedProviderServer"/> so the test can assert the path, query, credential headers, and
    /// request body each provider receives.
    /// </summary>
    public static class ProviderWireTests
    {
        private const string Vector = "[0.1,0.2,0.3]";

        /// <summary>
        /// Build the Touchstone suite of provider wire tests.
        /// </summary>
        /// <returns>A suite descriptor exposing every case.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                "ProviderWire",
                "Provider wrapper wire tests for Azure OpenAI, Anthropic, Cohere, Bedrock, Vertex AI, TEI, and VoyageAI",
                BuildCases());
        }

        private static List<TestCaseDescriptor> BuildCases()
        {
            List<TestCaseDescriptor> tests = new List<TestCaseDescriptor>();

            tests.Add(TestCaseFactory.Async("ProviderWire", "Azure OpenAI completion targets the deployment with api-key and api-version", async () =>
            {
                using ScriptedProviderServer server = new ScriptedProviderServer()
                    .On("POST", "/openai/deployments/gpt-4o-prod/chat/completions", ChatCompletion("Stub Azure response."));

                using AzureOpenAiCompletionClient client = new AzureOpenAiCompletionClient(
                    server.BaseUrl + "/openai", "gpt-4o-prod", "azure-secret",
                    new AzureOpenAiProviderSettings { ApiVersion = "2025-01-01-preview" }, Quiet(), 5000);

                string? result = await client.GenerateCompletionAsync("hello", "gpt-4o-prod", 32, 5000);

                Check.Equal("Stub Azure response.", result);
                RecordedRequest request = Check.Single(server.Requests);
                Check.Contains("api-version=2025-01-01-preview", request.Query);
                Check.Equal("azure-secret", request.Header("api-key"));
                Check.Null(request.Header("Authorization"), "api-key auth sends no bearer token");
            }));

            tests.Add(TestCaseFactory.Async("ProviderWire", "Azure OpenAI bearer mode sends an Entra token and embeds per deployment", async () =>
            {
                using ScriptedProviderServer server = new ScriptedProviderServer()
                    .On("POST", "/openai/deployments/embed-small/embeddings", OpenAiEmbeddings(2));

                using AzureOpenAiEmbeddingClient client = new AzureOpenAiEmbeddingClient(
                    server.BaseUrl, "embed-small", "eyJ0eXAi.entra.token",
                    new AzureOpenAiProviderSettings { AuthMode = AzureAuthModeEnum.BearerToken }, Quiet(), 5000);

                List<List<float>> vectors = await client.EmbedBatchAsync(new List<string> { "a", "b" }, "embed-small");

                Check.Equal(2, vectors.Count);
                RecordedRequest request = Check.Single(server.Requests);
                Check.Equal("Bearer eyJ0eXAi.entra.token", request.Header("Authorization"));
                Check.Null(request.Header("api-key"), "bearer mode sends no api-key");
                Check.Contains("api-version=" + ProviderDefaults.AzureOpenAiApiVersion, request.Query);
            }));

            tests.Add(TestCaseFactory.Async("ProviderWire", "Anthropic completion uses the Messages API with x-api-key and workspace headers", async () =>
            {
                using ScriptedProviderServer server = new ScriptedProviderServer()
                    .On("POST", "/v1/messages", AnthropicMessage("Stub Anthropic response."));

                using AnthropicCompletionClient client = new AnthropicCompletionClient(
                    server.BaseUrl + "/v1", "sk-ant-secret",
                    new AnthropicProviderSettings { WorkspaceId = "wrkspc_test" }, Quiet(), 5000);

                string? result = await client.GenerateCompletionAsync("hello", "claude-sonnet-4-5", 32, 5000, default, "be terse");

                Check.Equal("Stub Anthropic response.", result);
                RecordedRequest request = Check.Single(server.Requests);
                Check.Equal("sk-ant-secret", request.Header("x-api-key"));
                Check.Equal(ProviderDefaults.AnthropicVersion, request.Header("anthropic-version"));
                Check.Equal("wrkspc_test", request.Header("anthropic-workspace-id"));
                Check.Contains("claude-sonnet-4-5", request.Body);
                Check.Contains("be terse", request.Body);
            }));

            tests.Add(TestCaseFactory.Async("ProviderWire", "Cohere completion and embedding use the v2 API with the configured input type", async () =>
            {
                using ScriptedProviderServer server = new ScriptedProviderServer()
                    .On("POST", "/v2/chat", CohereChat("Stub Cohere response."))
                    .On("POST", "/v2/embed", "{\"id\":\"e1\",\"embeddings\":{\"float\":[" + Vector + "," + Vector + "]}}");

                using CohereCompletionClient chat = new CohereCompletionClient(server.BaseUrl, "co-secret", null, Quiet(), 5000);
                using CohereEmbeddingClient embed = new CohereEmbeddingClient(server.BaseUrl + "/v2/", "co-secret",
                    new CohereProviderSettings { InputType = "search_query" }, Quiet(), 5000);

                string? reply = await chat.GenerateCompletionAsync("hello", "command-a-03-2025", 32, 5000);
                List<List<float>> vectors = await embed.EmbedBatchAsync(new List<string> { "a", "b" }, "embed-v4.0");

                Check.Equal("Stub Cohere response.", reply);
                Check.Equal(2, vectors.Count);
                Check.All(server.Requests, r => Check.Equal("Bearer co-secret", r.Header("Authorization")));
                RecordedRequest embedRequest = Check.Single(server.RequestsTo(p => p == "/v2/embed"));
                Check.Contains("search_query", embedRequest.Body);
                Check.Null(await embed.GetModelCapabilitiesAsync("embed-v4.0"), "Cohere defers to the server tokenization defaults");
            }));

            tests.Add(TestCaseFactory.Async("ProviderWire", "Bedrock completion signs Converse requests with SigV4", async () =>
            {
                using ScriptedProviderServer server = new ScriptedProviderServer()
                    .On("POST", p => p.StartsWith("/model/", StringComparison.Ordinal) && p.EndsWith("/converse", StringComparison.Ordinal),
                        "{\"output\":{\"message\":{\"role\":\"assistant\",\"content\":[{\"text\":\"Stub Bedrock response.\"}]}},\"stopReason\":\"end_turn\",\"usage\":{\"inputTokens\":3,\"outputTokens\":3,\"totalTokens\":6}}");

                BedrockProviderSettings settings = new BedrockProviderSettings
                {
                    Region = "us-west-2",
                    AccessKeyId = "AKIAPARTIOTEST",
                    SecretAccessKey = "partio/test/secret",
                    SessionToken = "partio-session-token"
                };
                using BedrockCompletionClient client = new BedrockCompletionClient(server.BaseUrl, settings, Quiet(), 5000);

                string? result = await client.GenerateCompletionAsync("hello", "anthropic.claude-3-5-sonnet-20240620-v1:0", 32, 5000);

                Check.Equal("Stub Bedrock response.", result);
                RecordedRequest request = Check.Single(server.Requests);
                Check.Contains("anthropic.claude-3-5-sonnet-20240620-v1:0", request.Path);
                string authorization = request.Header("Authorization") ?? string.Empty;
                Check.True(authorization.StartsWith("AWS4-HMAC-SHA256 ", StringComparison.Ordinal), "SigV4 authorization expected, got: " + authorization);
                Check.Contains("Credential=AKIAPARTIOTEST/", authorization);
                Check.Contains("/us-west-2/bedrock/aws4_request", authorization);
                Check.False(authorization.Contains("partio/test/secret", StringComparison.Ordinal), "the secret key must never be sent");
                Check.Equal("partio-session-token", request.Header("X-Amz-Security-Token"));
                Check.NotNull(request.Header("X-Amz-Date"));
            }));

            tests.Add(TestCaseFactory.Async("ProviderWire", "Bedrock embedding signs InvokeModel requests per input", async () =>
            {
                using ScriptedProviderServer server = new ScriptedProviderServer()
                    .On("POST", p => p.StartsWith("/model/", StringComparison.Ordinal) && p.EndsWith("/invoke", StringComparison.Ordinal),
                        "{\"embedding\":" + Vector + ",\"inputTextTokenCount\":2}");

                using BedrockEmbeddingClient client = new BedrockEmbeddingClient(server.BaseUrl,
                    new BedrockProviderSettings { Region = "us-east-1", AccessKeyId = "AKIAPARTIOTEST", SecretAccessKey = "secret" }, Quiet(), 5000);

                List<List<float>> vectors = await client.EmbedBatchAsync(new List<string> { "a", "b" }, "amazon.titan-embed-text-v2:0");

                Check.Equal(2, vectors.Count);
                Check.NotEmpty(server.Requests);
                Check.All(server.Requests, r => Check.Contains("amazon.titan-embed-text-v2:0", r.Path));
                Check.All(server.Requests, r => Check.Contains("/us-east-1/bedrock/aws4_request", r.Header("Authorization") ?? string.Empty));
                Check.Null(await client.GetModelCapabilitiesAsync("amazon.titan-embed-text-v2:0"), "Bedrock defers to the server tokenization defaults");
            }));

            tests.Add(TestCaseFactory.Async("ProviderWire", "Vertex AI exchanges a service-account JWT once and reuses the token", async () =>
            {
                using ScriptedProviderServer server = new ScriptedProviderServer();
                server
                    .On("POST", "/token", "{\"access_token\":\"ya29.partio-test\",\"expires_in\":3600,\"token_type\":\"Bearer\"}")
                    .On("POST", p => p.EndsWith(":generateContent", StringComparison.Ordinal), GeminiContent("Stub Vertex response."));

                VertexAiProviderSettings settings = new VertexAiProviderSettings
                {
                    ProjectId = "partio-test-project",
                    Region = "us-central1",
                    CredentialSource = GcpCredentialSourceEnum.ServiceAccountJson,
                    ServiceAccountJson = BuildServiceAccountJson(server.BaseUrl + "/token")
                };

                using (VertexAiCompletionClient first = new VertexAiCompletionClient(server.BaseUrl, null, settings, Quiet(), 5000))
                    Check.Equal("Stub Vertex response.", await first.GenerateCompletionAsync("hello", "gemini-2.5-flash", 32, 5000));
                using (VertexAiCompletionClient second = new VertexAiCompletionClient(server.BaseUrl, null, settings, Quiet(), 5000))
                    Check.Equal("Stub Vertex response.", await second.GenerateCompletionAsync("again", "gemini-2.5-flash", 32, 5000));

                RecordedRequest tokenRequest = Check.Single(server.RequestsTo(p => p == "/token"), "the token is exchanged once and cached across clients");
                Check.Contains("jwt-bearer", Uri.UnescapeDataString(tokenRequest.Body));

                List<RecordedRequest> generate = server.RequestsTo(p => p.EndsWith(":generateContent", StringComparison.Ordinal));
                Check.Equal(2, generate.Count);
                Check.All(generate, r => Check.Equal("Bearer ya29.partio-test", r.Header("Authorization")));
                Check.All(generate, r => Check.Contains("/projects/partio-test-project/locations/us-central1/", r.Path));
                Check.All(generate, r => Check.Contains("gemini-2.5-flash", r.Path));
            }));

            tests.Add(TestCaseFactory.Async("ProviderWire", "Vertex AI embedding with an access token sends it as a bearer token", async () =>
            {
                using ScriptedProviderServer server = new ScriptedProviderServer()
                    .On("POST", p => p.EndsWith(":predict", StringComparison.Ordinal),
                        "{\"predictions\":[{\"embeddings\":{\"values\":" + Vector + "}},{\"embeddings\":{\"values\":" + Vector + "}}]}");

                VertexAiProviderSettings settings = new VertexAiProviderSettings
                {
                    ProjectId = "partio-test-project",
                    Region = "europe-west4",
                    CredentialSource = GcpCredentialSourceEnum.AccessToken
                };
                using VertexAiEmbeddingClient client = new VertexAiEmbeddingClient(server.BaseUrl, "ya29.static-token", settings, Quiet(), 5000);

                List<List<float>> vectors = await client.EmbedBatchAsync(new List<string> { "a", "b" }, "text-embedding-005");

                Check.Equal(2, vectors.Count);
                RecordedRequest request = Check.Single(server.Requests);
                Check.Equal("Bearer ya29.static-token", request.Header("Authorization"));
                Check.Contains("/locations/europe-west4/", request.Path);
                Check.Contains("text-embedding-005", request.Path);
            }));

            tests.Add(TestCaseFactory.Async("ProviderWire", "TEI embedding posts to /embed and sends the optional bearer token", async () =>
            {
                using ScriptedProviderServer server = new ScriptedProviderServer()
                    .On("POST", "/embed", "[" + Vector + "," + Vector + "]");

                using TeiEmbeddingClient anonymous = new TeiEmbeddingClient(server.BaseUrl + "/", null, Quiet(), 5000);
                using TeiEmbeddingClient keyed = new TeiEmbeddingClient(server.BaseUrl, "tei-token", Quiet(), 5000);

                Check.Equal(2, (await anonymous.EmbedBatchAsync(new List<string> { "a", "b" }, "bge-small")).Count);
                Check.Equal(2, (await keyed.EmbedBatchAsync(new List<string> { "a", "b" }, "bge-small")).Count);

                List<RecordedRequest> requests = server.Requests.ToList();
                Check.Equal(2, requests.Count);
                Check.Null(requests[0].Header("Authorization"), "no key: anonymous request");
                Check.Equal("Bearer tei-token", requests[1].Header("Authorization"));
            }));

            tests.Add(TestCaseFactory.Async("ProviderWire", "VoyageAI embedding sends input type and output dimension", async () =>
            {
                using ScriptedProviderServer server = new ScriptedProviderServer()
                    .On("POST", "/v1/embeddings", OpenAiEmbeddings(1));

                using VoyageAiEmbeddingClient client = new VoyageAiEmbeddingClient(server.BaseUrl + "/v1", "pa-secret",
                    new VoyageAiProviderSettings { InputType = "document", OutputDimension = 512 }, Quiet(), 5000);

                List<List<float>> vectors = await client.EmbedBatchAsync(new List<string> { "a" }, "voyage-3.5");

                Check.Equal(1, vectors.Count);
                RecordedRequest request = Check.Single(server.Requests);
                Check.Equal("Bearer pa-secret", request.Header("Authorization"));
                using JsonDocument body = JsonDocument.Parse(request.Body);
                Check.Equal("document", body.RootElement.GetProperty("input_type").GetString());
                Check.Equal(512, body.RootElement.GetProperty("output_dimension").GetInt32());
            }));

            tests.Add(TestCaseFactory.Async("ProviderWire", "Connectivity probes report reachability through the wrapper", async () =>
            {
                using ScriptedProviderServer server = new ScriptedProviderServer()
                    .On("POST", "/v1/embeddings", OpenAiEmbeddings(1));

                using VoyageAiEmbeddingClient healthy = new VoyageAiEmbeddingClient(server.BaseUrl, "pa-secret", null, Quiet(), 5000);
                Check.True(await healthy.ValidateConnectivityAsync("voyage-3.5", 5000), "VoyageAI probe should succeed against a working server");

                using ScriptedProviderServer broken = new ScriptedProviderServer()
                    .On("POST", "/v1/embeddings", "{\"detail\":\"invalid key\"}", 401);
                using VoyageAiEmbeddingClient unhealthy = new VoyageAiEmbeddingClient(broken.BaseUrl, "bad", null, Quiet(), 5000);
                Check.False(await unhealthy.ValidateConnectivityAsync("voyage-3.5", 5000), "a 401 should report unreachable");
            }));

            tests.Add(TestCaseFactory.Sync("ProviderWire", "New wrappers reject missing required configuration", () =>
            {
                LoggingModule logging = Quiet();
                Check.Throws<ArgumentException>(() => new AzureOpenAiCompletionClient("https://res.openai.azure.com", "dep", "", null, logging, 5000));
                Check.Throws<ArgumentException>(() => new AzureOpenAiEmbeddingClient("https://res.openai.azure.com", " ", "key", null, logging, 5000));
                Check.Throws<ArgumentException>(() => new AnthropicCompletionClient("https://api.anthropic.com", "", null, logging, 5000));
                Check.Throws<ArgumentException>(() => new CohereEmbeddingClient("https://api.cohere.com", "", null, logging, 5000));
                Check.Throws<ArgumentException>(() => new VoyageAiEmbeddingClient("https://api.voyageai.com", "", null, logging, 5000));
                Check.Throws<ArgumentException>(() => new BedrockCompletionClient("", new BedrockProviderSettings(), logging, 5000));
                Check.Throws<ArgumentException>(() => new VertexAiEmbeddingClient("", null, new VertexAiProviderSettings { Region = "us-central1" }, logging, 5000));
                Check.Throws<ArgumentException>(() => ProviderCredentialCache.GetVertexCredential(
                    new VertexAiProviderSettings { ProjectId = "p", Region = "r", CredentialSource = GcpCredentialSourceEnum.AccessToken }, null));
                Check.Throws<ArgumentException>(() => ProviderCredentialCache.GetAwsCredential(new BedrockProviderSettings { Region = "us-east-1" }));
            }));

            return tests;
        }

        private static LoggingModule Quiet()
        {
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;
            return logging;
        }

        private static string ChatCompletion(string text)
        {
            return "{\"id\":\"chatcmpl-1\",\"object\":\"chat.completion\",\"model\":\"m\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":"
                + JsonSerializer.Serialize(text) + "},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":3,\"completion_tokens\":3,\"total_tokens\":6}}";
        }

        private static string OpenAiEmbeddings(int count)
        {
            IEnumerable<string> items = Enumerable.Range(0, count).Select(i => "{\"object\":\"embedding\",\"index\":" + i + ",\"embedding\":" + Vector + "}");
            return "{\"object\":\"list\",\"data\":[" + string.Join(",", items) + "],\"model\":\"m\",\"usage\":{\"prompt_tokens\":2,\"total_tokens\":2}}";
        }

        private static string AnthropicMessage(string text)
        {
            return "{\"id\":\"msg_1\",\"type\":\"message\",\"role\":\"assistant\",\"model\":\"claude\",\"content\":[{\"type\":\"text\",\"text\":"
                + JsonSerializer.Serialize(text) + "}],\"stop_reason\":\"end_turn\",\"usage\":{\"input_tokens\":3,\"output_tokens\":3}}";
        }

        private static string CohereChat(string text)
        {
            return "{\"id\":\"c1\",\"finish_reason\":\"COMPLETE\",\"message\":{\"role\":\"assistant\",\"content\":[{\"type\":\"text\",\"text\":"
                + JsonSerializer.Serialize(text) + "}]},\"usage\":{\"tokens\":{\"input_tokens\":3,\"output_tokens\":3}}}";
        }

        private static string GeminiContent(string text)
        {
            return "{\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":" + JsonSerializer.Serialize(text)
                + "}]},\"finishReason\":\"STOP\"}],\"usageMetadata\":{\"promptTokenCount\":3,\"candidatesTokenCount\":3,\"totalTokenCount\":6}}";
        }

        /// <summary>
        /// Build a service-account key JSON with a freshly generated RSA key whose token_uri points at the scripted server.
        /// </summary>
        /// <param name="tokenUri">Token exchange URL.</param>
        /// <returns>The service-account JSON.</returns>
        internal static string BuildServiceAccountJson(string tokenUri)
        {
            using RSA rsa = RSA.Create(2048);
            Dictionary<string, string> key = new Dictionary<string, string>
            {
                ["type"] = "service_account",
                ["project_id"] = "partio-test-project",
                ["private_key_id"] = Guid.NewGuid().ToString("N"),
                ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
                ["client_email"] = "partio-test@partio-test-project.iam.gserviceaccount.com",
                ["token_uri"] = tokenUri
            };
            return JsonSerializer.Serialize(key);
        }
    }
}
