namespace Test.Automated
{
    using Partio.Sdk;
    using Partio.Sdk.Models;
    using Test.Shared;

    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--sdk-harnesses", StringComparison.OrdinalIgnoreCase))
            {
                await using SelfHostedPartioTestEnvironment sdkEnvironment = await SelfHostedPartioTestEnvironment.StartAsync().ConfigureAwait(false);
                SdkHarnessRunSummary sdkSummary = await SdkHarnessRunner.RunAsync(sdkEnvironment).ConfigureAwait(false);
                return sdkSummary.FailedCount > 0 ? 1 : 0;
            }

            if (args.Length > 0)
            {
                string endpoint = args[0];
                string adminKey = args.Length >= 2 ? args[1] : "partioadmin";
                string testToken = args.Length >= 3 ? args[2] : "default";
                string upstreamEndpoint = args.Length >= 4 ? args[3] : "http://127.0.0.1:11434";

                AutomatedConsoleRunner externalRunner = new AutomatedConsoleRunner(endpoint, adminKey, testToken, upstreamEndpoint);
                AutomatedRunSummary externalSummary = await externalRunner.RunAsync().ConfigureAwait(false);
                return externalSummary.FailedCount > 0 ? 1 : 0;
            }

            await using SelfHostedPartioTestEnvironment environment = await SelfHostedPartioTestEnvironment.StartAsync().ConfigureAwait(false);
            IReadOnlyList<SharedNamedTestCase> selfHostedTests = new List<SharedNamedTestCase>
            {
                SharedNamedTestCase.CreateAsync(
                    "Health Checks Share Same URL Probe",
                    async () => await TestSharedHealthCheckUrlProbeAsync(environment).ConfigureAwait(false))
            };
            AutomatedConsoleRunner runner = new AutomatedConsoleRunner(
                environment.Endpoint,
                environment.AdminKey,
                environment.TestToken,
                environment.UpstreamEndpoint,
                selfHostedTests);
            AutomatedRunSummary summary = await runner.RunAsync().ConfigureAwait(false);
            return summary.FailedCount > 0 ? 1 : 0;
        }

        private static async Task TestSharedHealthCheckUrlProbeAsync(SelfHostedPartioTestEnvironment environment)
        {
            string uniquePath = "/api/tags?health-singleton=" + Guid.NewGuid().ToString("N");
            string healthUrl = environment.UpstreamEndpoint.TrimEnd('/') + uniquePath;
            int baseline = environment.GetUpstreamRawPathRequestCount(uniquePath);
            string? embeddingEndpointId = null;
            string? completionEndpointId = null;

            using PartioClient admin = new PartioClient(environment.Endpoint, environment.AdminKey);

            try
            {
                EmbeddingEndpoint? embeddingEndpoint = await admin.CreateEndpointAsync(new EmbeddingEndpoint
                {
                    TenantId = "default",
                    Name = "Singleton Health Embedding",
                    Model = "singleton-embed",
                    Endpoint = environment.UpstreamEndpoint,
                    ApiFormat = "Ollama",
                    Active = true,
                    HealthCheckEnabled = true,
                    HealthCheckUrl = healthUrl,
                    HealthCheckMethod = "GET",
                    HealthCheckIntervalMs = 200,
                    HealthCheckTimeoutMs = 2000,
                    HealthCheckExpectedStatusCode = 200,
                    HealthyThreshold = 1,
                    UnhealthyThreshold = 1
                }).ConfigureAwait(false);
                embeddingEndpointId = embeddingEndpoint?.Id;

                CompletionEndpoint? completionEndpoint = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
                {
                    TenantId = "default",
                    Name = "Singleton Health Completion",
                    Model = "singleton-chat",
                    Endpoint = environment.UpstreamEndpoint,
                    ApiFormat = "Ollama",
                    Active = true,
                    HealthCheckEnabled = true,
                    HealthCheckUrl = healthUrl,
                    HealthCheckMethod = "GET",
                    HealthCheckIntervalMs = 200,
                    HealthCheckTimeoutMs = 2000,
                    HealthCheckExpectedStatusCode = 200,
                    HealthyThreshold = 1,
                    UnhealthyThreshold = 1
                }).ConfigureAwait(false);
                completionEndpointId = completionEndpoint?.Id;

                if (string.IsNullOrEmpty(embeddingEndpointId) || string.IsNullOrEmpty(completionEndpointId))
                    throw new InvalidOperationException("Expected both health-check test endpoints to be created.");

                await environment.WaitForUpstreamRawPathRequestCountAsync(uniquePath, baseline + 2, 5000).ConfigureAwait(false);
                await Task.Delay(350).ConfigureAwait(false);

                int delta = environment.GetUpstreamRawPathRequestCount(uniquePath) - baseline;
                if (delta > 4)
                    throw new InvalidOperationException("Expected shared URL health checks to use one probe loop; observed " + delta + " probes for " + healthUrl + ".");
            }
            finally
            {
                if (!string.IsNullOrEmpty(completionEndpointId))
                {
                    try { await admin.DeleteCompletionEndpointAsync(completionEndpointId).ConfigureAwait(false); } catch { }
                }

                if (!string.IsNullOrEmpty(embeddingEndpointId))
                {
                    try { await admin.DeleteEndpointAsync(embeddingEndpointId).ConfigureAwait(false); } catch { }
                }
            }
        }
    }
}
