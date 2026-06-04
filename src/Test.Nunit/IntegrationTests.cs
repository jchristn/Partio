namespace Test.Nunit
{
    using System.Diagnostics;
    using NUnit.Framework;
    using Test.Shared;

    [TestFixture]
    public class IntegrationTests
    {
        [TestCaseSource(nameof(GetTestNames))]
        public void IntegrationTestPasses(string testName)
        {
            bool found = IntegrationResultCache.Results.TryGetValue(testName, out AutomatedTestResult? result);
            Assert.That(found, Is.True, "Integration result not found for '" + testName + "'.");
            Assert.That(result!.Passed, Is.True, result.ErrorMessage ?? ("Integration test failed for '" + testName + "'."));
        }

        public static IEnumerable<string> GetTestNames()
        {
            IReadOnlyList<SharedNamedTestCase> tests = SharedIntegrationTests.GetTests();
            for (int i = 0; i < tests.Count; i++)
            {
                yield return tests[i].Name;
            }
        }

        private static class IntegrationResultCache
        {
            private static readonly object _Sync = new object();
            private static IReadOnlyDictionary<string, AutomatedTestResult>? _Results;

            public static IReadOnlyDictionary<string, AutomatedTestResult> Results
            {
                get
                {
                    lock (_Sync)
                    {
                        if (_Results != null)
                            return _Results;

                        Dictionary<string, AutomatedTestResult> results = new Dictionary<string, AutomatedTestResult>(StringComparer.Ordinal);

                        using (SelfHostedPartioTestEnvironment environment = SelfHostedPartioTestEnvironment.StartAsync().GetAwaiter().GetResult())
                        {
                            SharedIntegrationTests.Configure(
                                environment.Endpoint,
                                environment.AdminKey,
                                environment.TestToken,
                                environment.UpstreamEndpoint);
                            IReadOnlyList<SharedNamedTestCase> tests = SharedIntegrationTests.GetTests();

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
                        }

                        _Results = results;
                        return _Results;
                    }
                }
            }
        }
    }
}
