namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Runs the shared Partio integration suite under NUnit. The suite starts an in-process
    /// Partio server and Ollama-compatible upstream through its before/after hooks, then executes
    /// its stateful cases in order via the fact-style adapter.
    /// </summary>
    [TestFixture]
    public sealed class IntegrationTests : TouchstoneNunitBase
    {
        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return new List<TestSuiteDescriptor> { SharedIntegrationTests.SelfHostedSuite() }; }
        }

        /// <summary>
        /// Execute the full integration suite as a single test.
        /// </summary>
        /// <returns>Task.</returns>
        [Test]
        public async Task RunAll()
        {
            await RunAllAsync();
        }
    }
}
