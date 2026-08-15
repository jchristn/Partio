namespace Test.XUnit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using Xunit;

    /// <summary>
    /// Runs the shared Partio integration suite under xUnit. The suite starts an in-process
    /// Partio server and Ollama-compatible upstream through its before/after hooks, then executes
    /// its stateful cases in order. Using the fact-style adapter keeps the environment lifecycle
    /// correct and the ordered cases sequential.
    /// </summary>
    public sealed class IntegrationTests : TouchstoneFactBase
    {
        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return new List<TestSuiteDescriptor> { SharedIntegrationTests.SelfHostedSuite() }; }
        }

        /// <summary>
        /// Execute the full integration suite as a single fact.
        /// </summary>
        [Fact]
        public async Task RunAll()
        {
            await RunAllAsync();
        }
    }
}
