namespace Test.Automated
{
    using Test.Shared;

    public class AutomatedRunSummary
    {
        public IReadOnlyList<AutomatedTestResult> Results { get; set; } = Array.Empty<AutomatedTestResult>();
        public int TotalCount { get; set; }
        public int PassedCount { get; set; }
        public int FailedCount { get; set; }
        public TimeSpan TotalRuntime { get; set; }
    }
}
