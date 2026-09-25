namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Runs the shared Partio MCP server integration suite under NUnit. The suite starts a Partio
    /// server and the partio-mcp server through its before/after hooks, then executes its cases in
    /// order via the fact-style adapter.
    /// </summary>
    [TestFixture]
    public sealed class McpIntegrationTests : TouchstoneNunitBase
    {
        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return new List<TestSuiteDescriptor> { McpServerIntegrationTests.SelfHostedSuite() }; }
        }

        /// <summary>
        /// Execute the full MCP integration suite as a single test.
        /// </summary>
        /// <returns>Task.</returns>
        [Test]
        public async Task RunAll()
        {
            await RunAllAsync();
        }
    }
}
