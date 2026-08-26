namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Partio.Core.Observability;
    using Partio.Core.Settings;
    using Partio.Server.Services;
    using SyslogLogging;
    using Touchstone.Core;

    /// <summary>
    /// White-box unit tests for the telemetry subsystem: the dependency-free <see cref="PartioMetrics"/>
    /// registry and its Prometheus rendering, the <see cref="PartioTelemetry"/> activity source, the
    /// processing/integration instrumentation scopes, <see cref="TelemetrySettings"/> defaults, and the
    /// best-effort <see cref="TelemetryService"/> lifecycle. Both positive and negative paths are covered.
    /// </summary>
    public static class TelemetryUnitTests
    {
        private const string SuiteId = "Telemetry";

        /// <summary>Build the telemetry test suite.</summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
            => new TestSuiteDescriptor(SuiteId, "Telemetry: metrics registry, instrumentation scopes, settings, and trace host", BuildCases());

        private static List<TestCaseDescriptor> BuildCases()
        {
            List<TestCaseDescriptor> tests = new List<TestCaseDescriptor>();

            // ---- PartioMetrics: rendering baseline ----

            tests.Add(TestCaseFactory.Sync(SuiteId, "Metrics: Render emits uptime gauge with HELP and TYPE", () =>
            {
                string text = PartioMetrics.Render();
                Check.Contains("# HELP partio_uptime_seconds", text, "uptime HELP");
                Check.Contains("# TYPE partio_uptime_seconds gauge", text, "uptime TYPE");
            }));

            tests.Add(TestCaseFactory.Sync(SuiteId, "Metrics: Reset clears recorded series", () =>
            {
                PartioMetrics.Reset();
                PartioMetrics.RecordProcess("reset_probe", "ok", 0.1);
                Check.Contains("operation=\"reset_probe\"", PartioMetrics.Render(), "series present before reset");
                PartioMetrics.Reset();
                string afterReset = PartioMetrics.Render();
                Check.False(afterReset.Contains("operation=\"reset_probe\""), "series cleared after reset");
                // Uptime gauge always survives a reset.
                Check.Contains("partio_uptime_seconds", afterReset, "uptime survives reset");
            }));

            // ---- PartioMetrics: process ----

            tests.Add(TestCaseFactory.Sync(SuiteId, "Metrics: RecordProcess emits counter and duration histogram", () =>
            {
                PartioMetrics.Reset();
                PartioMetrics.RecordProcess("chunk", "ok", 0.2);
                string text = PartioMetrics.Render();
                Check.Contains("partio_process_total{operation=\"chunk\",outcome=\"ok\"} 1", text, "process counter");
                Check.Contains("partio_process_duration_seconds_count{operation=\"chunk\"} 1", text, "duration count");
                Check.Contains("partio_process_duration_seconds_bucket{operation=\"chunk\",le=\"+Inf\"} 1", text, "duration +Inf bucket");
            }));

            tests.Add(TestCaseFactory.Sync(SuiteId, "Metrics: repeated RecordProcess increments the counter", () =>
            {
                PartioMetrics.Reset();
                PartioMetrics.RecordProcess("embed", "ok", 0.1);
                PartioMetrics.RecordProcess("embed", "ok", 0.1);
                PartioMetrics.RecordProcess("embed", "ok", 0.1);
                Check.Contains("partio_process_total{operation=\"embed\",outcome=\"ok\"} 3", PartioMetrics.Render(), "counter reaches 3");
            }));

            tests.Add(TestCaseFactory.Sync(SuiteId, "Metrics: RecordProcessStage emits stage counter and duration", () =>
            {
                PartioMetrics.Reset();
                PartioMetrics.RecordProcessStage("chunk", "ok", 0.03);
                string text = PartioMetrics.Render();
                Check.Contains("partio_process_stage_total{stage=\"chunk\",outcome=\"ok\"} 1", text, "stage counter");
                Check.Contains("partio_process_stage_duration_seconds_count{stage=\"chunk\"} 1", text, "stage duration count");
            }));

            // ---- PartioMetrics: histogram bucket correctness ----

            tests.Add(TestCaseFactory.Sync(SuiteId, "Metrics: histogram buckets are cumulative for a known sample", () =>
            {
                PartioMetrics.Reset();
                PartioMetrics.RecordProcessStage("bkt", "ok", 0.03); // falls into the le=0.05 bucket
                string text = PartioMetrics.Render();
                // Below the sample's bucket the cumulative count is 0; at/above it is 1.
                Check.Contains("partio_process_stage_duration_seconds_bucket{stage=\"bkt\",le=\"0.025\"} 0", text, "below-bucket cumulative 0");
                Check.Contains("partio_process_stage_duration_seconds_bucket{stage=\"bkt\",le=\"0.05\"} 1", text, "at-bucket cumulative 1");
                Check.Contains("partio_process_stage_duration_seconds_bucket{stage=\"bkt\",le=\"+Inf\"} 1", text, "+Inf cumulative 1");
                Check.Contains("partio_process_stage_duration_seconds_count{stage=\"bkt\"} 1", text, "count 1");
            }));

            // ---- PartioMetrics: integrations ----

            tests.Add(TestCaseFactory.Sync(SuiteId, "Metrics: RecordIntegration emits service/operation/outcome counter and duration", () =>
            {
                PartioMetrics.Reset();
                PartioMetrics.RecordIntegration("ollama", "embedding", "ok", 0.4);
                string text = PartioMetrics.Render();
                Check.Contains("partio_integration_requests_total{service=\"ollama\",operation=\"embedding\",outcome=\"ok\"} 1", text, "integration counter");
                Check.Contains("partio_integration_request_duration_seconds_count{service=\"ollama\",operation=\"embedding\"} 1", text, "integration duration count");
            }));

            tests.Add(TestCaseFactory.Sync(SuiteId, "Metrics: RecordIntegration distinguishes outcomes including rejected", () =>
            {
                PartioMetrics.Reset();
                PartioMetrics.RecordIntegration("openai", "completion", "rejected", 0.0);
                Check.Contains("partio_integration_requests_total{service=\"openai\",operation=\"completion\",outcome=\"rejected\"} 1", PartioMetrics.Render(), "rejected outcome recorded");
            }));

            // ---- PartioMetrics: health gauges ----

            tests.Add(TestCaseFactory.Sync(SuiteId, "Metrics: SetEndpointHealthGauges publishes healthy and unhealthy counts", () =>
            {
                PartioMetrics.Reset();
                PartioMetrics.SetEndpointHealthGauges("embedding", 3, 1);
                string text = PartioMetrics.Render();
                Check.Contains("partio_endpoints_healthy{kind=\"embedding\"} 3", text, "healthy gauge");
                Check.Contains("partio_endpoints_unhealthy{kind=\"embedding\"} 1", text, "unhealthy gauge");
            }));

            tests.Add(TestCaseFactory.Sync(SuiteId, "Metrics: SetEndpointHealthGauges overwrites the previous value", () =>
            {
                PartioMetrics.Reset();
                PartioMetrics.SetEndpointHealthGauges("completion", 5, 0);
                PartioMetrics.SetEndpointHealthGauges("completion", 2, 3);
                string text = PartioMetrics.Render();
                Check.Contains("partio_endpoints_healthy{kind=\"completion\"} 2", text, "gauge overwritten");
                Check.False(text.Contains("partio_endpoints_healthy{kind=\"completion\"} 5"), "old gauge value gone");
            }));

            // ---- PartioMetrics: health checks, model load, request history, authz ----

            tests.Add(TestCaseFactory.Sync(SuiteId, "Metrics: RecordEndpointHealthCheck / ModelLoad / RequestHistory / Authz families render", () =>
            {
                PartioMetrics.Reset();
                PartioMetrics.RecordEndpointHealthCheck("embedding", "healthy", 0.05);
                PartioMetrics.RecordModelLoad("completion", "ok", 1.2);
                PartioMetrics.RecordRequestHistoryWrite("ok");
                PartioMetrics.RecordRequestHistoryCleanup("ok");
                PartioMetrics.RecordAuthzDecision("permit");
                PartioMetrics.RecordAuthzDecision("deny");
                string text = PartioMetrics.Render();
                Check.Contains("partio_endpoint_health_checks_total{kind=\"embedding\",outcome=\"healthy\"} 1", text, "health check counter");
                Check.Contains("partio_model_load_total{kind=\"completion\",outcome=\"ok\"} 1", text, "model load counter");
                Check.Contains("partio_request_history_writes_total{outcome=\"ok\"} 1", text, "request history write counter");
                Check.Contains("partio_request_history_cleanup_total{outcome=\"ok\"} 1", text, "cleanup counter");
                Check.Contains("partio_authz_decisions_total{result=\"permit\"} 1", text, "authz permit");
                Check.Contains("partio_authz_decisions_total{result=\"deny\"} 1", text, "authz deny");
            }));

            // ---- PartioMetrics: negative / edge cases ----

            tests.Add(TestCaseFactory.Sync(SuiteId, "Metrics: null and empty labels fall back to (unknown)", () =>
            {
                PartioMetrics.Reset();
                PartioMetrics.RecordProcess(null!, null!, 0.1);
                PartioMetrics.RecordProcess("", "", 0.1);
                Check.Contains("partio_process_total{operation=\"(unknown)\",outcome=\"(unknown)\"} 2", PartioMetrics.Render(), "unknown fallback and merged series");
            }));

            tests.Add(TestCaseFactory.Sync(SuiteId, "Metrics: label values with quotes and backslashes are escaped", () =>
            {
                PartioMetrics.Reset();
                PartioMetrics.RecordProcess("a\"b\\c", "ok", 0.1);
                Check.Contains("operation=\"a\\\"b\\\\c\"", PartioMetrics.Render(), "escaped label value");
            }));

            tests.Add(TestCaseFactory.Sync(SuiteId, "Metrics: recording is thread-safe under concurrent load", () =>
            {
                PartioMetrics.Reset();
                System.Threading.Tasks.Parallel.For(0, 1000, _ => PartioMetrics.RecordProcess("concurrency", "ok", 0.001));
                Check.Contains("partio_process_total{operation=\"concurrency\",outcome=\"ok\"} 1000", PartioMetrics.Render(), "all concurrent increments counted");
            }));

            // ---- PartioTelemetry ----

            tests.Add(TestCaseFactory.Sync(SuiteId, "Telemetry: activity source is named Partio with stable tag keys", () =>
            {
                Check.Equal("Partio", PartioTelemetry.SourceName, "source name");
                Check.Equal("Partio", PartioTelemetry.ActivitySource.Name, "activity source name");
                Check.Equal("partio.operation", PartioTelemetry.TagOperation, "operation tag key");
                Check.Equal("partio.stage", PartioTelemetry.TagStage, "stage tag key");
                Check.Equal("partio.service", PartioTelemetry.TagService, "service tag key");
            }));

            tests.Add(TestCaseFactory.Sync(SuiteId, "Telemetry: StartActivity returns null when no collector is listening", () =>
            {
                // Best-effort instrumentation: with no ActivityListener subscribed, StartActivity is a cheap no-op.
                using Activity? activity = PartioTelemetry.ActivitySource.StartActivity("unit-test-span");
                Check.Null(activity, "no listener means null activity");
            }));

            // ---- Instrumentation scopes ----

            tests.Add(TestCaseFactory.Sync(SuiteId, "Scope: ProcessOperationScope records ok when completed", () =>
            {
                PartioMetrics.Reset();
                using (ProcessOperationScope op = ProcessOperationScope.Begin("scope_op_ok"))
                {
                    op.Complete();
                }
                Check.Contains("partio_process_total{operation=\"scope_op_ok\",outcome=\"ok\"} 1", PartioMetrics.Render(), "completed scope records ok");
            }));

            tests.Add(TestCaseFactory.Sync(SuiteId, "Scope: ProcessOperationScope records error when not completed", () =>
            {
                PartioMetrics.Reset();
                using (ProcessOperationScope op = ProcessOperationScope.Begin("scope_op_err"))
                {
                    // no Complete() — simulates an exception path
                }
                Check.Contains("partio_process_total{operation=\"scope_op_err\",outcome=\"error\"} 1", PartioMetrics.Render(), "incomplete scope records error");
            }));

            tests.Add(TestCaseFactory.Sync(SuiteId, "Scope: ProcessStageScope records the stage outcome", () =>
            {
                PartioMetrics.Reset();
                using (ProcessStageScope stage = ProcessStageScope.Begin("scope_stage"))
                {
                    stage.Complete();
                }
                Check.Contains("partio_process_stage_total{stage=\"scope_stage\",outcome=\"ok\"} 1", PartioMetrics.Render(), "stage scope records ok");
            }));

            tests.Add(TestCaseFactory.Sync(SuiteId, "Scope: IntegrationScope defaults to error and honors explicit outcome", () =>
            {
                PartioMetrics.Reset();
                using (IntegrationScope integ = IntegrationScope.Begin("svc", "op", "endpoint-123"))
                {
                    // leave default outcome (error)
                }
                using (IntegrationScope integ = IntegrationScope.Begin("svc", "op", "endpoint-123"))
                {
                    integ.Ok();
                }
                string text = PartioMetrics.Render();
                Check.Contains("partio_integration_requests_total{service=\"svc\",operation=\"op\",outcome=\"error\"} 1", text, "default error outcome");
                Check.Contains("partio_integration_requests_total{service=\"svc\",operation=\"op\",outcome=\"ok\"} 1", text, "explicit ok outcome");
            }));

            // ---- TelemetrySettings defaults ----

            tests.Add(TestCaseFactory.Sync(SuiteId, "Settings: TelemetrySettings has secure loopback defaults", () =>
            {
                TelemetrySettings settings = new TelemetrySettings();
                Check.True(settings.Enabled, "enabled by default");
                Check.Equal("partio-server", settings.ServiceName, "service name default");
                Check.Equal("http://127.0.0.1:4317", settings.OtlpEndpoint, "loopback IPv4 default, not localhost");
                Check.Equal("grpc", settings.OtlpProtocol, "grpc default");
                Check.True(settings.PrometheusEnabled, "prometheus enabled by default");
            }));

            tests.Add(TestCaseFactory.Sync(SuiteId, "Settings: ServerSettings exposes Telemetry and rejects null", () =>
            {
                ServerSettings settings = new ServerSettings();
                Check.NotNull(settings.Telemetry, "telemetry present by default");
                bool threw = false;
                try { settings.Telemetry = null!; }
                catch (ArgumentNullException) { threw = true; }
                Check.True(threw, "assigning null telemetry throws");
            }));

            // ---- TelemetryService lifecycle (best-effort) ----

            tests.Add(TestCaseFactory.Sync(SuiteId, "Service: disabled telemetry constructs inert and disposes cleanly", () =>
            {
                LoggingModule logging = NewQuietLogging();
                TelemetrySettings settings = new TelemetrySettings { Enabled = false };
                using TelemetryService service = new TelemetryService(settings, logging);
                Check.False(service.Enabled, "disabled service is inert");
            }));

            tests.Add(TestCaseFactory.Sync(SuiteId, "Service: null settings constructs inert rather than throwing", () =>
            {
                LoggingModule logging = NewQuietLogging();
                using TelemetryService service = new TelemetryService(null!, logging);
                Check.False(service.Enabled, "null settings means inert");
            }));

            tests.Add(TestCaseFactory.Sync(SuiteId, "Service: null logging throws ArgumentNullException", () =>
            {
                bool threw = false;
                try
                {
                    using TelemetryService service = new TelemetryService(new TelemetrySettings { Enabled = false }, null!);
                }
                catch (ArgumentNullException) { threw = true; }
                Check.True(threw, "null logging is rejected");
            }));

            tests.Add(TestCaseFactory.Sync(SuiteId, "Service: enabled telemetry starts best-effort and disposes without throwing", () =>
            {
                LoggingModule logging = NewQuietLogging();
                // A well-formed but unreachable endpoint must not crash the server: init is best-effort.
                TelemetrySettings settings = new TelemetrySettings { Enabled = true, OtlpEndpoint = "http://127.0.0.1:4317" };
                TelemetryService service = new TelemetryService(settings, logging);
                service.Dispose();
                // Double dispose is safe.
                service.Dispose();
            }));

            return tests;
        }

        private static LoggingModule NewQuietLogging()
        {
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;
            return logging;
        }
    }
}
