namespace Test.Shared
{
    using System.Collections.Generic;
    using System.Linq;
    using Touchstone.Core;

    /// <summary>
    /// Central registry of Touchstone test suites for the Partio platform. This is the single
    /// source of truth consumed by every runner: Test.Automated (Touchstone CLI),
    /// Test.XUnit (Touchstone xUnit adapter), and Test.Nunit (Touchstone NUnit adapter).
    /// </summary>
    public static class PartioTestSuites
    {
        /// <summary>
        /// Suites that run entirely in-process with no external Partio server. These cover the
        /// SDK model contracts, tokenization and chunking, provider clients, request-history
        /// persistence, embedding batch-limit handling, SQLite migration, and core primitives.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> UnitSuites
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    CoreUnitTests.Suite(),
                    SharedSummarizationUnitTests.Suite(),
                    SharedTokenizerUnitTests.Suite(),
                    ProviderClientTests.Suite(),
                    RequestHistoryDiagnostics.Suite(),
                    EmbeddingBatchLimitDiagnostics.Suite(),
                    SqliteMigrationTests.Suite(),
                };
            }
        }

        /// <summary>
        /// Every suite, including the self-hosted integration suite that starts an in-process
        /// Partio server and Ollama-compatible upstream via its before/after hooks.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                List<TestSuiteDescriptor> suites = UnitSuites.ToList();
                suites.Add(SharedIntegrationTests.SelfHostedSuite());
                return suites;
            }
        }

        /// <summary>
        /// Every unit suite plus an integration suite targeting an already-running external
        /// Partio server.
        /// </summary>
        /// <param name="endpoint">Partio server endpoint.</param>
        /// <param name="adminKey">Administrative bearer token.</param>
        /// <param name="testToken">Tenant/test bearer token.</param>
        /// <param name="upstreamEndpoint">Upstream provider endpoint.</param>
        /// <returns>The full suite list bound to the external endpoint.</returns>
        public static IReadOnlyList<TestSuiteDescriptor> AllForExternalEndpoint(
            string endpoint,
            string adminKey,
            string testToken,
            string upstreamEndpoint)
        {
            List<TestSuiteDescriptor> suites = UnitSuites.ToList();
            suites.Add(SharedIntegrationTests.ExternalSuite(endpoint, adminKey, testToken, upstreamEndpoint));
            return suites;
        }
    }
}
