namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Partio.Sdk;
    using Partio.Sdk.Models;

    /// <summary>
    /// Ollama Cloud and xAI endpoint cases: API-key enforcement, server-side endpoint normalization, and
    /// xAI traffic (warm requests and proxy passthrough) against the in-process OpenAI/xAI fake server.
    /// Ollama Cloud's host is fixed, so its upstream traffic is covered by the provider client suite.
    /// </summary>
    public static partial class SharedIntegrationTests
    {
        public static async Task TestCloudProviderEndpointsRequireApiKeyAsync()
        {
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            foreach (string format in new[] { "OllamaCloud", "xAI" })
            {
                try
                {
                    await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                    {
                        TenantId = _TestTenantId,
                        Name = format + " Missing Key",
                        Model = "model",
                        Endpoint = format == "xAI" ? "https://api.x.ai" : "https://ollama.com",
                        ApiFormat = format,
                        HealthCheckEnabled = false
                    }).ConfigureAwait(false);
                    throw new Exception("Expected 400 creating a " + format + " completion endpoint without an ApiKey");
                }
                catch (PartioException ex) when (ex.StatusCode == 400)
                {
                }

                try
                {
                    await admin.CreateEndpointAsync(new EmbeddingEndpoint
                    {
                        TenantId = _TestTenantId,
                        Name = format + " Missing Key",
                        Model = "model",
                        Endpoint = format == "xAI" ? "https://api.x.ai" : "https://ollama.com",
                        ApiFormat = format,
                        HealthCheckEnabled = false
                    }).ConfigureAwait(false);
                    throw new Exception("Expected 400 creating a " + format + " embedding endpoint without an ApiKey");
                }
                catch (PartioException ex) when (ex.StatusCode == 400)
                {
                }
            }
        }

        public static async Task TestCreateOllamaCloudEndpointsAsync()
        {
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            CompletionEndpoint? completion = null;
            EmbeddingEndpoint? embedding = null;
            try
            {
                completion = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Ollama Cloud Completion",
                    Model = "gpt-oss:20b",
                    Endpoint = "http://ignored-host:11434",
                    ApiFormat = "OllamaCloud",
                    ApiKey = "oc-test-key",
                    HealthCheckEnabled = false
                }).ConfigureAwait(false);
                if (completion == null || string.IsNullOrEmpty(completion.Id)) throw new Exception("No Ollama Cloud completion endpoint returned");
                if (completion.ApiFormat != "OllamaCloud") throw new Exception("Expected ApiFormat OllamaCloud, got " + completion.ApiFormat);
                if (completion.Endpoint != "https://ollama.com") throw new Exception("Expected the fixed Ollama Cloud host, got " + completion.Endpoint);
                if (completion.HealthCheckUrl != "https://ollama.com/api/tags") throw new Exception("Unexpected health check URL: " + completion.HealthCheckUrl);
                if (!completion.HealthCheckUseAuth) throw new Exception("Expected Ollama Cloud health checks to use auth");

                embedding = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Ollama Cloud Embedding",
                    Model = "embeddinggemma",
                    Endpoint = "https://ollama.com",
                    ApiFormat = "OllamaCloud",
                    ApiKey = "oc-test-key",
                    HealthCheckEnabled = false
                }).ConfigureAwait(false);
                if (embedding == null || string.IsNullOrEmpty(embedding.Id)) throw new Exception("No Ollama Cloud embedding endpoint returned");
                if (embedding.Endpoint != "https://ollama.com") throw new Exception("Expected the fixed Ollama Cloud host, got " + embedding.Endpoint);

                EmbeddingEndpoint? read = await admin.GetEndpointAsync(embedding.Id).ConfigureAwait(false);
                if (read == null || read.ApiFormat != "OllamaCloud") throw new Exception("Ollama Cloud ApiFormat did not round-trip through the database");

                // Updates re-pin the host and keep enforcing the API key.
                read.Endpoint = "http://another-host:11434";
                EmbeddingEndpoint? updated = await admin.UpdateEndpointAsync(embedding.Id, read).ConfigureAwait(false);
                if (updated == null || updated.Endpoint != "https://ollama.com") throw new Exception("Expected update to keep the fixed Ollama Cloud host, got " + updated?.Endpoint);

                updated.ApiKey = null;
                try
                {
                    await admin.UpdateEndpointAsync(embedding.Id, updated).ConfigureAwait(false);
                    throw new Exception("Expected 400 removing the ApiKey from an Ollama Cloud endpoint");
                }
                catch (PartioException ex) when (ex.StatusCode == 400)
                {
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(completion?.Id))
                    await admin.DeleteCompletionEndpointAsync(completion.Id).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(embedding?.Id))
                    await admin.DeleteEndpointAsync(embedding.Id).ConfigureAwait(false);
            }
        }

        public static async Task TestWarmXAiCompletionEndpointAsync()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer();
            provider.CompletionModel = "grok-4";
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            CompletionEndpoint? endpoint = null;
            try
            {
                endpoint = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "xAI Warm Test",
                    Model = "grok-4",
                    Endpoint = provider.BaseUrl + "/v1",
                    ApiFormat = "xAI",
                    ApiKey = "xai-test-key",
                    HealthCheckEnabled = false
                }).ConfigureAwait(false);
                if (endpoint == null || string.IsNullOrEmpty(endpoint.Id)) throw new Exception("No xAI endpoint returned");
                if (endpoint.Endpoint != provider.BaseUrl) throw new Exception("Expected the trailing /v1 to be dropped, got " + endpoint.Endpoint);
                if (endpoint.HealthCheckUrl != provider.BaseUrl + "/v1/models") throw new Exception("Unexpected xAI health check URL: " + endpoint.HealthCheckUrl);
                if (!endpoint.HealthCheckUseAuth) throw new Exception("Expected xAI health checks to use auth");

                ModelLoadResponse? result = await admin.LoadCompletionEndpointAsync(endpoint.Id, new ModelLoadRequest
                {
                    SampleInput = "xAI warm probe",
                    MaxTokens = 1
                }).ConfigureAwait(false);

                if (result == null) throw new Exception("No load response");
                if (!result.Success) throw new Exception("Warm failed: " + result.Message);
                if (result.Outcome != "Warmed") throw new Exception("Expected Warmed, got " + result.Outcome);
                if (result.Strategy != "WarmRequest") throw new Exception("Expected WarmRequest, got " + result.Strategy);
                if (provider.ResponsesRequestCount < 1) throw new Exception("Expected the xAI Responses API to receive the warm request");
                if (provider.LastAuthorization != "Bearer xai-test-key") throw new Exception("Expected the xAI bearer token, got " + provider.LastAuthorization);
            }
            finally
            {
                if (!string.IsNullOrEmpty(endpoint?.Id))
                    await admin.DeleteCompletionEndpointAsync(endpoint.Id).ConfigureAwait(false);
            }
        }

        public static async Task TestWarmXAiEmbeddingEndpointAsync()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer();
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            EmbeddingEndpoint? endpoint = null;
            try
            {
                endpoint = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "xAI Embedding Warm Test",
                    Model = "grok-embedding",
                    Endpoint = provider.BaseUrl,
                    ApiFormat = "xAI",
                    ApiKey = "xai-test-key",
                    HealthCheckEnabled = false
                }).ConfigureAwait(false);
                if (endpoint == null || string.IsNullOrEmpty(endpoint.Id)) throw new Exception("No xAI embedding endpoint returned");

                ModelLoadResponse? result = await admin.LoadEndpointAsync(endpoint.Id, new ModelLoadRequest
                {
                    SampleInput = "xAI embedding warm probe"
                }).ConfigureAwait(false);

                if (result == null) throw new Exception("No load response");
                if (!result.Success) throw new Exception("Warm failed: " + result.Message);
                if (result.Outcome != "Warmed") throw new Exception("Expected Warmed, got " + result.Outcome);
                await provider.WaitForEmbeddingRequestCountAsync(1).ConfigureAwait(false);
                if (provider.LastAuthorization != "Bearer xai-test-key") throw new Exception("Expected the xAI bearer token, got " + provider.LastAuthorization);
            }
            finally
            {
                if (!string.IsNullOrEmpty(endpoint?.Id))
                    await admin.DeleteEndpointAsync(endpoint.Id).ConfigureAwait(false);
            }
        }

        public static async Task TestProxyXAiResponsesAsync()
        {
            using SlowOpenAiCompatibleServer provider = new SlowOpenAiCompatibleServer();
            using PartioClient admin = new PartioClient(_Endpoint, _AdminKey);

            CompletionEndpoint? endpoint = null;
            try
            {
                endpoint = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                {
                    TenantId = _TestTenantId,
                    Name = "Proxy xAI",
                    Model = "grok-4",
                    Endpoint = provider.BaseUrl,
                    ApiFormat = "xAI",
                    ApiKey = "xai-proxy-key",
                    HealthCheckEnabled = false
                }).ConfigureAwait(false);
                if (endpoint == null || string.IsNullOrEmpty(endpoint.Id)) throw new Exception("No proxy xAI endpoint returned");

                string body = "{\"model\":\"grok-4\",\"input\":\"hi\"}";
                ProxyResponse result = await admin.ProxyPostAsync(endpoint.Id, "v1/responses", body).ConfigureAwait(false);
                if (result.StatusCode != 200) throw new Exception("Expected 200, got " + result.StatusCode + ": " + result.Body);
                if (result.Body.IndexOf("Stub xAI response", StringComparison.Ordinal) < 0)
                    throw new Exception("Expected the xAI Responses body to be relayed verbatim, got: " + result.Body);
                if (provider.LastAuthorization != "Bearer xai-proxy-key") throw new Exception("Expected Partio to inject the xAI bearer token, got " + provider.LastAuthorization);

                ProxyResponse denied = await admin.ProxyPostAsync(endpoint.Id, "api/chat", "{}").ConfigureAwait(false);
                if (denied.StatusCode != 404) throw new Exception("Expected 404 for an Ollama path on an xAI endpoint, got " + denied.StatusCode);
            }
            finally
            {
                if (!string.IsNullOrEmpty(endpoint?.Id))
                    await admin.DeleteCompletionEndpointAsync(endpoint.Id).ConfigureAwait(false);
            }
        }
    }
}
