namespace Test.Automated
{
    using System;
    using System.Threading.Tasks;
    using Touchstone.Cli;
    using Test.Shared;

    /// <summary>
    /// Touchstone CLI runner for the Partio test suites. With no arguments it runs every suite,
    /// starting an in-process Partio server for the integration suite. Given an endpoint it runs
    /// the suites against an external server. The <c>--sdk-harnesses</c> switch drives the
    /// cross-language SDK harnesses against a self-hosted environment.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Process exit code: 0 when all tests pass, 1 otherwise.</returns>
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

                return await ConsoleRunner.RunAsync(
                    PartioTestSuites.AllForExternalEndpoint(endpoint, adminKey, testToken, upstreamEndpoint)).ConfigureAwait(false);
            }

            return await ConsoleRunner.RunAsync(PartioTestSuites.All).ConfigureAwait(false);
        }
    }
}
