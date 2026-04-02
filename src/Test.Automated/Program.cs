namespace Test.Automated
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            string endpoint = "http://localhost:8400";
            string adminKey = "partioadmin";
            string testToken = "default";

            if (args.Length >= 1) endpoint = args[0];
            if (args.Length >= 2) adminKey = args[1];
            if (args.Length >= 3) testToken = args[2];

            AutomatedConsoleRunner runner = new AutomatedConsoleRunner(endpoint, adminKey, testToken);
            AutomatedRunSummary summary = await runner.RunAsync().ConfigureAwait(false);
            Environment.Exit(summary.FailedCount > 0 ? 1 : 0);
        }
    }
}
