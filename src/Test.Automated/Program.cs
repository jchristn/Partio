namespace Test.Automated
{
    using Test.Shared;

    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
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
            AutomatedConsoleRunner runner = new AutomatedConsoleRunner(
                environment.Endpoint,
                environment.AdminKey,
                environment.TestToken,
                environment.UpstreamEndpoint);
            AutomatedRunSummary summary = await runner.RunAsync().ConfigureAwait(false);
            return summary.FailedCount > 0 ? 1 : 0;
        }
    }
}
