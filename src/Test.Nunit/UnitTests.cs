namespace Test.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Exposes every server-independent Touchstone unit case from Test.Shared as an individual
    /// NUnit test via <see cref="TouchstoneTestCaseSource"/>, so each case is discovered and
    /// reported separately by <c>dotnet test</c>.
    /// </summary>
    [TestFixture]
    public class UnitTests
    {
        private static IEnumerable Cases()
        {
            return new TouchstoneTestCaseSource(PartioTestSuites.UnitSuites);
        }

        /// <summary>
        /// Execute a single shared unit test case.
        /// </summary>
        /// <param name="testCase">Case to execute.</param>
        /// <returns>Task.</returns>
        [Test]
        [TestCaseSource(nameof(Cases))]
        public async Task Run(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
