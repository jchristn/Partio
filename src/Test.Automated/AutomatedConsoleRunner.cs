namespace Test.Automated
{
    using System.Diagnostics;
    using Test.Shared;

    public class AutomatedConsoleRunner
    {
        private readonly string _Endpoint;
        private readonly string _AdminKey;
        private readonly string _TestToken;

        public AutomatedConsoleRunner(string endpoint, string adminKey, string testToken)
        {
            _Endpoint = endpoint;
            _AdminKey = adminKey;
            _TestToken = testToken;
        }

        public async Task<AutomatedRunSummary> RunAsync()
        {
            bool useColor = !Console.IsOutputRedirected;
            int consoleWidth = 100;
            try { consoleWidth = Console.WindowWidth; } catch { }
            if (consoleWidth < 40) consoleWidth = 100;

            int descriptionWidth = consoleWidth - 24; // space for result + runtime columns
            if (descriptionWidth < 20) descriptionWidth = 20;

            Console.WriteLine();
            Console.WriteLine("  Partio Automated Test Suite");
            Console.WriteLine("  Endpoint  : " + _Endpoint);
            Console.WriteLine("  Admin Key : " + _AdminKey);
            Console.WriteLine("  Token     : " + _TestToken);
            Console.WriteLine();

            // Print table header
            Console.WriteLine("  " + "Test".PadRight(descriptionWidth) + "  Result    Runtime");
            Console.WriteLine("  " + new string('-', descriptionWidth) + "  ------    --------");

            List<AutomatedTestResult> results = new List<AutomatedTestResult>();
            AutomatedTestReporter.ResultRecorded = result =>
            {
                WriteResultLine(result, descriptionWidth, Console.Out, useColor);
            };

            Stopwatch totalSw = Stopwatch.StartNew();

            // Run unit tests
            await ExecuteTestsAsync(SharedSummarizationUnitTests.GetTests(), results);

            // Run integration tests
            SharedIntegrationTests.Configure(_Endpoint, _AdminKey, _TestToken);
            await ExecuteTestsAsync(SharedIntegrationTests.GetTests(), results);

            totalSw.Stop();

            // Summary
            int passed = results.Count(r => r.Passed);
            int failed = results.Count(r => !r.Passed);

            Console.WriteLine();
            Console.WriteLine("  " + new string('=', Math.Min(consoleWidth - 4, 60)));
            Console.WriteLine("  Total: " + results.Count + "  Passed: " + passed + "  Failed: " + failed);
            Console.WriteLine("  Runtime: " + totalSw.ElapsedMilliseconds + "ms");
            Console.WriteLine("  Result: " + (failed == 0 ? "PASS" : "FAIL"));

            if (failed > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Failed tests:");
                foreach (AutomatedTestResult r in results.Where(r => !r.Passed))
                {
                    Console.WriteLine("    - " + r.TestName);
                    if (!string.IsNullOrEmpty(r.ErrorMessage))
                        Console.WriteLine("      " + r.ErrorMessage);
                }
            }

            Console.WriteLine("  " + new string('=', Math.Min(consoleWidth - 4, 60)));
            Console.WriteLine();

            return new AutomatedRunSummary
            {
                Results = results,
                TotalCount = results.Count,
                PassedCount = passed,
                FailedCount = failed,
                TotalRuntime = totalSw.Elapsed
            };
        }

        private async Task ExecuteTestsAsync(IReadOnlyList<SharedNamedTestCase> tests, List<AutomatedTestResult> results)
        {
            for (int i = 0; i < tests.Count; i++)
            {
                SharedNamedTestCase test = tests[i];
                Stopwatch sw = Stopwatch.StartNew();
                AutomatedTestResult result = new AutomatedTestResult { TestName = test.Name };

                try
                {
                    await test.ExecuteAsync().ConfigureAwait(false);
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
                    results.Add(result);
                    AutomatedTestReporter.ResultRecorded?.Invoke(result);
                }
            }
        }

        private static void WriteResultLine(AutomatedTestResult result, int descriptionWidth, TextWriter writer, bool useColor)
        {
            string name = result.TestName;
            if (name.Length > descriptionWidth)
                name = name.Substring(0, descriptionWidth - 3) + "...";

            string status = result.Passed ? "PASS" : "FAIL";
            string runtime = result.ElapsedMilliseconds + "ms";

            writer.Write("  " + name.PadRight(descriptionWidth) + "  ");

            if (useColor)
            {
                Console.ForegroundColor = result.Passed ? ConsoleColor.Green : ConsoleColor.Red;
                writer.Write(status.PadRight(10));
                Console.ResetColor();
            }
            else
            {
                writer.Write(status.PadRight(10));
            }

            writer.WriteLine(runtime.PadLeft(8));
        }
    }
}
