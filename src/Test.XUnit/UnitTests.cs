namespace Test.XUnit
{
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Xunit;

    /// <summary>
    /// Exposes every server-independent Touchstone unit case from Test.Shared as an individual
    /// xUnit theory row, so each case is discovered and reported separately by <c>dotnet test</c>.
    /// </summary>
    public class UnitTests
    {
        /// <summary>
        /// Theory data: one row per non-skipped unit case across all unit suites.
        /// </summary>
        /// <returns>Theory data of test case descriptors.</returns>
        public static TheoryData<TestCaseDescriptor> Cases()
        {
            TheoryData<TestCaseDescriptor> data = new TheoryData<TestCaseDescriptor>();

            foreach (TestSuiteDescriptor suite in PartioTestSuites.UnitSuites)
            {
                foreach (TestCaseDescriptor testCase in suite.Cases)
                {
                    if (!testCase.Skip)
                        data.Add(testCase);
                }
            }

            return data;
        }

        /// <summary>
        /// Execute a single shared unit test case.
        /// </summary>
        /// <param name="testCase">Case to execute.</param>
        [Theory]
        [MemberData(nameof(Cases))]
        public async Task Run(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
