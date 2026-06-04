namespace Test.Nunit
{
    using NUnit.Framework;
    using Test.Shared;

    [TestFixture]
    public class TokenizerUnitTests
    {
        [TestCaseSource(nameof(GetTests))]
        public async Task TokenizerTestPasses(SharedNamedTestCase testCase)
        {
            Assert.That(testCase, Is.Not.Null);
            await testCase.ExecuteAsync().ConfigureAwait(false);
        }

        public static IEnumerable<SharedNamedTestCase> GetTests()
        {
            return SharedTokenizerUnitTests.GetTests();
        }
    }
}
