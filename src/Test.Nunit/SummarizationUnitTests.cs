namespace Test.Nunit
{
    using NUnit.Framework;
    using Test.Shared;

    [TestFixture]
    public class SummarizationUnitTests
    {
        [TestCaseSource(nameof(GetTests))]
        public async Task SummarizationTestPasses(SharedNamedTestCase testCase)
        {
            Assert.That(testCase, Is.Not.Null);
            await testCase.ExecuteAsync().ConfigureAwait(false);
        }

        public static IEnumerable<SharedNamedTestCase> GetTests()
        {
            return SharedSummarizationUnitTests.GetTests();
        }
    }
}
