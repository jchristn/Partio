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
            if (HasFlag(args, "--help") || HasFlag(args, "-h"))
            {
                PrintHelp();
                return 0;
            }

            // Configured self-hosted mode: run every suite against a chosen database backend and/or real
            // upstream endpoints supplied via CLI flags (see --help). The environment is started and torn
            // down by the integration suite's hooks.
            if (TestEnvironmentOptions.HasAnyOption(args))
            {
                TestEnvironmentOptions options = TestEnvironmentOptions.Parse(args);
                if (options.UsesRealEndpoints)
                {
                    await using SelfHostedPartioTestEnvironment harnessEnvironment = await SelfHostedPartioTestEnvironment.StartAsync(options).ConfigureAwait(false);
                    if (HasFlag(args, "--sdk-harnesses"))
                    {
                        SdkHarnessRunSummary configuredSummary = await SdkHarnessRunner.RunAsync(harnessEnvironment).ConfigureAwait(false);
                        return configuredSummary.FailedCount > 0 ? 1 : 0;
                    }
                }

                return await ConsoleRunner.RunAsync(PartioTestSuites.AllForOptions(options)).ConfigureAwait(false);
            }

            if (args.Length > 0 && string.Equals(args[0], "--sdk-harnesses", StringComparison.OrdinalIgnoreCase))
            {
                await using SelfHostedPartioTestEnvironment sdkEnvironment = await SelfHostedPartioTestEnvironment.StartAsync().ConfigureAwait(false);
                SdkHarnessRunSummary sdkSummary = await SdkHarnessRunner.RunAsync(sdkEnvironment).ConfigureAwait(false);
                return sdkSummary.FailedCount > 0 ? 1 : 0;
            }

            if (args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal))
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

        private static bool HasFlag(string[] args, string flag)
        {
            foreach (string arg in args)
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Partio automated test runner");
            Console.WriteLine("");
            Console.WriteLine("Usage:");
            Console.WriteLine("  Test.Automated                         Run all suites (in-process SQLite + Ollama stub)");
            Console.WriteLine("  Test.Automated <endpoint> [adminKey] [testToken] [upstream]   Run against an external Partio server");
            Console.WriteLine("  Test.Automated --sdk-harnesses         Run the cross-language SDK harnesses");
            Console.WriteLine("");
            Console.WriteLine("Configured self-hosted mode (choose a database and/or real endpoints):");
            Console.WriteLine("  --db <Sqlite|Postgresql|Mysql|SqlServer>");
            Console.WriteLine("  --db-host <host>   --db-port <port>   --db-name <name>");
            Console.WriteLine("  --db-user <user>   --db-pass <pass>   --db-schema <schema>   --db-instance <instance>");
            Console.WriteLine("  --embedding-endpoint <url>   --embedding-model <model>");
            Console.WriteLine("  --inference-endpoint <url>   --inference-model <model>");
            Console.WriteLine("  --upstream-format <Ollama|OpenAI|vLLM|Gemini>   --upstream-bearer <token>");
            Console.WriteLine("  --sdk-harnesses   (with real endpoints) also run the SDK harnesses against the configured server");
            Console.WriteLine("");
            Console.WriteLine("Flags accept both '--flag value' and '--flag=value'.");
        }
    }
}
