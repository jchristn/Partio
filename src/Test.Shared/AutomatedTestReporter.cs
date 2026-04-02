namespace Test.Shared
{
    using System;

    public static class AutomatedTestReporter
    {
        public static Action<AutomatedTestResult>? ResultRecorded { get; set; }
    }
}
