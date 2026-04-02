namespace Test.Shared
{
    public class AutomatedTestResult
    {
        public string SuiteName { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public bool Passed { get; set; } = false;
        public long ElapsedMilliseconds { get; set; } = 0;
        public string? ErrorMessage { get; set; } = null;
    }
}
