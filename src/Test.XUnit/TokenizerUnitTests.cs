namespace Test.XUnit
{
    using Test.Shared;
    using Xunit;

    public class TokenizerUnitTests
    {
        [Theory]
        [MemberData(nameof(GetTests))]
        public async Task TokenizerTestPasses(SharedNamedTestCase testCase)
        {
            Assert.NotNull(testCase);
            await testCase.ExecuteAsync();
        }

        public static IEnumerable<object[]> GetTests()
        {
            IReadOnlyList<SharedNamedTestCase> tests = SharedTokenizerUnitTests.GetTests();
            for (int i = 0; i < tests.Count; i++)
            {
                yield return new object[] { tests[i] };
            }
        }
    }
}
