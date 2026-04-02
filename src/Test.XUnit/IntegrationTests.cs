namespace Test.XUnit
{
    using Test.Shared;
    using Xunit;

    [Collection("Integration")]
    public class IntegrationTests
    {
        private readonly IntegrationFixture _Fixture;

        public IntegrationTests(IntegrationFixture fixture)
        {
            _Fixture = fixture;
        }

        [Theory]
        [MemberData(nameof(GetTestNames))]
        public void IntegrationTestPasses(string testName)
        {
            bool found = _Fixture.Results.TryGetValue(testName, out AutomatedTestResult? result);
            Assert.True(found, "Integration result not found for '" + testName + "'.");
            Assert.True(result!.Passed, result.ErrorMessage ?? ("Integration test failed for '" + testName + "'."));
        }

        public static IEnumerable<object[]> GetTestNames()
        {
            IReadOnlyList<SharedNamedTestCase> tests = SharedIntegrationTests.GetTests();
            for (int i = 0; i < tests.Count; i++)
            {
                yield return new object[] { tests[i].Name };
            }
        }
    }
}
