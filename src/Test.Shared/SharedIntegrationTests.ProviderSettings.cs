namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;

    /// <summary>
    /// Provider-settings cases for the PolyPrompt providers added in 0.5.2 (Azure OpenAI, Anthropic, Cohere,
    /// AWS Bedrock, Google Vertex AI, Hugging Face TEI, VoyageAI): server-side validation and normalization,
    /// secret masking on every read, the reveal APIs, echo-back of masked secrets on update, end-to-end
    /// completion/embedding/proxy traffic against a scripted upstream, and connectivity-probe health checks.
    /// These cases speak raw JSON so they pin the wire contract independently of the SDK models.
    /// </summary>
    public static partial class SharedIntegrationTests
    {
        private const string _AzureKey = "azure-key-0123456789abcd";
        private const string _AwsSecret = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";

        public static async Task TestProviderSettingsValidationAsync()
        {
            List<InvalidEndpointCase> invalid = new List<InvalidEndpointCase>
            {
                new InvalidEndpointCase("completion", "Ollama header auth without a header name", WithAuth(Endpoint("Ollama", "gemma3:4b", "http://127.0.0.1:1", "gw-key"),
                    new JsonObject { ["Mode"] = "Header" })),
                new InvalidEndpointCase("completion", "vLLM header auth on a reserved header", WithAuth(Endpoint("vLLM", "m", "http://127.0.0.1:1", "gw-key"),
                    new JsonObject { ["Mode"] = "Header", ["HeaderName"] = "Content-Length" })),
                new InvalidEndpointCase("embedding", "TEI query auth without a credential", WithAuth(Endpoint("TEI", "bge", "http://127.0.0.1:1"),
                    new JsonObject { ["Mode"] = "QueryParameter", ["QueryParameterName"] = "api_key" })),
                new InvalidEndpointCase("completion", "OpenAI basic auth without a user name", WithAuth(Endpoint("OpenAI", "gpt-4.1-mini", "https://api.openai.com", "pw"),
                    new JsonObject { ["Mode"] = "Basic" })),
                new InvalidEndpointCase("completion", "TEI as a completion endpoint", Endpoint("TEI", "bge-small", "http://127.0.0.1:1")),
                new InvalidEndpointCase("embedding", "Ollama with an empty endpoint", Endpoint("Ollama", "nomic-embed-text", "")),
                new InvalidEndpointCase("completion", "VoyageAI as a completion endpoint", Endpoint("VoyageAI", "voyage-3.5", null, "pa-key")),
                new InvalidEndpointCase("embedding", "Anthropic as an embedding endpoint", Endpoint("Anthropic", "claude-sonnet-4-5", null, "sk-ant")),
                new InvalidEndpointCase("completion", "Azure OpenAI without a resource URL", Endpoint("AzureOpenAI", "gpt-4o", null, _AzureKey)),
                new InvalidEndpointCase("embedding", "Azure OpenAI without an ApiKey", Endpoint("AzureOpenAI", "embed", "https://res.openai.azure.com")),
                new InvalidEndpointCase("completion", "Anthropic without an ApiKey", Endpoint("Anthropic", "claude-sonnet-4-5", null)),
                new InvalidEndpointCase("embedding", "Cohere without an ApiKey", Endpoint("Cohere", "embed-v4.0", null)),
                new InvalidEndpointCase("completion", "Bedrock without ProviderSettings", Endpoint("Bedrock", "anthropic.claude-3-5-sonnet-20240620-v1:0", null)),
                new InvalidEndpointCase("completion", "Bedrock without a region", WithSettings(Endpoint("Bedrock", "m", null), "Bedrock",
                    new JsonObject { ["AccessKeyId"] = "AKIA", ["SecretAccessKey"] = _AwsSecret })),
                new InvalidEndpointCase("embedding", "Bedrock static credentials without a secret", WithSettings(Endpoint("Bedrock", "m", null), "Bedrock",
                    new JsonObject { ["Region"] = "us-east-1", ["AccessKeyId"] = "AKIA" })),
                new InvalidEndpointCase("completion", "Vertex AI without a project", WithSettings(Endpoint("VertexAI", "gemini-2.5-flash", null), "VertexAI",
                    new JsonObject { ["Region"] = "us-central1", ["CredentialSource"] = "ApplicationDefault" })),
                new InvalidEndpointCase("embedding", "Vertex AI with malformed service-account JSON", WithSettings(Endpoint("VertexAI", "text-embedding-005", null), "VertexAI",
                    new JsonObject { ["ProjectId"] = "p", ["Region"] = "us-central1", ["ServiceAccountJson"] = "{\"client_email\":\"a@b\"}" })),
            };

            foreach (InvalidEndpointCase testCase in invalid)
            {
                string kind = testCase.Kind;
                string description = testCase.Description;
                JsonObject body = testCase.Body;
                RawResponse statusResponse = await SendRawAsync(HttpMethod.Put, "/v1.0/endpoints/" + kind, body).ConfigureAwait(false);
                int status = statusResponse.Status;
                JsonNode? response = statusResponse.Body;
                if (status != 400) throw new Exception("Expected 400 for " + description + ", got " + status + ": " + response?.ToJsonString());
            }
        }

        public static async Task TestProviderEndpointNormalizationAsync()
        {
            List<CreatedEndpoint> created = new List<CreatedEndpoint>();
            try
            {
                JsonObject anthropic = await CreateRawAsync("completion", Endpoint("Anthropic", "claude-sonnet-4-5", null, "sk-ant-normalize-0001"), created).ConfigureAwait(false);
                Expect(anthropic, "Endpoint", "https://api.anthropic.com");
                Expect(anthropic, "HealthCheckUrl", "https://api.anthropic.com/v1/models");
                if (anthropic["HealthCheckUseAuth"]?.GetValue<bool>() != true) throw new Exception("Anthropic health checks should authenticate");

                JsonObject azure = await CreateRawAsync("completion", WithSettings(Endpoint("AzureOpenAI", "gpt-4o-prod", "https://res.openai.azure.com/openai/", _AzureKey), "AzureOpenAI",
                    new JsonObject { ["ApiVersion"] = "2025-01-01-preview" }), created).ConfigureAwait(false);
                Expect(azure, "Endpoint", "https://res.openai.azure.com");
                Expect(azure, "HealthCheckUrl", "https://res.openai.azure.com/openai/models?api-version=2025-01-01-preview");
                Expect(azure["ProviderSettings"]?["AzureOpenAI"], "AuthMode", "ApiKey");

                JsonObject bedrock = await CreateRawAsync("embedding", WithSettings(Endpoint("Bedrock", "amazon.titan-embed-text-v2:0", null), "Bedrock",
                    new JsonObject { ["Region"] = "eu-central-1", ["CredentialSource"] = "Environment" }), created).ConfigureAwait(false);
                Expect(bedrock, "Endpoint", "https://bedrock-runtime.eu-central-1.amazonaws.com");
                if (bedrock["HealthCheckUrl"] != null) throw new Exception("Bedrock endpoints are probed, so HealthCheckUrl should be null, got " + bedrock["HealthCheckUrl"]);

                JsonObject vertex = await CreateRawAsync("completion", WithSettings(Endpoint("VertexAI", "gemini-2.5-flash", null), "VertexAI",
                    new JsonObject { ["ProjectId"] = "partio-test", ["Region"] = "us-east4", ["CredentialSource"] = "ApplicationDefault" }), created).ConfigureAwait(false);
                Expect(vertex, "Endpoint", "https://us-east4-aiplatform.googleapis.com");

                JsonObject voyage = await CreateRawAsync("embedding", Endpoint("VoyageAI", "voyage-3.5", "", "pa-normalize-key-0001"), created).ConfigureAwait(false);
                Expect(voyage, "Endpoint", "https://api.voyageai.com");

                JsonObject cohere = await CreateRawAsync("embedding", WithSettings(Endpoint("Cohere", "embed-v4.0", "https://api.cohere.com/v2", "co-normalize-key-0001"), "Cohere",
                    new JsonObject { ["InputType"] = "search_document" }), created).ConfigureAwait(false);
                Expect(cohere, "Endpoint", "https://api.cohere.com");
                Expect(cohere["ProviderSettings"]?["Cohere"], "InputType", "search_document");

                // Sections for other providers are dropped rather than stored.
                JsonObject tei = await CreateRawAsync("embedding", WithSettings(Endpoint("TEI", "bge-small", "http://127.0.0.1:1/"), "Bedrock",
                    new JsonObject { ["Region"] = "us-east-1" }), created).ConfigureAwait(false);
                Expect(tei, "Endpoint", "http://127.0.0.1:1");
                Expect(tei, "HealthCheckUrl", "http://127.0.0.1:1/health");
                if (tei["ProviderSettings"] != null) throw new Exception("Unrelated provider settings should be dropped, got " + tei["ProviderSettings"]!.ToJsonString());

                // Values round-trip through the database.
                RawResponse statusResponse = await SendRawAsync(HttpMethod.Get, "/v1.0/endpoints/completion/" + azure["Id"], null).ConfigureAwait(false);
                int status = statusResponse.Status;
                JsonNode? read = statusResponse.Body;
                if (status != 200) throw new Exception("Read Azure endpoint returned " + status);
                Expect(read?["ProviderSettings"]?["AzureOpenAI"], "ApiVersion", "2025-01-01-preview");
                Expect(read, "ApiFormat", "AzureOpenAI");
            }
            finally
            {
                await DeleteRawAsync(created).ConfigureAwait(false);
            }
        }

        public static async Task TestEndpointSecretsMaskedAndRevealedAsync()
        {
            List<CreatedEndpoint> created = new List<CreatedEndpoint>();
            string serviceAccountJson = ProviderWireTests.BuildServiceAccountJson("https://oauth2.googleapis.com/token");
            try
            {
                // Enumeration is scoped to the caller's tenant (default for the admin token), so this endpoint lives there.
                JsonObject bedrockBody = Endpoint("Bedrock", "anthropic.claude-3-5-sonnet-20240620-v1:0", null);
                bedrockBody["TenantId"] = "default";
                JsonObject bedrock = await CreateRawAsync("completion", WithSettings(bedrockBody, "Bedrock",
                    new JsonObject
                    {
                        ["Region"] = "us-east-1",
                        ["AccessKeyId"] = "AKIAPARTIOEXAMPLE",
                        ["SecretAccessKey"] = _AwsSecret,
                        ["SessionToken"] = "IQoJb3JpZ2luX2VjEJrSession"
                    }), created).ConfigureAwait(false);
                string bedrockId = bedrock["Id"]!.GetValue<string>();

                // The create response, the read, and the enumeration are all masked.
                Expect(bedrock["ProviderSettings"]?["Bedrock"], "SecretAccessKey", "****" + _AwsSecret.Substring(_AwsSecret.Length - 4));
                Expect(bedrock["ProviderSettings"]?["Bedrock"], "SessionToken", "****sion");
                Expect(bedrock["ProviderSettings"]?["Bedrock"], "AccessKeyId", "AKIAPARTIOEXAMPLE");

                RawResponse readStatusResponse = await SendRawAsync(HttpMethod.Get, "/v1.0/endpoints/completion/" + bedrockId, null).ConfigureAwait(false);
                int readStatus = readStatusResponse.Status;
                JsonNode? read = readStatusResponse.Body;
                if (readStatus != 200) throw new Exception("Read returned " + readStatus);
                Expect(read?["ProviderSettings"]?["Bedrock"], "SecretAccessKey", "****" + _AwsSecret.Substring(_AwsSecret.Length - 4));

                RawResponse enumStatusResponse = await SendRawAsync(HttpMethod.Post, "/v1.0/endpoints/completion/enumerate", new JsonObject { ["MaxResults"] = 1000 }).ConfigureAwait(false);
                int enumStatus = enumStatusResponse.Status;
                JsonNode? listed = enumStatusResponse.Body;
                if (enumStatus != 200) throw new Exception("Enumerate returned " + enumStatus);
                JsonNode? listedBedrock = listed?["Data"]?.AsArray().FirstOrDefault(n => n?["Id"]?.GetValue<string>() == bedrockId);
                if (listedBedrock == null) throw new Exception("Bedrock endpoint missing from enumeration");
                if (listed!.ToJsonString().Contains(_AwsSecret, StringComparison.Ordinal)) throw new Exception("Enumeration leaked the AWS secret");

                // Reveal returns the stored values.
                JsonNode revealed = await RevealRawAsync("completion", bedrockId).ConfigureAwait(false);
                Expect(revealed["ProviderSettings"]?["Bedrock"], "SecretAccessKey", _AwsSecret);
                Expect(revealed["ProviderSettings"]?["Bedrock"], "SessionToken", "IQoJb3JpZ2luX2VjEJrSession");

                // Echoing the masked read back keeps the secrets; a new value replaces one; null clears one.
                JsonObject echo = read!.AsObject();
                echo["Name"] = "Bedrock Renamed";
                RawResponse echoStatusResponse = await SendRawAsync(HttpMethod.Put, "/v1.0/endpoints/completion/" + bedrockId, echo).ConfigureAwait(false);
                int echoStatus = echoStatusResponse.Status;
                JsonNode? echoed = echoStatusResponse.Body;
                if (echoStatus != 200) throw new Exception("Echo update returned " + echoStatus + ": " + echoed?.ToJsonString());
                Expect(echoed, "Name", "Bedrock Renamed");
                JsonNode afterEcho = await RevealRawAsync("completion", bedrockId).ConfigureAwait(false);
                Expect(afterEcho["ProviderSettings"]?["Bedrock"], "SecretAccessKey", _AwsSecret);
                Expect(afterEcho["ProviderSettings"]?["Bedrock"], "SessionToken", "IQoJb3JpZ2luX2VjEJrSession");

                JsonObject rotate = JsonNode.Parse(echoed!.ToJsonString())!.AsObject();
                rotate["ProviderSettings"]!["Bedrock"]!["SecretAccessKey"] = "rotated/secret/value/0000";
                rotate["ProviderSettings"]!["Bedrock"]!["SessionToken"] = null;
                RawResponse rotateStatusResponse = await SendRawAsync(HttpMethod.Put, "/v1.0/endpoints/completion/" + bedrockId, rotate).ConfigureAwait(false);
                int rotateStatus = rotateStatusResponse.Status;
                if (rotateStatus != 200) throw new Exception("Rotate update returned " + rotateStatus);
                JsonNode afterRotate = await RevealRawAsync("completion", bedrockId).ConfigureAwait(false);
                Expect(afterRotate["ProviderSettings"]?["Bedrock"], "SecretAccessKey", "rotated/secret/value/0000");
                if (afterRotate["ProviderSettings"]?["Bedrock"]?["SessionToken"] != null) throw new Exception("A null SessionToken should clear it");

                // Vertex AI: the service-account JSON is masked fully, and an echo keeps it.
                JsonObject vertex = await CreateRawAsync("embedding", WithSettings(Endpoint("VertexAI", "text-embedding-005", null), "VertexAI",
                    new JsonObject { ["ProjectId"] = "partio-test", ["Region"] = "us-central1", ["ServiceAccountJson"] = serviceAccountJson }), created).ConfigureAwait(false);
                string vertexId = vertex["Id"]!.GetValue<string>();
                Expect(vertex["ProviderSettings"]?["VertexAI"], "ServiceAccountJson", "****");
                Expect(vertex["ProviderSettings"]?["VertexAI"], "CredentialSource", "ServiceAccountJson");
                RawResponse vertexEchoStatusResponse = await SendRawAsync(HttpMethod.Put, "/v1.0/endpoints/embedding/" + vertexId, vertex).ConfigureAwait(false);
                int vertexEchoStatus = vertexEchoStatusResponse.Status;
                if (vertexEchoStatus != 200) throw new Exception("Vertex echo update returned " + vertexEchoStatus);
                Expect((await RevealRawAsync("embedding", vertexId).ConfigureAwait(false))["ProviderSettings"]?["VertexAI"], "ServiceAccountJson", serviceAccountJson);

                // ApiKey masking applies to every provider, including the original ones.
                JsonObject openai = await CreateRawAsync("embedding", Endpoint("OpenAI", "text-embedding-3-small", "https://api.openai.com", "sk-proj-abcdefghijklmnop1234"), created).ConfigureAwait(false);
                string openaiId = openai["Id"]!.GetValue<string>();
                Expect(openai, "ApiKey", "****1234");
                Expect(await RevealRawAsync("embedding", openaiId).ConfigureAwait(false), "ApiKey", "sk-proj-abcdefghijklmnop1234");
                openai["ApiKey"] = null;
                RawResponse clearStatusResponse = await SendRawAsync(HttpMethod.Put, "/v1.0/endpoints/embedding/" + openaiId, openai).ConfigureAwait(false);
                int clearStatus = clearStatusResponse.Status;
                JsonNode? cleared = clearStatusResponse.Body;
                if (clearStatus != 200) throw new Exception("Clear ApiKey update returned " + clearStatus);
                if (cleared?["ApiKey"] != null) throw new Exception("A null ApiKey should clear it, got " + cleared["ApiKey"]);

                RawResponse missingStatusResponse = await SendRawAsync(HttpMethod.Get, "/v1.0/endpoints/embedding/eep_does_not_exist/reveal", null).ConfigureAwait(false);
                int missingStatus = missingStatusResponse.Status;
                if (missingStatus != 404) throw new Exception("Reveal of a missing endpoint should return 404, got " + missingStatus);
            }
            finally
            {
                await DeleteRawAsync(created).ConfigureAwait(false);
            }
        }

        public static async Task TestRevealRequiresAdminAsync()
        {
            List<CreatedEndpoint> created = new List<CreatedEndpoint>();
            try
            {
                JsonObject cohere = await CreateRawAsync("completion", Endpoint("Cohere", "command-a-03-2025", null, "co-admin-only-key-0001"), created).ConfigureAwait(false);
                using HttpClient http = new HttpClient();
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, _Endpoint.TrimEnd('/') + "/v1.0/endpoints/completion/" + cohere["Id"] + "/reveal");
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _TestToken);
                using HttpResponseMessage response = await http.SendAsync(request).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if ((int)response.StatusCode < 400) throw new Exception("A non-admin token must not reveal secrets, got " + (int)response.StatusCode);
                if (body.Contains("co-admin-only-key-0001", StringComparison.Ordinal)) throw new Exception("The secret leaked to a non-admin caller");
            }
            finally
            {
                await DeleteRawAsync(created).ConfigureAwait(false);
            }
        }

        public static async Task TestAnthropicCompletionAndProxyAsync()
        {
            using ScriptedProviderServer upstream = new ScriptedProviderServer()
                .On("POST", "/v1/messages",
                    "{\"id\":\"msg_1\",\"type\":\"message\",\"role\":\"assistant\",\"model\":\"claude\",\"content\":[{\"type\":\"text\",\"text\":\"Stub Anthropic response.\"}],\"stop_reason\":\"end_turn\",\"usage\":{\"input_tokens\":3,\"output_tokens\":3}}");
            List<CreatedEndpoint> created = new List<CreatedEndpoint>();
            try
            {
                JsonObject endpoint = await CreateRawAsync("completion", WithSettings(Endpoint("Anthropic", "claude-sonnet-4-5", upstream.BaseUrl, "sk-ant-e2e-secret-0001"), "Anthropic",
                    new JsonObject { ["WorkspaceId"] = "wrkspc_e2e" }), created).ConfigureAwait(false);
                string id = endpoint["Id"]!.GetValue<string>();

                RawResponse statusResponse = await SendRawAsync(HttpMethod.Post, "/v1.0/completion",
                    new JsonObject { ["EndpointId"] = id, ["Prompt"] = "hello", ["MaxTokens"] = 32, ["TimeoutMs"] = 10000 }).ConfigureAwait(false);
                int status = statusResponse.Status;
                JsonNode? completion = statusResponse.Body;
                if (status != 200) throw new Exception("Completion returned " + status + ": " + completion?.ToJsonString());
                Expect(completion, "Output", "Stub Anthropic response.");

                RawResponse proxyStatusResponse = await SendRawAsync(HttpMethod.Post, "/v1.0/proxy/" + id + "/v1/messages",
                    new JsonObject { ["model"] = "claude-sonnet-4-5", ["max_tokens"] = 16, ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = "hi" }) }).ConfigureAwait(false);
                int proxyStatus = proxyStatusResponse.Status;
                JsonNode? proxied = proxyStatusResponse.Body;
                if (proxyStatus != 200) throw new Exception("Proxy returned " + proxyStatus + ": " + proxied?.ToJsonString());
                if (!proxied!.ToJsonString().Contains("Stub Anthropic response.", StringComparison.Ordinal)) throw new Exception("Proxy did not relay the Anthropic body");

                List<RecordedRequest> requests = upstream.RequestsTo(p => p == "/v1/messages");
                if (requests.Count != 2) throw new Exception("Expected 2 upstream requests, got " + requests.Count);
                foreach (RecordedRequest r in requests)
                {
                    if (r.Header("x-api-key") != "sk-ant-e2e-secret-0001") throw new Exception("Expected the x-api-key header, got " + r.Header("x-api-key"));
                    if (string.IsNullOrEmpty(r.Header("anthropic-version"))) throw new Exception("Expected an anthropic-version header");
                    if (r.Header("anthropic-workspace-id") != "wrkspc_e2e") throw new Exception("Expected the workspace header, got " + r.Header("anthropic-workspace-id"));
                    if (r.Header("Authorization") != null) throw new Exception("The Partio bearer token must not be forwarded, got " + r.Header("Authorization"));
                }

                RawResponse deniedStatusResponse = await SendRawAsync(HttpMethod.Post, "/v1.0/proxy/" + id + "/v1/chat/completions", new JsonObject()).ConfigureAwait(false);
                int deniedStatus = deniedStatusResponse.Status;
                if (deniedStatus != 404) throw new Exception("Expected 404 for an OpenAI path on an Anthropic endpoint, got " + deniedStatus);
            }
            finally
            {
                await DeleteRawAsync(created).ConfigureAwait(false);
            }
        }

        public static async Task TestAzureOpenAiProxyAppendsApiVersionAsync()
        {
            using ScriptedProviderServer upstream = new ScriptedProviderServer()
                .On("POST", "/openai/deployments/gpt-4o-prod/chat/completions",
                    "{\"id\":\"c1\",\"object\":\"chat.completion\",\"model\":\"gpt-4o\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"Stub Azure response.\"},\"finish_reason\":\"stop\"}]}");
            List<CreatedEndpoint> created = new List<CreatedEndpoint>();
            try
            {
                JsonObject endpoint = await CreateRawAsync("completion", WithSettings(Endpoint("AzureOpenAI", "gpt-4o-prod", upstream.BaseUrl, _AzureKey), "AzureOpenAI",
                    new JsonObject { ["ApiVersion"] = "2025-01-01-preview" }), created).ConfigureAwait(false);
                string id = endpoint["Id"]!.GetValue<string>();

                RawResponse statusResponse = await SendRawAsync(HttpMethod.Post, "/v1.0/proxy/" + id + "/openai/deployments/gpt-4o-prod/chat/completions",
                    new JsonObject { ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = "hi" }) }).ConfigureAwait(false);
                int status = statusResponse.Status;
                JsonNode? proxied = statusResponse.Body;
                if (status != 200) throw new Exception("Azure proxy returned " + status + ": " + proxied?.ToJsonString());

                RecordedRequest request = upstream.RequestsTo(p => p.StartsWith("/openai/", StringComparison.Ordinal)).Single();
                if (!request.Query.Contains("api-version=2025-01-01-preview", StringComparison.Ordinal)) throw new Exception("Expected the configured api-version to be appended, got " + request.Query);
                if (request.Header("api-key") != _AzureKey) throw new Exception("Expected the api-key header to be injected");

                RawResponse completionStatusResponse = await SendRawAsync(HttpMethod.Post, "/v1.0/completion",
                    new JsonObject { ["EndpointId"] = id, ["Prompt"] = "hello", ["MaxTokens"] = 32, ["TimeoutMs"] = 10000 }).ConfigureAwait(false);
                int completionStatus = completionStatusResponse.Status;
                JsonNode? completion = completionStatusResponse.Body;
                if (completionStatus != 200) throw new Exception("Azure completion returned " + completionStatus + ": " + completion?.ToJsonString());
                Expect(completion, "Output", "Stub Azure response.");
            }
            finally
            {
                await DeleteRawAsync(created).ConfigureAwait(false);
            }
        }

        public static async Task TestBedrockCompletionSignedAndNotProxiedAsync()
        {
            using ScriptedProviderServer upstream = new ScriptedProviderServer()
                .On("POST", p => p.EndsWith("/converse", StringComparison.Ordinal),
                    "{\"output\":{\"message\":{\"role\":\"assistant\",\"content\":[{\"text\":\"Stub Bedrock response.\"}]}},\"stopReason\":\"end_turn\",\"usage\":{\"inputTokens\":3,\"outputTokens\":3,\"totalTokens\":6}}");
            List<CreatedEndpoint> created = new List<CreatedEndpoint>();
            try
            {
                JsonObject endpoint = await CreateRawAsync("completion", WithSettings(Endpoint("Bedrock", "anthropic.claude-3-5-sonnet-20240620-v1:0", upstream.BaseUrl), "Bedrock",
                    new JsonObject { ["Region"] = "us-west-2", ["AccessKeyId"] = "AKIAPARTIOE2E", ["SecretAccessKey"] = _AwsSecret }), created).ConfigureAwait(false);
                string id = endpoint["Id"]!.GetValue<string>();

                RawResponse statusResponse = await SendRawAsync(HttpMethod.Post, "/v1.0/completion",
                    new JsonObject { ["EndpointId"] = id, ["Prompt"] = "hello", ["MaxTokens"] = 32, ["TimeoutMs"] = 10000 }).ConfigureAwait(false);
                int status = statusResponse.Status;
                JsonNode? completion = statusResponse.Body;
                if (status != 200) throw new Exception("Bedrock completion returned " + status + ": " + completion?.ToJsonString());
                Expect(completion, "Output", "Stub Bedrock response.");

                RecordedRequest request = upstream.Requests.Single();
                string authorization = request.Header("Authorization") ?? string.Empty;
                if (!authorization.StartsWith("AWS4-HMAC-SHA256 Credential=AKIAPARTIOE2E/", StringComparison.Ordinal))
                    throw new Exception("Expected a SigV4 signature from the stored credentials, got " + authorization);

                RawResponse proxyStatusResponse = await SendRawAsync(HttpMethod.Post, "/v1.0/proxy/" + id + "/v1/chat/completions", new JsonObject()).ConfigureAwait(false);
                int proxyStatus = proxyStatusResponse.Status;
                JsonNode? proxied = proxyStatusResponse.Body;
                if (proxyStatus != 404) throw new Exception("Bedrock endpoints must not be proxied, got " + proxyStatus);
                if (!(proxied?.ToJsonString() ?? string.Empty).Contains("cannot be proxied", StringComparison.Ordinal))
                    throw new Exception("Expected an explanation in the proxy refusal, got " + proxied?.ToJsonString());
                if (upstream.Requests.Count != 1) throw new Exception("The refused proxy request must not reach the upstream");
            }
            finally
            {
                await DeleteRawAsync(created).ConfigureAwait(false);
            }
        }

        public static async Task TestEmbeddingOnlyProvidersEmbedAsync()
        {
            using ScriptedProviderServer upstream = new ScriptedProviderServer()
                .On("POST", "/embed", "[[0.1,0.2,0.3],[0.4,0.5,0.6]]")
                .On("POST", "/v2/embed", "{\"id\":\"e1\",\"embeddings\":{\"float\":[[0.1,0.2,0.3],[0.4,0.5,0.6]]}}");
            List<CreatedEndpoint> created = new List<CreatedEndpoint>();
            try
            {
                JsonObject tei = await CreateRawAsync("embedding", Endpoint("TEI", "bge-small-en-v1.5", upstream.BaseUrl), created).ConfigureAwait(false);
                JsonObject cohere = await CreateRawAsync("embedding", WithSettings(Endpoint("Cohere", "embed-v4.0", upstream.BaseUrl, "co-embed-e2e-key-0001"), "Cohere",
                    new JsonObject { ["InputType"] = "classification" }), created).ConfigureAwait(false);

                foreach (JsonObject endpoint in new[] { tei, cohere })
                {
                    RawResponse statusResponse = await SendRawAsync(HttpMethod.Post, "/v1.0/embed",
                        new JsonObject { ["EndpointId"] = endpoint["Id"]!.GetValue<string>(), ["Input"] = new JsonArray("first", "second") }).ConfigureAwait(false);
                    int status = statusResponse.Status;
                    JsonNode? embedded = statusResponse.Body;
                    if (status != 200) throw new Exception(endpoint["ApiFormat"] + " embed returned " + status + ": " + embedded?.ToJsonString());
                    if (!embedded!.ToJsonString().Contains("0.4", StringComparison.Ordinal)) throw new Exception(endpoint["ApiFormat"] + " did not return both vectors: " + embedded.ToJsonString());
                }

                RecordedRequest cohereRequest = upstream.RequestsTo(p => p == "/v2/embed").First();
                if (!cohereRequest.Body.Contains("classification", StringComparison.Ordinal)) throw new Exception("Expected the Cohere input_type to be sent, got " + cohereRequest.Body);
                if (cohereRequest.Header("Authorization") != "Bearer co-embed-e2e-key-0001") throw new Exception("Expected the Cohere bearer token");
            }
            finally
            {
                await DeleteRawAsync(created).ConfigureAwait(false);
            }
        }

        public static async Task TestConnectivityProbeHealthCheckAsync()
        {
            using ScriptedProviderServer upstream = new ScriptedProviderServer()
                .On("POST", "/v1/embeddings", "{\"object\":\"list\",\"data\":[{\"object\":\"embedding\",\"index\":0,\"embedding\":[0.1,0.2,0.3]}],\"model\":\"voyage-3.5\"}");
            List<CreatedEndpoint> created = new List<CreatedEndpoint>();
            try
            {
                JsonObject body = Endpoint("VoyageAI", "voyage-3.5", upstream.BaseUrl, "pa-probe-key-0001");
                body["HealthCheckEnabled"] = true;
                body["HealthCheckIntervalMs"] = 250;
                body["HealthCheckTimeoutMs"] = 2000;
                body["HealthyThreshold"] = 1;
                body["UnhealthyThreshold"] = 1;
                JsonObject voyage = await CreateRawAsync("embedding", body, created).ConfigureAwait(false);
                string id = voyage["Id"]!.GetValue<string>();

                DateTime deadline = DateTime.UtcNow.AddSeconds(15);
                bool healthy = false;
                while (DateTime.UtcNow < deadline && !healthy)
                {
                    RawResponse statusResponse = await SendRawAsync(HttpMethod.Get, "/v1.0/endpoints/embedding/" + id + "/health", null).ConfigureAwait(false);
                    int status = statusResponse.Status;
                    JsonNode? health = statusResponse.Body;
                    healthy = status == 200 && health?["IsHealthy"]?.GetValue<bool>() == true;
                    if (!healthy) await Task.Delay(200).ConfigureAwait(false);
                }

                if (!healthy) throw new Exception("The VoyageAI endpoint never became healthy through the connectivity probe");
                if (upstream.RequestsTo(p => p == "/v1/embeddings").Count == 0) throw new Exception("The probe should embed a one-word input");
                if (upstream.Requests.Any(r => r.Method == "GET")) throw new Exception("Probe providers must not receive plain HTTP GET health checks");
                if (upstream.Requests.First().Header("Authorization") != "Bearer pa-probe-key-0001") throw new Exception("The probe should authenticate with the endpoint key");
            }
            finally
            {
                await DeleteRawAsync(created).ConfigureAwait(false);
            }
        }

        public static async Task TestSelectableAuthHeaderEndToEndAsync()
        {
            using ScriptedProviderServer upstream = new ScriptedProviderServer()
                .On("GET", "/api/tags", "{\"models\":[]}")
                .On("POST", "/api/chat", "{\"model\":\"gemma3:4b\",\"message\":{\"role\":\"assistant\",\"content\":\"Stub gateway response.\"},\"done\":true}");
            List<CreatedEndpoint> created = new List<CreatedEndpoint>();
            try
            {
                JsonObject body = WithAuth(Endpoint("Ollama", "gemma3:4b", upstream.BaseUrl, "gateway-secret-0001"),
                    new JsonObject { ["Mode"] = "Header", ["HeaderName"] = "X-Gateway-Key" });
                body["HealthCheckEnabled"] = true;
                body["HealthCheckIntervalMs"] = 250;
                body["HealthCheckTimeoutMs"] = 2000;
                body["HealthyThreshold"] = 1;
                body["UnhealthyThreshold"] = 1;
                JsonObject endpoint = await CreateRawAsync("completion", body, created).ConfigureAwait(false);
                string id = endpoint["Id"]!.GetValue<string>();

                Expect(endpoint, "ApiKey", "****0001");
                Expect(endpoint["ProviderSettings"]?["Auth"], "Mode", "Header");
                Expect(endpoint["ProviderSettings"]?["Auth"], "HeaderName", "X-Gateway-Key");
                if (endpoint["HealthCheckUseAuth"]?.GetValue<bool>() != true) throw new Exception("Header-authenticated Ollama endpoints should authenticate health checks");
                Expect(await RevealRawAsync("completion", id).ConfigureAwait(false), "ApiKey", "gateway-secret-0001");

                DateTime deadline = DateTime.UtcNow.AddSeconds(15);
                bool healthy = false;
                while (DateTime.UtcNow < deadline && !healthy)
                {
                    RawResponse health = await SendRawAsync(HttpMethod.Get, "/v1.0/endpoints/completion/" + id + "/health", null).ConfigureAwait(false);
                    healthy = health.Status == 200 && health.Body?["IsHealthy"]?.GetValue<bool>() == true;
                    if (!healthy) await Task.Delay(200).ConfigureAwait(false);
                }
                if (!healthy) throw new Exception("The header-authenticated Ollama endpoint never became healthy");

                RawResponse completion = await SendRawAsync(HttpMethod.Post, "/v1.0/completion",
                    new JsonObject { ["EndpointId"] = id, ["Prompt"] = "hello", ["MaxTokens"] = 32, ["TimeoutMs"] = 10000 }).ConfigureAwait(false);
                if (completion.Status != 200) throw new Exception("Completion returned " + completion.Status + ": " + completion.Body?.ToJsonString());
                Expect(completion.Body, "Output", "Stub gateway response.");

                RawResponse proxied = await SendRawAsync(HttpMethod.Post, "/v1.0/proxy/" + id + "/api/chat",
                    new JsonObject { ["model"] = "gemma3:4b", ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = "hi" }), ["stream"] = false }).ConfigureAwait(false);
                if (proxied.Status != 200) throw new Exception("Proxy returned " + proxied.Status + ": " + proxied.Body?.ToJsonString());

                List<RecordedRequest> requests = upstream.Requests.ToList();
                if (!requests.Any(r => r.Method == "GET" && r.Path == "/api/tags")) throw new Exception("Expected an /api/tags health check");
                foreach (RecordedRequest r in requests)
                {
                    if (r.Header("X-Gateway-Key") != "gateway-secret-0001") throw new Exception(r.Method + " " + r.Path + " lacked the gateway header");
                    if (r.Header("Authorization") != null) throw new Exception(r.Method + " " + r.Path + " should not carry an Authorization header, got " + r.Header("Authorization"));
                }
                if (requests.Count(r => r.Path == "/api/chat") != 2) throw new Exception("Expected the completion and the proxied chat to reach /api/chat");
            }
            finally
            {
                await DeleteRawAsync(created).ConfigureAwait(false);
            }
        }

        public static async Task TestSelectableAuthQueryAndBasicAsync()
        {
            string chat = "{\"id\":\"c1\",\"object\":\"chat.completion\",\"model\":\"m\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"Stub vLLM response.\"},\"finish_reason\":\"stop\"}]}";
            using ScriptedProviderServer upstream = new ScriptedProviderServer().On("POST", "/v1/chat/completions", chat);
            List<CreatedEndpoint> created = new List<CreatedEndpoint>();
            try
            {
                JsonObject query = await CreateRawAsync("completion", WithAuth(Endpoint("vLLM", "Qwen/Qwen2.5-7B-Instruct", upstream.BaseUrl, "query-secret-0001"),
                    new JsonObject { ["Mode"] = "QueryParameter", ["QueryParameterName"] = "api_key" }), created).ConfigureAwait(false);
                string queryId = query["Id"]!.GetValue<string>();

                RawResponse completion = await SendRawAsync(HttpMethod.Post, "/v1.0/completion",
                    new JsonObject { ["EndpointId"] = queryId, ["Prompt"] = "hello", ["MaxTokens"] = 32, ["TimeoutMs"] = 10000 }).ConfigureAwait(false);
                if (completion.Status != 200) throw new Exception("vLLM completion returned " + completion.Status + ": " + completion.Body?.ToJsonString());
                Expect(completion.Body, "Output", "Stub vLLM response.");
                if (completion.Body!.ToJsonString().Contains("query-secret-0001", StringComparison.Ordinal))
                    throw new Exception("The query credential leaked into the completion response call details");

                RawResponse proxied = await SendRawAsync(HttpMethod.Post, "/v1.0/proxy/" + queryId + "/v1/chat/completions?stream=false",
                    new JsonObject { ["model"] = "m", ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = "hi" }) }).ConfigureAwait(false);
                if (proxied.Status != 200) throw new Exception("vLLM proxy returned " + proxied.Status);

                List<RecordedRequest> queryRequests = upstream.Requests.ToList();
                foreach (RecordedRequest r in queryRequests)
                {
                    if (!r.Query.Contains("api_key=query-secret-0001", StringComparison.Ordinal)) throw new Exception("Expected the query credential, got " + r.Query);
                    if (r.Header("Authorization") != null) throw new Exception("Query auth should not send an Authorization header");
                }
                if (!queryRequests.Last().Query.Contains("stream=false", StringComparison.Ordinal)) throw new Exception("The caller's query string should be kept, got " + queryRequests.Last().Query);

                JsonObject basic = await CreateRawAsync("completion", WithAuth(Endpoint("OpenAI", "gpt-4.1-mini", upstream.BaseUrl, "basic-password-0001"),
                    new JsonObject { ["Mode"] = "Basic", ["Username"] = "partio" }), created).ConfigureAwait(false);
                RawResponse basicCompletion = await SendRawAsync(HttpMethod.Post, "/v1.0/completion",
                    new JsonObject { ["EndpointId"] = basic["Id"]!.GetValue<string>(), ["Prompt"] = "hello", ["MaxTokens"] = 32, ["TimeoutMs"] = 10000 }).ConfigureAwait(false);
                if (basicCompletion.Status != 200) throw new Exception("Basic-auth completion returned " + basicCompletion.Status);
                string expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("partio:basic-password-0001"));
                if (upstream.Requests.Last().Header("Authorization") != expected) throw new Exception("Expected basic auth, got " + upstream.Requests.Last().Header("Authorization"));

                JsonObject none = await CreateRawAsync("embedding", WithAuth(Endpoint("Ollama", "nomic-embed-text", upstream.BaseUrl, "dropped-secret"),
                    new JsonObject { ["Mode"] = "None" }), created).ConfigureAwait(false);
                if (none["ApiKey"] != null) throw new Exception("An endpoint with auth Mode None should not store an ApiKey, got " + none["ApiKey"]);
            }
            finally
            {
                await DeleteRawAsync(created).ConfigureAwait(false);
            }
        }

        private static JsonObject WithAuth(JsonObject body, JsonObject auth)
        {
            body["ProviderSettings"] = new JsonObject { ["Auth"] = auth };
            return body;
        }

        private static JsonObject Endpoint(string apiFormat, string model, string? endpoint, string? apiKey = null)
        {
            JsonObject body = new JsonObject
            {
                ["TenantId"] = _TestTenantId,
                ["Name"] = apiFormat + " " + model,
                ["Model"] = model,
                ["ApiFormat"] = apiFormat,
                ["HealthCheckEnabled"] = false
            };
            if (endpoint != null) body["Endpoint"] = endpoint;
            if (apiKey != null) body["ApiKey"] = apiKey;
            return body;
        }

        private static JsonObject WithSettings(JsonObject body, string section, JsonObject settings)
        {
            body["ProviderSettings"] = new JsonObject { [section] = settings };
            return body;
        }

        private static void Expect(JsonNode? node, string property, string expected)
        {
            string? actual = node?[property]?.ToString();
            if (actual != expected) throw new Exception("Expected " + property + " = '" + expected + "' but got '" + actual + "' in " + node?.ToJsonString());
        }

        private static async Task<JsonObject> CreateRawAsync(string kind, JsonObject body, List<CreatedEndpoint> created)
        {
            RawResponse statusResponse = await SendRawAsync(HttpMethod.Put, "/v1.0/endpoints/" + kind, body).ConfigureAwait(false);
            int status = statusResponse.Status;
            JsonNode? response = statusResponse.Body;
            if (status != 201) throw new Exception("Creating " + body["ApiFormat"] + " " + kind + " endpoint returned " + status + ": " + response?.ToJsonString());
            JsonObject obj = response!.AsObject();
            created.Add(new CreatedEndpoint(kind, obj["Id"]!.GetValue<string>()));
            return obj;
        }

        private static async Task<JsonNode> RevealRawAsync(string kind, string id)
        {
            RawResponse statusResponse = await SendRawAsync(HttpMethod.Get, "/v1.0/endpoints/" + kind + "/" + id + "/reveal", null).ConfigureAwait(false);
            int status = statusResponse.Status;
            JsonNode? response = statusResponse.Body;
            if (status != 200 || response == null) throw new Exception("Reveal returned " + status + ": " + response?.ToJsonString());
            return response;
        }

        private static async Task DeleteRawAsync(List<CreatedEndpoint> created)
        {
            foreach (CreatedEndpoint endpoint in created)
            {
                try { await SendRawAsync(HttpMethod.Delete, "/v1.0/endpoints/" + endpoint.Kind + "/" + endpoint.Id, null).ConfigureAwait(false); }
                catch { }
            }
        }

        private static async Task<RawResponse> SendRawAsync(HttpMethod method, string path, JsonNode? body)
        {
            using HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            using HttpRequestMessage request = new HttpRequestMessage(method, _Endpoint.TrimEnd('/') + path);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _AdminKey);
            if (body != null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await http.SendAsync(request).ConfigureAwait(false);
            string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            JsonNode? parsed = null;
            if (!string.IsNullOrWhiteSpace(text))
            {
                try { parsed = JsonNode.Parse(text); }
                catch { parsed = JsonValue.Create(text); }
            }
            return new RawResponse((int)response.StatusCode, parsed);
        }

        private sealed class RawResponse
        {
            public int Status { get; }
            public JsonNode? Body { get; }

            public RawResponse(int status, JsonNode? body)
            {
                Status = status;
                Body = body;
            }
        }

        private sealed class CreatedEndpoint
        {
            public string Kind { get; }
            public string Id { get; }

            public CreatedEndpoint(string kind, string id)
            {
                Kind = kind;
                Id = id;
            }
        }

        private sealed class InvalidEndpointCase
        {
            public string Kind { get; }
            public string Description { get; }
            public JsonObject Body { get; }

            public InvalidEndpointCase(string kind, string description, JsonObject body)
            {
                Kind = kind;
                Description = description;
                Body = body;
            }
        }
    }
}
