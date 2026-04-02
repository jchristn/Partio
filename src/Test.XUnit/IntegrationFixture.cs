namespace Test.XUnit
{
    using System.Diagnostics;
    using Test.Shared;

    public class IntegrationFixture
    {
        private static readonly object _Sync = new object();
        private static IReadOnlyDictionary<string, AutomatedTestResult>? _CachedResults = null;

        public IReadOnlyDictionary<string, AutomatedTestResult> Results
        {
            get { return EnsureResults(); }
        }

        private static IReadOnlyDictionary<string, AutomatedTestResult> EnsureResults()
        {
            lock (_Sync)
            {
                if (_CachedResults != null)
                    return _CachedResults;

                // Configure from environment or defaults
                string endpoint = Environment.GetEnvironmentVariable("PARTIO_TEST_ENDPOINT") ?? "http://localhost:8400";
                string adminKey = Environment.GetEnvironmentVariable("PARTIO_TEST_ADMIN_KEY") ?? "partioadmin";
                string testToken = Environment.GetEnvironmentVariable("PARTIO_TEST_TOKEN") ?? "default";

                SharedIntegrationTests.Configure(endpoint, adminKey, testToken);
                IReadOnlyList<SharedNamedTestCase> tests = SharedIntegrationTests.GetTests();

                Dictionary<string, AutomatedTestResult> results = new Dictionary<string, AutomatedTestResult>(StringComparer.Ordinal);

                for (int i = 0; i < tests.Count; i++)
                {
                    SharedNamedTestCase test = tests[i];
                    Stopwatch sw = Stopwatch.StartNew();
                    AutomatedTestResult result = new AutomatedTestResult { TestName = test.Name };

                    try
                    {
                        test.ExecuteAsync().GetAwaiter().GetResult();
                        result.Passed = true;
                    }
                    catch (Exception ex)
                    {
                        result.Passed = false;
                        result.ErrorMessage = ex.Message;
                    }
                    finally
                    {
                        sw.Stop();
                        result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
                    }

                    results[test.Name] = result;
                }

                _CachedResults = results;
                return _CachedResults;
            }
        }
    }
}
