namespace Test.XUnit
{
    using Test.Shared;
    using Xunit;

    public class SummarizationUnitTests
    {
        [Theory]
        [MemberData(nameof(GetTests))]
        public async Task SummarizationTestPasses(SharedNamedTestCase testCase)
        {
            Assert.NotNull(testCase);
            await testCase.ExecuteAsync();
        }

        public static IEnumerable<object[]> GetTests()
        {
            IReadOnlyList<SharedNamedTestCase> tests = SharedSummarizationUnitTests.GetTests();
            for (int i = 0; i < tests.Count; i++)
            {
                yield return new object[] { tests[i] };
            }
        }
    }
}
