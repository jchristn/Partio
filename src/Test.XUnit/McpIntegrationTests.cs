namespace Test.XUnit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using Xunit;

    /// <summary>
    /// Runs the shared Partio MCP server integration suite under xUnit. The suite starts a Partio
    /// server and the partio-mcp server through its before/after hooks, then executes its cases in
    /// order against the MCP server's HTTP endpoints.
    /// </summary>
    public sealed class McpIntegrationTests : TouchstoneFactBase
    {
        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return new List<TestSuiteDescriptor> { McpServerIntegrationTests.SelfHostedSuite() }; }
        }

        /// <summary>
        /// Execute the full MCP integration suite as a single fact.
        /// </summary>
        [Fact]
        public async Task RunAll()
        {
            await RunAllAsync();
        }
    }
}
