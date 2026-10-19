namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using Partio.Core.Proxy;
    using Partio.Core.Settings;
    using Partio.Core.ThirdParty;
    using Touchstone.Core;

    /// <summary>
    /// Offline unit tests for the per-provider rules: capability matrix, endpoint normalization and defaults,
    /// provider-settings validation, health check defaults, upstream auth headers, proxy allow-lists, and
    /// secret masking.
    /// </summary>
    public static class ProviderRulesTests
    {
        private const string ServiceAccountJson =
            "{\"type\":\"service_account\",\"client_email\":\"partio@test-project.iam.gserviceaccount.com\",\"private_key\":\"-----BEGIN PRIVATE KEY-----\\nAAAA\\n-----END PRIVATE KEY-----\\n\"}";

        /// <summary>
        /// Build the Touchstone suite of provider rule unit tests.
        /// </summary>
        /// <returns>A suite descriptor exposing every case.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                "ProviderRules",
                "Per-provider validation, defaults, auth, proxy, and secret masking unit tests",
                BuildCases());
        }

        private static List<TestCaseDescriptor> BuildCases()
        {
            List<TestCaseDescriptor> tests = new List<TestCaseDescriptor>();

            // ===== Capability matrix =====

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Embedding-only and completion-only providers are flagged", () =>
            {
                Check.False(ProviderDefaults.SupportsCompletion(ApiFormatEnum.TEI), "TEI is embedding-only");
                Check.False(ProviderDefaults.SupportsCompletion(ApiFormatEnum.VoyageAI), "VoyageAI is embedding-only");
                Check.False(ProviderDefaults.SupportsEmbedding(ApiFormatEnum.Anthropic), "Anthropic is completion-only");

                foreach (ApiFormatEnum format in Enum.GetValues<ApiFormatEnum>())
                {
                    if (format != ApiFormatEnum.TEI && format != ApiFormatEnum.VoyageAI)
                        Check.True(ProviderDefaults.SupportsCompletion(format), format + " supports completion");
                    if (format != ApiFormatEnum.Anthropic)
                        Check.True(ProviderDefaults.SupportsEmbedding(format), format + " supports embedding");
                }
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Every provider has a display name and a distinct telemetry label", () =>
            {
                List<string> labels = Enum.GetValues<ApiFormatEnum>().Select(ProviderDefaults.ServiceLabel).ToList();
                Check.DoesNotContain(labels, l => l == "(unknown)");
                Check.Equal(labels.Count, labels.Distinct().Count(), "labels must be distinct");
                Check.Equal("AWS Bedrock", ProviderDefaults.DisplayName(ApiFormatEnum.Bedrock));
                Check.Equal("Google Vertex AI", ProviderDefaults.DisplayName(ApiFormatEnum.VertexAI));
                Check.Equal("Azure OpenAI", ProviderDefaults.DisplayName(ApiFormatEnum.AzureOpenAI));
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Signed providers use connectivity probes and cannot be proxied", () =>
            {
                Check.True(ProviderDefaults.UsesSignedRequests(ApiFormatEnum.Bedrock), "Bedrock signs");
                Check.True(ProviderDefaults.UsesSignedRequests(ApiFormatEnum.VertexAI), "Vertex signs");
                Check.False(ProviderDefaults.SupportsProxy(ApiFormatEnum.Bedrock), "Bedrock not proxied");
                Check.False(ProviderDefaults.SupportsProxy(ApiFormatEnum.VertexAI), "Vertex not proxied");
                Check.True(ProviderDefaults.SupportsProxy(ApiFormatEnum.Anthropic), "Anthropic proxied");
                Check.True(ProviderDefaults.UsesConnectivityProbe(ApiFormatEnum.VoyageAI), "VoyageAI has no model listing");
                Check.False(ProviderDefaults.UsesConnectivityProbe(ApiFormatEnum.TEI), "TEI has /health");
            }));

            // ===== Endpoint normalization =====

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Empty endpoints resolve to provider defaults", () =>
            {
                Check.Equal("https://api.anthropic.com", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.Anthropic, null));
                Check.Equal("https://api.cohere.com", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.Cohere, ""));
                Check.Equal("https://api.voyageai.com", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.VoyageAI, " "));
                Check.Equal("https://api.openai.com", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.OpenAI, null));
                Check.Equal("https://generativelanguage.googleapis.com", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.Gemini, null));
                Check.Equal(string.Empty, ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.AzureOpenAI, null));
                Check.Equal(string.Empty, ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.TEI, null));
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Bedrock and Vertex AI endpoints derive from the region", () =>
            {
                ProviderSettings aws = new ProviderSettings { Bedrock = new BedrockProviderSettings { Region = "eu-west-1" } };
                ProviderSettings gcp = new ProviderSettings { VertexAI = new VertexAiProviderSettings { ProjectId = "p", Region = "us-central1" } };
                Check.Equal("https://bedrock-runtime.eu-west-1.amazonaws.com", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.Bedrock, null, aws));
                Check.Equal("https://us-central1-aiplatform.googleapis.com", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.VertexAI, "", gcp));
                Check.Equal("http://localhost:4566", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.Bedrock, "http://localhost:4566/", aws));
                Check.Equal(string.Empty, ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.Bedrock, null, null));
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Versioned path suffixes are trimmed to the API root", () =>
            {
                Check.Equal("https://api.anthropic.com", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.Anthropic, "https://api.anthropic.com/v1/"));
                Check.Equal("https://api.cohere.com", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.Cohere, "https://api.cohere.com/v2"));
                Check.Equal("https://api.cohere.com", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.Cohere, "https://api.cohere.com/v1"));
                Check.Equal("https://api.voyageai.com", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.VoyageAI, "https://api.voyageai.com/v1"));
                Check.Equal("https://res.openai.azure.com", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.AzureOpenAI, "https://res.openai.azure.com/openai/"));
                Check.Equal("http://tei:8080", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.TEI, "http://tei:8080/"));
                Check.Equal("http://localhost:8000/v1", ProviderDefaults.NormalizeEndpoint(ApiFormatEnum.vLLM, "http://localhost:8000/v1"), "vLLM URLs are unchanged");
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Provider settings keep only the section matching the format", () =>
            {
                ProviderSettings mixed = new ProviderSettings
                {
                    AzureOpenAI = new AzureOpenAiProviderSettings { ApiVersion = " 2025-01-01 " },
                    Bedrock = new BedrockProviderSettings { Region = "us-east-1" },
                    Cohere = new CohereProviderSettings { InputType = "search_query" }
                };

                ProviderSettings? azure = ProviderDefaults.NormalizeProviderSettings(ApiFormatEnum.AzureOpenAI, mixed);
                Check.NotNull(azure);
                Check.Equal("2025-01-01", azure!.AzureOpenAI!.ApiVersion);
                Check.Null(azure.Bedrock);
                Check.Null(azure.Cohere);

                Check.Null(ProviderDefaults.NormalizeProviderSettings(ApiFormatEnum.Ollama, mixed), "formats without settings drop them");
                Check.Null(ProviderDefaults.NormalizeProviderSettings(ApiFormatEnum.VertexAI, mixed), "missing section is null");
                Check.Null(ProviderDefaults.NormalizeProviderSettings(ApiFormatEnum.Cohere, null));
            }));

            // ===== Validation =====

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Validation rejects unsupported endpoint kinds", () =>
            {
                ArgumentException tei = Check.Throws<ArgumentException>(() => ProviderDefaults.Validate(ApiFormatEnum.TEI, EndpointTypeEnum.Completion, "http://tei", null, null));
                Check.Contains("does not support completion", tei.Message);
                Check.Throws<ArgumentException>(() => ProviderDefaults.Validate(ApiFormatEnum.VoyageAI, EndpointTypeEnum.Completion, "https://api.voyageai.com", "k", null));
                ArgumentException anthropic = Check.Throws<ArgumentException>(() => ProviderDefaults.Validate(ApiFormatEnum.Anthropic, EndpointTypeEnum.Embedding, "https://api.anthropic.com", "k", null));
                Check.Contains("does not support embedding", anthropic.Message);
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Validation requires API keys for keyed cloud providers", () =>
            {
                foreach (ApiFormatEnum format in new[] { ApiFormatEnum.AzureOpenAI, ApiFormatEnum.Anthropic, ApiFormatEnum.Cohere, ApiFormatEnum.VoyageAI, ApiFormatEnum.OllamaCloud, ApiFormatEnum.xAI })
                {
                    EndpointTypeEnum kind = ProviderDefaults.SupportsCompletion(format) ? EndpointTypeEnum.Completion : EndpointTypeEnum.Embedding;
                    ArgumentException ex = Check.Throws<ArgumentException>(() => ProviderDefaults.Validate(format, kind, "https://host", null, null));
                    Check.Equal("ApiKey", ex.ParamName, format + " should report ApiKey");
                }

                ProviderDefaults.Validate(ApiFormatEnum.TEI, EndpointTypeEnum.Embedding, "http://tei", null, null);
                ProviderDefaults.Validate(ApiFormatEnum.Ollama, EndpointTypeEnum.Completion, "http://localhost:11434", null, null);
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Providers without a default endpoint require one", () =>
            {
                foreach (ApiFormatEnum format in new[] { ApiFormatEnum.Ollama, ApiFormatEnum.vLLM, ApiFormatEnum.TEI })
                {
                    EndpointTypeEnum kind = ProviderDefaults.SupportsCompletion(format) ? EndpointTypeEnum.Completion : EndpointTypeEnum.Embedding;
                    Check.Equal("Endpoint", Check.Throws<ArgumentException>(() => ProviderDefaults.Validate(format, kind, " ", null, null)).ParamName, format + " needs an endpoint");
                }

                EmbeddingEndpoint ep = new EmbeddingEndpoint { Endpoint = "" };
                Check.Equal(string.Empty, ep.Endpoint, "an empty endpoint is accepted and normalized later");
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Azure OpenAI requires a resource URL", () =>
            {
                ArgumentException ex = Check.Throws<ArgumentException>(() => ProviderDefaults.Validate(ApiFormatEnum.AzureOpenAI, EndpointTypeEnum.Completion, "", "key", null));
                Check.Equal("Endpoint", ex.ParamName);
                ProviderDefaults.Validate(ApiFormatEnum.AzureOpenAI, EndpointTypeEnum.Completion, "https://res.openai.azure.com", "key", null);
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Bedrock validation requires settings, region, and static credentials", () =>
            {
                Check.Equal("ProviderSettings.Bedrock", Check.Throws<ArgumentException>(() =>
                    ProviderDefaults.Validate(ApiFormatEnum.Bedrock, EndpointTypeEnum.Completion, "x", null, null)).ParamName);
                Check.Equal("ProviderSettings.Bedrock.Region", Check.Throws<ArgumentException>(() =>
                    ProviderDefaults.Validate(ApiFormatEnum.Bedrock, EndpointTypeEnum.Completion, "x", null, Bedrock(null, AwsCredentialSourceEnum.Static, "AKIA", "secret"))).ParamName);
                Check.Equal("ProviderSettings.Bedrock.AccessKeyId", Check.Throws<ArgumentException>(() =>
                    ProviderDefaults.Validate(ApiFormatEnum.Bedrock, EndpointTypeEnum.Completion, "x", null, Bedrock("us-east-1", AwsCredentialSourceEnum.Static, null, "secret"))).ParamName);
                Check.Equal("ProviderSettings.Bedrock.SecretAccessKey", Check.Throws<ArgumentException>(() =>
                    ProviderDefaults.Validate(ApiFormatEnum.Bedrock, EndpointTypeEnum.Embedding, "x", null, Bedrock("us-east-1", AwsCredentialSourceEnum.Static, "AKIA", null))).ParamName);

                ProviderDefaults.Validate(ApiFormatEnum.Bedrock, EndpointTypeEnum.Completion, "x", null, Bedrock("us-east-1", AwsCredentialSourceEnum.Static, "AKIA", "secret"));
                ProviderDefaults.Validate(ApiFormatEnum.Bedrock, EndpointTypeEnum.Embedding, "x", null, Bedrock("us-east-1", AwsCredentialSourceEnum.Environment, null, null));
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Vertex AI validation requires project, region, and a usable credential", () =>
            {
                Check.Equal("ProviderSettings.VertexAI", Check.Throws<ArgumentException>(() =>
                    ProviderDefaults.Validate(ApiFormatEnum.VertexAI, EndpointTypeEnum.Completion, "x", null, null)).ParamName);
                Check.Equal("ProviderSettings.VertexAI.ProjectId", Check.Throws<ArgumentException>(() =>
                    ProviderDefaults.Validate(ApiFormatEnum.VertexAI, EndpointTypeEnum.Completion, "x", null, Vertex(null, "us-central1", GcpCredentialSourceEnum.ApplicationDefault, null))).ParamName);
                Check.Equal("ProviderSettings.VertexAI.Region", Check.Throws<ArgumentException>(() =>
                    ProviderDefaults.Validate(ApiFormatEnum.VertexAI, EndpointTypeEnum.Completion, "x", null, Vertex("p", null, GcpCredentialSourceEnum.ApplicationDefault, null))).ParamName);
                Check.Equal("ProviderSettings.VertexAI.ServiceAccountJson", Check.Throws<ArgumentException>(() =>
                    ProviderDefaults.Validate(ApiFormatEnum.VertexAI, EndpointTypeEnum.Completion, "x", null, Vertex("p", "r", GcpCredentialSourceEnum.ServiceAccountJson, null))).ParamName);
                Check.Contains("not valid JSON", Check.Throws<ArgumentException>(() =>
                    ProviderDefaults.Validate(ApiFormatEnum.VertexAI, EndpointTypeEnum.Completion, "x", null, Vertex("p", "r", GcpCredentialSourceEnum.ServiceAccountJson, "{not json"))).Message);
                Check.Contains("client_email and private_key", Check.Throws<ArgumentException>(() =>
                    ProviderDefaults.Validate(ApiFormatEnum.VertexAI, EndpointTypeEnum.Completion, "x", null, Vertex("p", "r", GcpCredentialSourceEnum.ServiceAccountJson, "{\"client_email\":\"a@b\"}"))).Message);
                Check.Equal("ApiKey", Check.Throws<ArgumentException>(() =>
                    ProviderDefaults.Validate(ApiFormatEnum.VertexAI, EndpointTypeEnum.Embedding, "x", null, Vertex("p", "r", GcpCredentialSourceEnum.AccessToken, null))).ParamName);

                ProviderDefaults.Validate(ApiFormatEnum.VertexAI, EndpointTypeEnum.Completion, "x", null, Vertex("p", "r", GcpCredentialSourceEnum.ServiceAccountJson, ServiceAccountJson));
                ProviderDefaults.Validate(ApiFormatEnum.VertexAI, EndpointTypeEnum.Embedding, "x", null, Vertex("p", "r", GcpCredentialSourceEnum.ApplicationDefault, null));
                ProviderDefaults.Validate(ApiFormatEnum.VertexAI, EndpointTypeEnum.Embedding, "x", "ya29.token", Vertex("p", "r", GcpCredentialSourceEnum.AccessToken, null));
            }));

            // ===== Health check defaults =====

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Health check URLs follow each provider's lightweight endpoint", () =>
            {
                Check.Equal("https://res.openai.azure.com/openai/models?api-version=" + ProviderDefaults.AzureOpenAiApiVersion,
                    ProviderDefaults.DefaultHealthCheckUrl(ApiFormatEnum.AzureOpenAI, "https://res.openai.azure.com"));
                Check.Equal("https://res.openai.azure.com/openai/models?api-version=2025-04-01-preview",
                    ProviderDefaults.DefaultHealthCheckUrl(ApiFormatEnum.AzureOpenAI, "https://res.openai.azure.com",
                        new ProviderSettings { AzureOpenAI = new AzureOpenAiProviderSettings { ApiVersion = "2025-04-01-preview" } }));
                Check.Equal("https://api.anthropic.com/v1/models", ProviderDefaults.DefaultHealthCheckUrl(ApiFormatEnum.Anthropic, "https://api.anthropic.com/"));
                Check.Equal("https://api.cohere.com/v1/models", ProviderDefaults.DefaultHealthCheckUrl(ApiFormatEnum.Cohere, "https://api.cohere.com"));
                Check.Equal("http://tei:8080/health", ProviderDefaults.DefaultHealthCheckUrl(ApiFormatEnum.TEI, "http://tei:8080"));
                Check.Null(ProviderDefaults.DefaultHealthCheckUrl(ApiFormatEnum.Bedrock, "https://bedrock-runtime.us-east-1.amazonaws.com"));
                Check.Null(ProviderDefaults.DefaultHealthCheckUrl(ApiFormatEnum.VertexAI, "https://us-central1-aiplatform.googleapis.com"));
                Check.Null(ProviderDefaults.DefaultHealthCheckUrl(ApiFormatEnum.VoyageAI, "https://api.voyageai.com"));
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Applying defaults derives Bedrock endpoints and probe timings", () =>
            {
                CompletionEndpoint ep = new CompletionEndpoint
                {
                    ApiFormat = ApiFormatEnum.Bedrock,
                    Model = "anthropic.claude-3-5-sonnet-20240620-v1:0",
                    HealthCheckIntervalMs = 0,
                    HealthCheckTimeoutMs = 0,
                    ProviderSettings = new ProviderSettings
                    {
                        Bedrock = Bedrock("us-west-2", AwsCredentialSourceEnum.Static, "AKIA", "secret").Bedrock,
                        Cohere = new CohereProviderSettings()
                    }
                };

                CompletionEndpoint.ApplyHealthCheckDefaults(ep);

                Check.Equal("https://bedrock-runtime.us-west-2.amazonaws.com", ep.Endpoint);
                Check.Null(ep.HealthCheckUrl, "probe providers have no health check URL");
                Check.Equal(ProviderDefaults.ProbeHealthCheckIntervalMs, ep.HealthCheckIntervalMs);
                Check.Equal(ProviderDefaults.ProbeHealthCheckTimeoutMs, ep.HealthCheckTimeoutMs);
                Check.Null(ep.ProviderSettings!.Cohere, "unrelated sections are dropped");
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Applying defaults turns on health check auth for keyed providers", () =>
            {
                EmbeddingEndpoint tei = new EmbeddingEndpoint { ApiFormat = ApiFormatEnum.TEI, Model = "bge", Endpoint = "http://tei:8080/", ApiKey = "tei-token" };
                EmbeddingEndpoint.ApplyHealthCheckDefaults(tei);
                Check.Equal("http://tei:8080", tei.Endpoint);
                Check.Equal("http://tei:8080/health", tei.HealthCheckUrl);
                Check.True(tei.HealthCheckUseAuth, "TEI with a key authenticates health checks");

                EmbeddingEndpoint ollama = new EmbeddingEndpoint { ApiFormat = ApiFormatEnum.Ollama, Model = "m", Endpoint = "http://localhost:11434", ApiKey = "k" };
                EmbeddingEndpoint.ApplyHealthCheckDefaults(ollama);
                Check.False(ollama.HealthCheckUseAuth, "local Ollama keeps anonymous health checks");
            }));

            // ===== Auth headers =====

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Anthropic sends x-api-key, anthropic-version, and the workspace", () =>
            {
                Dictionary<string, string> plain = Headers(ApiFormatEnum.Anthropic, "sk-ant", null);
                Check.Equal("sk-ant", plain["x-api-key"]);
                Check.Equal("2023-06-01", plain["anthropic-version"]);
                Check.False(plain.ContainsKey("Authorization"), "no bearer token for Anthropic");

                Dictionary<string, string> custom = Headers(ApiFormatEnum.Anthropic, "sk-ant", new ProviderSettings
                {
                    Anthropic = new AnthropicProviderSettings { AnthropicVersion = "2024-01-01", WorkspaceId = "wrkspc_1" }
                });
                Check.Equal("2024-01-01", custom["anthropic-version"]);
                Check.Equal("wrkspc_1", custom["anthropic-workspace-id"]);
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Azure OpenAI sends api-key or a bearer token by auth mode", () =>
            {
                Check.Equal("az-key", Headers(ApiFormatEnum.AzureOpenAI, "az-key", null)["api-key"]);
                Dictionary<string, string> bearer = Headers(ApiFormatEnum.AzureOpenAI, "eyJ.token", new ProviderSettings
                {
                    AzureOpenAI = new AzureOpenAiProviderSettings { AuthMode = AzureAuthModeEnum.BearerToken }
                });
                Check.Equal("Bearer eyJ.token", bearer["Authorization"]);
                Check.False(bearer.ContainsKey("api-key"), "bearer mode omits api-key");
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Signed providers never send the ApiKey as a header", () =>
            {
                Check.Equal(0, Headers(ApiFormatEnum.Bedrock, "ignored", Bedrock("us-east-1", AwsCredentialSourceEnum.Static, "AKIA", "s")).Count);
                Check.Equal(0, Headers(ApiFormatEnum.VertexAI, "ignored", Vertex("p", "r", GcpCredentialSourceEnum.ServiceAccountJson, ServiceAccountJson)).Count);
                Check.Equal("Bearer ya29", Headers(ApiFormatEnum.VertexAI, "ya29", Vertex("p", "r", GcpCredentialSourceEnum.AccessToken, null))["Authorization"]);
                Check.Equal("Bearer pa-key", Headers(ApiFormatEnum.VoyageAI, "pa-key", null)["Authorization"]);
                Check.Equal("Bearer co-key", Headers(ApiFormatEnum.Cohere, "co-key", null)["Authorization"]);
                Check.Equal(0, Headers(ApiFormatEnum.Cohere, null, null).Count);
            }));

            // ===== Proxy policy =====

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Proxy allows Azure deployment and v1 paths only", () =>
            {
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.AzureOpenAI, "openai/deployments/gpt-4o/chat/completions"), "deployment chat");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.AzureOpenAI, "openai/deployments/emb/embeddings"), "deployment embeddings");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.AzureOpenAI, "openai/v1/chat/completions"), "v1 chat");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.AzureOpenAI, "openai/models"), "models");
                Check.False(ProxyPathPolicy.IsAllowed(ApiFormatEnum.AzureOpenAI, "openai/deployments/gpt-4o/files"), "files rejected");
                Check.False(ProxyPathPolicy.IsAllowed(ApiFormatEnum.AzureOpenAI, "openai/deployments//chat/completions"), "empty deployment rejected");
                Check.False(ProxyPathPolicy.IsAllowed(ApiFormatEnum.AzureOpenAI, "v1/chat/completions"), "plain OpenAI path rejected");
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Proxy allows Anthropic and Cohere native paths and refuses signed providers", () =>
            {
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.Anthropic, "v1/messages"), "Anthropic messages");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.Anthropic, "v1/messages/count_tokens"), "Anthropic count tokens");
                Check.False(ProxyPathPolicy.IsAllowed(ApiFormatEnum.Anthropic, "v1/chat/completions"), "Anthropic rejects OpenAI path");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.Cohere, "v2/chat"), "Cohere chat");
                Check.True(ProxyPathPolicy.IsAllowed(ApiFormatEnum.Cohere, "v2/embed"), "Cohere embed");
                Check.False(ProxyPathPolicy.IsAllowed(ApiFormatEnum.Cohere, "v2/rerank"), "Cohere rerank is not relayed");
                Check.False(ProxyPathPolicy.IsAllowed(ApiFormatEnum.Bedrock, "model/x/converse"), "Bedrock never relayed");
                Check.False(ProxyPathPolicy.IsAllowed(ApiFormatEnum.VertexAI, "v1beta/models"), "Vertex never relayed");
                Check.Contains("v1/messages", ProxyPathPolicy.DescribeAllowed(ApiFormatEnum.Anthropic));
                Check.Contains("openai/deployments/{deployment}/chat/completions", ProxyPathPolicy.DescribeAllowed(ApiFormatEnum.AzureOpenAI));
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Proxy appends the Azure api-version only when missing", () =>
            {
                Check.Equal("?api-version=" + ProviderDefaults.AzureOpenAiApiVersion, ProxyPathPolicy.ApplyRequiredQuery(ApiFormatEnum.AzureOpenAI, null, null));
                Check.Equal("?stream=true&api-version=2025-01-01", ProxyPathPolicy.ApplyRequiredQuery(ApiFormatEnum.AzureOpenAI, "stream=true",
                    new ProviderSettings { AzureOpenAI = new AzureOpenAiProviderSettings { ApiVersion = "2025-01-01" } }));
                Check.Equal("?api-version=2024-02-01", ProxyPathPolicy.ApplyRequiredQuery(ApiFormatEnum.AzureOpenAI, "?api-version=2024-02-01", null));
                Check.Equal("?x=1", ProxyPathPolicy.ApplyRequiredQuery(ApiFormatEnum.OpenAI, "?x=1", null));
                Check.Null(ProxyPathPolicy.ApplyRequiredQuery(ApiFormatEnum.Anthropic, null, null));
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Proxy auth headers include Anthropic companions", () =>
            {
                List<ProxyAuthHeader> headers = ProxyPathPolicy.ResolveAuthHeaders(ApiFormatEnum.Anthropic, "sk-ant", null);
                Check.Contains(headers, h => h.Name == "x-api-key" && h.Value == "sk-ant");
                Check.Contains(headers, h => h.Name == "anthropic-version");
                ProxyAuthHeader? primary = ProxyPathPolicy.ResolveAuthHeader(ApiFormatEnum.Anthropic, "sk-ant");
                Check.Equal("x-api-key", primary!.Value.Name);
                Check.Null(ProxyPathPolicy.ResolveAuthHeader(ApiFormatEnum.Bedrock, "ignored"));
            }));

            // ===== Selectable authentication =====

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Selectable auth applies to Ollama, vLLM, TEI, and OpenAI only", () =>
            {
                foreach (ApiFormatEnum format in Enum.GetValues<ApiFormatEnum>())
                {
                    bool expected = format == ApiFormatEnum.Ollama || format == ApiFormatEnum.vLLM || format == ApiFormatEnum.TEI || format == ApiFormatEnum.OpenAI;
                    Check.Equal(expected, ProviderDefaults.SupportsSelectableAuth(format), format.ToString());
                }

                ProviderSettings withAuth = new ProviderSettings { Auth = new EndpointAuthSettings { Mode = EndpointAuthModeEnum.Header, HeaderName = "X-Key" } };
                Check.Null(ProviderDefaults.EffectiveAuthMode(ApiFormatEnum.Anthropic, "k", withAuth), "fixed providers have no selectable mode");
                Check.Null(ProviderDefaults.NormalizeProviderSettings(ApiFormatEnum.Anthropic, withAuth), "Auth is dropped for fixed providers");
                Check.Equal(EndpointAuthModeEnum.Bearer, ProviderDefaults.EffectiveAuthMode(ApiFormatEnum.Ollama, "k", null), "legacy key means bearer");
                Check.Equal(EndpointAuthModeEnum.None, ProviderDefaults.EffectiveAuthMode(ApiFormatEnum.Ollama, null, null), "no key means none");
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Normalization keeps only the fields the auth mode uses", () =>
            {
                ProviderSettings? normalized = ProviderDefaults.NormalizeProviderSettings(ApiFormatEnum.vLLM, new ProviderSettings
                {
                    Auth = new EndpointAuthSettings { Mode = EndpointAuthModeEnum.Header, HeaderName = " X-Gateway-Key ", QueryParameterName = "q", Username = "u" },
                    Bedrock = new BedrockProviderSettings { Region = "us-east-1" }
                });
                Check.NotNull(normalized);
                Check.Equal("X-Gateway-Key", normalized!.Auth!.HeaderName);
                Check.Null(normalized.Auth.QueryParameterName);
                Check.Null(normalized.Auth.Username);
                Check.Null(normalized.Bedrock);
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Selectable auth validation checks names and the credential", () =>
            {
                ProviderSettings Auth(EndpointAuthModeEnum mode, string? header = null, string? query = null, string? user = null) =>
                    new ProviderSettings { Auth = new EndpointAuthSettings { Mode = mode, HeaderName = header, QueryParameterName = query, Username = user } };

                Check.Equal("ApiKey", Check.Throws<ArgumentException>(() => ProviderDefaults.Validate(ApiFormatEnum.Ollama, EndpointTypeEnum.Completion, "http://o", null, Auth(EndpointAuthModeEnum.Bearer))).ParamName);
                Check.Equal("ProviderSettings.Auth.HeaderName", Check.Throws<ArgumentException>(() => ProviderDefaults.Validate(ApiFormatEnum.Ollama, EndpointTypeEnum.Completion, "http://o", "k", Auth(EndpointAuthModeEnum.Header))).ParamName);
                Check.Contains("not a valid HTTP header name", Check.Throws<ArgumentException>(() => ProviderDefaults.Validate(ApiFormatEnum.vLLM, EndpointTypeEnum.Completion, "http://v", "k", Auth(EndpointAuthModeEnum.Header, "X Bad"))).Message);
                Check.Contains("managed by the HTTP client", Check.Throws<ArgumentException>(() => ProviderDefaults.Validate(ApiFormatEnum.vLLM, EndpointTypeEnum.Completion, "http://v", "k", Auth(EndpointAuthModeEnum.Header, "Host"))).Message);
                Check.Equal("ProviderSettings.Auth.QueryParameterName", Check.Throws<ArgumentException>(() => ProviderDefaults.Validate(ApiFormatEnum.TEI, EndpointTypeEnum.Embedding, "http://t", "k", Auth(EndpointAuthModeEnum.QueryParameter, query: "a&b"))).ParamName);
                Check.Equal("ProviderSettings.Auth.Username", Check.Throws<ArgumentException>(() => ProviderDefaults.Validate(ApiFormatEnum.OpenAI, EndpointTypeEnum.Completion, "https://o", "k", Auth(EndpointAuthModeEnum.Basic))).ParamName);
                Check.Contains("must not contain ':'", Check.Throws<ArgumentException>(() => ProviderDefaults.Validate(ApiFormatEnum.OpenAI, EndpointTypeEnum.Completion, "https://o", "k", Auth(EndpointAuthModeEnum.Basic, user: "a:b"))).Message);

                ProviderDefaults.Validate(ApiFormatEnum.Ollama, EndpointTypeEnum.Completion, "http://o", null, Auth(EndpointAuthModeEnum.None));
                ProviderDefaults.Validate(ApiFormatEnum.Ollama, EndpointTypeEnum.Completion, "http://o", "k", Auth(EndpointAuthModeEnum.Header, "X-API-Key"));
                ProviderDefaults.Validate(ApiFormatEnum.TEI, EndpointTypeEnum.Embedding, "http://t", "k", Auth(EndpointAuthModeEnum.QueryParameter, query: "api_key"));
                ProviderDefaults.Validate(ApiFormatEnum.OpenAI, EndpointTypeEnum.Embedding, "https://o", "k", Auth(EndpointAuthModeEnum.Basic, user: "partio"));
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Selectable auth resolves headers and query credentials", () =>
            {
                ProviderSettings header = new ProviderSettings { Auth = new EndpointAuthSettings { Mode = EndpointAuthModeEnum.Header, HeaderName = "X-Gateway-Key" } };
                ProviderSettings basic = new ProviderSettings { Auth = new EndpointAuthSettings { Mode = EndpointAuthModeEnum.Basic, Username = "partio" } };
                ProviderSettings query = new ProviderSettings { Auth = new EndpointAuthSettings { Mode = EndpointAuthModeEnum.QueryParameter, QueryParameterName = "api_key" } };
                ProviderSettings none = new ProviderSettings { Auth = new EndpointAuthSettings { Mode = EndpointAuthModeEnum.None } };

                Check.Equal("gw", Headers(ApiFormatEnum.Ollama, "gw", header)["X-Gateway-Key"]);
                Check.Equal("Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("partio:pw")), Headers(ApiFormatEnum.vLLM, "pw", basic)["Authorization"]);
                Check.Equal(0, Headers(ApiFormatEnum.TEI, "q", query).Count, "query credentials are not headers");
                Check.Equal(0, Headers(ApiFormatEnum.OpenAI, "k", none).Count);
                Check.Equal("Bearer legacy", Headers(ApiFormatEnum.Ollama, "legacy", null)["Authorization"]);

                Check.Equal("http://t/health?api_key=a%20b", ProviderDefaults.ApplyAuthQuery("http://t/health", ApiFormatEnum.TEI, "a b", query));
                Check.Equal("http://t/x?y=1&api_key=k", ProviderDefaults.ApplyAuthQuery("http://t/x?y=1", ApiFormatEnum.TEI, "k", query));
                Check.Equal("http://t/x", ProviderDefaults.ApplyAuthQuery("http://t/x", ApiFormatEnum.TEI, "k", header));
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Applying defaults clears the key for None and authenticates explicit credentials", () =>
            {
                CompletionEndpoint none = new CompletionEndpoint
                {
                    ApiFormat = ApiFormatEnum.Ollama, Model = "m", Endpoint = "http://o:11434", ApiKey = "stale",
                    ProviderSettings = new ProviderSettings { Auth = new EndpointAuthSettings { Mode = EndpointAuthModeEnum.None } }
                };
                CompletionEndpoint.ApplyHealthCheckDefaults(none);
                Check.Null(none.ApiKey, "None carries no credential");
                Check.False(none.HealthCheckUseAuth);

                CompletionEndpoint header = new CompletionEndpoint
                {
                    ApiFormat = ApiFormatEnum.Ollama, Model = "m", Endpoint = "http://o:11434", ApiKey = "gw",
                    ProviderSettings = new ProviderSettings { Auth = new EndpointAuthSettings { Mode = EndpointAuthModeEnum.Header, HeaderName = "X-Gateway-Key" } }
                };
                CompletionEndpoint.ApplyHealthCheckDefaults(header);
                Check.True(header.HealthCheckUseAuth, "an explicitly authenticated Ollama endpoint authenticates health checks");
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Credential resolution routes each mode to PolyPrompt or the auth transport", () =>
            {
                ClientCredentials legacy = EndpointAuthHandler.ResolveClientCredentials(ApiFormatEnum.Ollama, "k", null);
                Check.Equal("k", legacy.ApiKey);
                Check.Null(legacy.Transport);

                ClientCredentials none = EndpointAuthHandler.ResolveClientCredentials(ApiFormatEnum.Ollama, "k", new ProviderSettings { Auth = new EndpointAuthSettings { Mode = EndpointAuthModeEnum.None } });
                Check.Null(none.ApiKey);
                Check.Null(none.Transport);

                ClientCredentials header = EndpointAuthHandler.ResolveClientCredentials(ApiFormatEnum.vLLM, "k", new ProviderSettings { Auth = new EndpointAuthSettings { Mode = EndpointAuthModeEnum.Header, HeaderName = "X-K" } });
                Check.Null(header.ApiKey);
                Check.True(header.Transport is EndpointAuthHandler, "custom header uses the auth transport");

                ClientCredentials fixedScheme = EndpointAuthHandler.ResolveClientCredentials(ApiFormatEnum.Anthropic, "k", new ProviderSettings { Auth = new EndpointAuthSettings { Mode = EndpointAuthModeEnum.Header, HeaderName = "X-K" } });
                Check.Equal("k", fixedScheme.ApiKey);
                Check.Null(fixedScheme.Transport);
            }));

            // ===== Secret masking =====

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Secrets mask to a four-character tail or fully", () =>
            {
                Check.Equal("****wxyz", EndpointSecrets.Mask("sk-abcdefghwxyz"));
                Check.Equal("****", EndpointSecrets.Mask("short"));
                Check.Null(EndpointSecrets.Mask(null));
                Check.Equal(string.Empty, EndpointSecrets.Mask(string.Empty));
                Check.Equal("****", EndpointSecrets.MaskFully(ServiceAccountJson));
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Echoed masks keep the stored secret and other values replace it", () =>
            {
                string stored = "sk-abcdefghwxyz";
                Check.Equal(stored, EndpointSecrets.Resolve("****wxyz", stored), "echoed mask keeps the stored value");
                Check.Equal("sk-new-value-1234", EndpointSecrets.Resolve("sk-new-value-1234", stored), "a new value replaces it");
                Check.Null(EndpointSecrets.Resolve(null, stored), "null clears");
                Check.Equal("****wxyz", EndpointSecrets.Resolve("****wxyz", null), "nothing stored: keep the supplied value");
                Check.Equal("short", EndpointSecrets.Resolve("****", "short"));
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Redacted endpoints mask provider secrets without changing the original", () =>
            {
                EmbeddingEndpoint ep = new EmbeddingEndpoint
                {
                    ApiFormat = ApiFormatEnum.Bedrock,
                    Model = "amazon.titan-embed-text-v2:0",
                    Endpoint = "https://bedrock-runtime.us-east-1.amazonaws.com",
                    ApiKey = null,
                    ProviderSettings = Bedrock("us-east-1", AwsCredentialSourceEnum.Static, "AKIAEXAMPLE", "wJalrXUtnFEMI/K7MDENG")
                };
                ep.ProviderSettings!.Bedrock!.SessionToken = "IQoJb3JpZ2luX2VjEJr";

                EmbeddingEndpoint redacted = ep.Redacted();

                Check.Equal("AKIAEXAMPLE", redacted.ProviderSettings!.Bedrock!.AccessKeyId, "access key ID is not a secret");
                Check.Equal("****DENG", redacted.ProviderSettings.Bedrock.SecretAccessKey);
                Check.Equal("****jEJr", redacted.ProviderSettings.Bedrock.SessionToken, "session token is masked");
                Check.Equal("wJalrXUtnFEMI/K7MDENG", ep.ProviderSettings.Bedrock.SecretAccessKey, "the original is unchanged");
                Check.Equal(ep.Id, redacted.Id);

                CompletionEndpoint vertex = new CompletionEndpoint
                {
                    ApiFormat = ApiFormatEnum.VertexAI,
                    Model = "gemini-2.5-flash",
                    Endpoint = "https://us-central1-aiplatform.googleapis.com",
                    ApiKey = "ya29.a0AfH6SMBexampletoken",
                    ProviderSettings = Vertex("p", "us-central1", GcpCredentialSourceEnum.ServiceAccountJson, ServiceAccountJson)
                };
                CompletionEndpoint redactedVertex = vertex.Redacted();
                Check.Equal("****oken", redactedVertex.ApiKey);
                Check.Equal("****", redactedVertex.ProviderSettings!.VertexAI!.ServiceAccountJson);
                Check.Equal(ServiceAccountJson, vertex.ProviderSettings!.VertexAI!.ServiceAccountJson);
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Restore puts back provider secrets echoed as masks", () =>
            {
                ProviderSettings stored = Bedrock("us-east-1", AwsCredentialSourceEnum.Static, "AKIA", "wJalrXUtnFEMI/K7MDENG");
                stored.Bedrock!.SessionToken = "IQoJb3JpZ2luX2VjEJr";
                ProviderSettings echoed = EndpointSecrets.Redact(stored)!;
                echoed.Bedrock!.SessionToken = null;

                EndpointSecrets.Restore(echoed, stored);

                Check.Equal("wJalrXUtnFEMI/K7MDENG", echoed.Bedrock.SecretAccessKey);
                Check.Null(echoed.Bedrock.SessionToken, "an explicit null clears the session token");

                ProviderSettings storedVertex = Vertex("p", "r", GcpCredentialSourceEnum.ServiceAccountJson, ServiceAccountJson);
                ProviderSettings echoedVertex = EndpointSecrets.Redact(storedVertex)!;
                EndpointSecrets.Restore(echoedVertex, storedVertex);
                Check.Equal(ServiceAccountJson, echoedVertex.VertexAI!.ServiceAccountJson);
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Provider settings serialize enums as strings and round-trip", () =>
            {
                SerializationHelper.Serializer serializer = new SerializationHelper.Serializer();
                ProviderSettings original = new ProviderSettings
                {
                    AzureOpenAI = new AzureOpenAiProviderSettings { ApiVersion = "2024-10-21", AuthMode = AzureAuthModeEnum.BearerToken },
                    VoyageAI = new VoyageAiProviderSettings { InputType = "document", OutputDimension = 1024 }
                };

                string json = serializer.SerializeJson(original, false);
                Check.Contains("\"BearerToken\"", json);
                ProviderSettings? parsed = serializer.DeserializeJson<ProviderSettings>(json);
                Check.Equal(AzureAuthModeEnum.BearerToken, parsed!.AzureOpenAI!.AuthMode);
                Check.Equal(1024, parsed.VoyageAI!.OutputDimension);
                Check.Throws<ArgumentOutOfRangeException>(() => new VoyageAiProviderSettings { OutputDimension = 0 });
            }));

            tests.Add(TestCaseFactory.Sync("ProviderRules", "Tokenization defaults exist for every new embedding provider", () =>
            {
                TokenizationDefaultsSettings defaults = new TokenizationDefaultsSettings();
                Check.Equal(8192, defaults.AzureOpenAI.MaxInputTokens);
                Check.Equal(512, defaults.Cohere.MaxInputTokens);
                Check.Equal(8192, defaults.Bedrock.MaxInputTokens);
                Check.Equal(2048, defaults.VertexAI.MaxInputTokens);
                Check.Equal(512, defaults.TEI.MaxInputTokens);
                Check.Equal(32000, defaults.VoyageAI.MaxInputTokens);
            }));

            return tests;
        }

        private static ProviderSettings Bedrock(string? region, AwsCredentialSourceEnum source, string? accessKeyId, string? secret)
        {
            return new ProviderSettings
            {
                Bedrock = new BedrockProviderSettings
                {
                    Region = region,
                    CredentialSource = source,
                    AccessKeyId = accessKeyId,
                    SecretAccessKey = secret
                }
            };
        }

        private static ProviderSettings Vertex(string? project, string? region, GcpCredentialSourceEnum source, string? json)
        {
            return new ProviderSettings
            {
                VertexAI = new VertexAiProviderSettings
                {
                    ProjectId = project,
                    Region = region,
                    CredentialSource = source,
                    ServiceAccountJson = json
                }
            };
        }

        private static Dictionary<string, string> Headers(ApiFormatEnum format, string? apiKey, ProviderSettings? settings)
        {
            return ProviderDefaults.ResolveAuthHeaders(format, apiKey, settings).ToDictionary(h => h.Key, h => h.Value, StringComparer.OrdinalIgnoreCase);
        }
    }
}
