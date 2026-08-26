namespace Partio.Core.Observability
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Dependency-free, thread-safe, in-process Prometheus text-format metrics registry for Partio's
    /// application layer. Watson emits the HTTP surface itself (<c>http_server_*</c>, <c>watson_*</c>); this
    /// registry covers everything behind the route — the processing pipeline and its stages, outbound
    /// provider integrations, endpoint health checks, model loading, request-history persistence, and
    /// authorization decisions. Every family is prefixed <c>partio_</c>.
    ///
    /// Labeled series are stored in <see cref="ConcurrentDictionary{TKey, TValue}"/> instances keyed by a
    /// composed, escaped label string; histograms are bucketed counters. Labels are deliberately
    /// low-cardinality (closed enumerations — no ids, no free-form input); high-cardinality identifiers
    /// belong on spans. Metric families and label keys are stable public contract consumed by dashboards.
    /// </summary>
    public static class PartioMetrics
    {
        #region Private-Members

        private static readonly DateTime _StartUtc = DateTime.UtcNow;

        private static readonly double[] _Buckets = new double[]
        {
            0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 120, 300, 600
        };

        private static readonly string[] _BucketLabels = BuildBucketLabels();

        // Processing pipeline
        private static readonly ConcurrentDictionary<string, long> _Process = new ConcurrentDictionary<string, long>();
        private static readonly ConcurrentDictionary<string, HistogramSeries> _ProcessDuration = new ConcurrentDictionary<string, HistogramSeries>();
        private static readonly ConcurrentDictionary<string, long> _ProcessStage = new ConcurrentDictionary<string, long>();
        private static readonly ConcurrentDictionary<string, HistogramSeries> _ProcessStageDuration = new ConcurrentDictionary<string, HistogramSeries>();

        // Outbound integrations
        private static readonly ConcurrentDictionary<string, long> _Integration = new ConcurrentDictionary<string, long>();
        private static readonly ConcurrentDictionary<string, HistogramSeries> _IntegrationDuration = new ConcurrentDictionary<string, HistogramSeries>();

        // Endpoint health
        private static readonly ConcurrentDictionary<string, long> _HealthChecks = new ConcurrentDictionary<string, long>();
        private static readonly ConcurrentDictionary<string, HistogramSeries> _HealthCheckDuration = new ConcurrentDictionary<string, HistogramSeries>();
        private static readonly ConcurrentDictionary<string, double> _EndpointsHealthy = new ConcurrentDictionary<string, double>();
        private static readonly ConcurrentDictionary<string, double> _EndpointsUnhealthy = new ConcurrentDictionary<string, double>();

        // Model load
        private static readonly ConcurrentDictionary<string, long> _ModelLoad = new ConcurrentDictionary<string, long>();
        private static readonly ConcurrentDictionary<string, HistogramSeries> _ModelLoadDuration = new ConcurrentDictionary<string, HistogramSeries>();

        // Request history + authz
        private static readonly ConcurrentDictionary<string, long> _RequestHistoryWrites = new ConcurrentDictionary<string, long>();
        private static readonly ConcurrentDictionary<string, long> _RequestHistoryCleanup = new ConcurrentDictionary<string, long>();
        private static readonly ConcurrentDictionary<string, long> _AuthzDecisions = new ConcurrentDictionary<string, long>();

        #endregion

        #region Public-Methods

        /// <summary>Record a completed top-level processing operation.</summary>
        /// <param name="operation">Operation: chunk, embed, summarize, process, or process_batch.</param>
        /// <param name="outcome">Outcome: ok, error, or cancelled.</param>
        /// <param name="seconds">Total operation duration in seconds.</param>
        public static void RecordProcess(string operation, string outcome, double seconds)
        {
            string op = Safe(operation);
            string oc = Safe(outcome);
            Increment(_Process, "operation=\"" + Escape(op) + "\",outcome=\"" + Escape(oc) + "\"");
            _ProcessDuration.GetOrAdd("operation=\"" + Escape(op) + "\"", CreateHistogram).Observe(seconds);
        }

        /// <summary>Record one processing-pipeline stage result and duration.</summary>
        /// <param name="stage">Stage: tokenize, chunk, summarize, embed, or queued.</param>
        /// <param name="outcome">Outcome: ok or error.</param>
        /// <param name="seconds">Stage duration in seconds.</param>
        public static void RecordProcessStage(string stage, string outcome, double seconds)
        {
            string st = Safe(stage);
            string oc = Safe(outcome);
            Increment(_ProcessStage, "stage=\"" + Escape(st) + "\",outcome=\"" + Escape(oc) + "\"");
            _ProcessStageDuration.GetOrAdd("stage=\"" + Escape(st) + "\"", CreateHistogram).Observe(seconds);
        }

        /// <summary>Record an outbound integration (provider) call result and duration.</summary>
        /// <param name="service">Provider service: ollama, openai, gemini, or vllm.</param>
        /// <param name="operation">Operation: embedding, completion, or probe.</param>
        /// <param name="outcome">Outcome: ok, error, cancelled, or rejected.</param>
        /// <param name="seconds">Call duration in seconds.</param>
        public static void RecordIntegration(string service, string operation, string outcome, double seconds)
        {
            string sv = Safe(service);
            string op = Safe(operation);
            string oc = Safe(outcome);
            Increment(_Integration, "service=\"" + Escape(sv) + "\",operation=\"" + Escape(op) + "\",outcome=\"" + Escape(oc) + "\"");
            _IntegrationDuration.GetOrAdd("service=\"" + Escape(sv) + "\",operation=\"" + Escape(op) + "\"", CreateHistogram).Observe(seconds);
        }

        /// <summary>Record a single endpoint health-check poll.</summary>
        /// <param name="kind">Endpoint kind: embedding or completion.</param>
        /// <param name="outcome">Outcome: healthy or unhealthy.</param>
        /// <param name="seconds">Check duration in seconds.</param>
        public static void RecordEndpointHealthCheck(string kind, string outcome, double seconds)
        {
            string kd = Safe(kind);
            string oc = Safe(outcome);
            Increment(_HealthChecks, "kind=\"" + Escape(kd) + "\",outcome=\"" + Escape(oc) + "\"");
            _HealthCheckDuration.GetOrAdd("kind=\"" + Escape(kd) + "\"", CreateHistogram).Observe(seconds);
        }

        /// <summary>Set the current healthy/unhealthy endpoint counts for a kind (observable gauges).</summary>
        /// <param name="kind">Endpoint kind: embedding or completion.</param>
        /// <param name="healthy">Count of healthy endpoints.</param>
        /// <param name="unhealthy">Count of unhealthy endpoints.</param>
        public static void SetEndpointHealthGauges(string kind, int healthy, int unhealthy)
        {
            string key = "kind=\"" + Escape(Safe(kind)) + "\"";
            _EndpointsHealthy[key] = healthy;
            _EndpointsUnhealthy[key] = unhealthy;
        }

        /// <summary>Record a model-load (warm-up) request result and duration.</summary>
        /// <param name="kind">Endpoint kind: embedding or completion.</param>
        /// <param name="outcome">Outcome: ok or error.</param>
        /// <param name="seconds">Load duration in seconds.</param>
        public static void RecordModelLoad(string kind, string outcome, double seconds)
        {
            string kd = Safe(kind);
            string oc = Safe(outcome);
            Increment(_ModelLoad, "kind=\"" + Escape(kd) + "\",outcome=\"" + Escape(oc) + "\"");
            _ModelLoadDuration.GetOrAdd("kind=\"" + Escape(kd) + "\"", CreateHistogram).Observe(seconds);
        }

        /// <summary>Record a request-history persistence outcome.</summary>
        /// <param name="outcome">Outcome: ok or error.</param>
        public static void RecordRequestHistoryWrite(string outcome)
        {
            Increment(_RequestHistoryWrites, "outcome=\"" + Escape(Safe(outcome)) + "\"");
        }

        /// <summary>Record a request-history cleanup sweep outcome.</summary>
        /// <param name="outcome">Outcome: ok or error.</param>
        public static void RecordRequestHistoryCleanup(string outcome)
        {
            Increment(_RequestHistoryCleanup, "outcome=\"" + Escape(Safe(outcome)) + "\"");
        }

        /// <summary>Record an authorization decision.</summary>
        /// <param name="result">Result: permit or deny.</param>
        public static void RecordAuthzDecision(string result)
        {
            Increment(_AuthzDecisions, "result=\"" + Escape(Safe(result)) + "\"");
        }

        /// <summary>Render the current metrics in Prometheus text exposition format.</summary>
        /// <returns>Prometheus-formatted metrics.</returns>
        public static string Render()
        {
            StringBuilder sb = new StringBuilder();

            AppendGauge(sb, "partio_uptime_seconds", "Server uptime in seconds", Math.Max(0.0, (DateTime.UtcNow - _StartUtc).TotalSeconds));

            AppendCounterFamily(sb, "partio_process_total", "Processing operations by operation and outcome", _Process);
            AppendHistogramFamily(sb, "partio_process_duration_seconds", "Processing operation duration in seconds, by operation", _ProcessDuration);
            AppendCounterFamily(sb, "partio_process_stage_total", "Processing pipeline stages by stage and outcome", _ProcessStage);
            AppendHistogramFamily(sb, "partio_process_stage_duration_seconds", "Processing stage duration in seconds, by stage", _ProcessStageDuration);

            AppendCounterFamily(sb, "partio_integration_requests_total", "Outbound provider calls by service, operation, and outcome", _Integration);
            AppendHistogramFamily(sb, "partio_integration_request_duration_seconds", "Outbound provider call duration in seconds, by service and operation", _IntegrationDuration);

            AppendCounterFamily(sb, "partio_endpoint_health_checks_total", "Endpoint health checks by kind and outcome", _HealthChecks);
            AppendHistogramFamily(sb, "partio_endpoint_health_check_duration_seconds", "Endpoint health-check duration in seconds, by kind", _HealthCheckDuration);
            AppendGaugeFamily(sb, "partio_endpoints_healthy", "Healthy endpoints by kind", _EndpointsHealthy);
            AppendGaugeFamily(sb, "partio_endpoints_unhealthy", "Unhealthy endpoints by kind", _EndpointsUnhealthy);

            AppendCounterFamily(sb, "partio_model_load_total", "Model-load requests by kind and outcome", _ModelLoad);
            AppendHistogramFamily(sb, "partio_model_load_duration_seconds", "Model-load duration in seconds, by kind", _ModelLoadDuration);

            AppendCounterFamily(sb, "partio_request_history_writes_total", "Request-history persistence by outcome", _RequestHistoryWrites);
            AppendCounterFamily(sb, "partio_request_history_cleanup_total", "Request-history cleanup sweeps by outcome", _RequestHistoryCleanup);
            AppendCounterFamily(sb, "partio_authz_decisions_total", "Authorization decisions by result", _AuthzDecisions);

            return sb.ToString();
        }

        /// <summary>
        /// Clear all recorded series. Intended for test isolation only; metrics are otherwise cumulative for
        /// the process lifetime.
        /// </summary>
        public static void Reset()
        {
            _Process.Clear();
            _ProcessDuration.Clear();
            _ProcessStage.Clear();
            _ProcessStageDuration.Clear();
            _Integration.Clear();
            _IntegrationDuration.Clear();
            _HealthChecks.Clear();
            _HealthCheckDuration.Clear();
            _EndpointsHealthy.Clear();
            _EndpointsUnhealthy.Clear();
            _ModelLoad.Clear();
            _ModelLoadDuration.Clear();
            _RequestHistoryWrites.Clear();
            _RequestHistoryCleanup.Clear();
            _AuthzDecisions.Clear();
        }

        #endregion

        #region Private-Methods

        private static HistogramSeries CreateHistogram(string key)
        {
            return new HistogramSeries();
        }

        private static string Safe(string value)
        {
            return String.IsNullOrEmpty(value) ? "(unknown)" : value;
        }

        private static void Increment(ConcurrentDictionary<string, long> family, string labelKey)
        {
            family.AddOrUpdate(labelKey, 1L, IncrementExisting);
        }

        private static long IncrementExisting(string key, long existing)
        {
            return existing + 1L;
        }

        private static string[] BuildBucketLabels()
        {
            string[] labels = new string[_Buckets.Length];
            for (int i = 0; i < _Buckets.Length; i++)
            {
                labels[i] = _Buckets[i].ToString(CultureInfo.InvariantCulture);
            }
            return labels;
        }

        private static void AppendGauge(StringBuilder sb, string name, string help, double value)
        {
            sb.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n');
            sb.Append("# TYPE ").Append(name).Append(" gauge\n");
            sb.Append(name).Append(' ').Append(value.ToString("F0", CultureInfo.InvariantCulture)).Append('\n');
        }

        private static void AppendGaugeFamily(StringBuilder sb, string name, string help, ConcurrentDictionary<string, double> family)
        {
            sb.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n');
            sb.Append("# TYPE ").Append(name).Append(" gauge\n");
            foreach (KeyValuePair<string, double> series in family)
            {
                sb.Append(name).Append('{').Append(series.Key).Append("} ").Append(series.Value.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
        }

        private static void AppendCounterFamily(StringBuilder sb, string name, string help, ConcurrentDictionary<string, long> family)
        {
            sb.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n');
            sb.Append("# TYPE ").Append(name).Append(" counter\n");
            foreach (KeyValuePair<string, long> series in family)
            {
                sb.Append(name).Append('{').Append(series.Key).Append("} ").Append(series.Value.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
        }

        private static void AppendHistogramFamily(StringBuilder sb, string name, string help, ConcurrentDictionary<string, HistogramSeries> family)
        {
            sb.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n');
            sb.Append("# TYPE ").Append(name).Append(" histogram\n");
            foreach (KeyValuePair<string, HistogramSeries> series in family)
            {
                series.Value.AppendTo(sb, name, series.Key);
            }
        }

        private static string FormatDouble(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            if (String.IsNullOrEmpty(value)) return String.Empty;
            StringBuilder sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (c == '\\') sb.Append("\\\\");
                else if (c == '"') sb.Append("\\\"");
                else if (c == '\n') sb.Append("\\n");
                else sb.Append(c);
            }
            return sb.ToString();
        }

        #endregion

        #region Nested-Types

        /// <summary>
        /// A single bucketed histogram series. Bucket counts are non-cumulative per index (rendered
        /// cumulatively) with a trailing +Inf bucket; sum and sample count are maintained alongside.
        /// </summary>
        private sealed class HistogramSeries
        {
            private readonly long[] _Counts = new long[_Buckets.Length + 1];
            private readonly object _Lock = new object();
            private long _SampleCount = 0;
            private double _Sum = 0.0;

            /// <summary>Observe a single sample value (seconds).</summary>
            /// <param name="value">Sample value.</param>
            public void Observe(double value)
            {
                int index = _Buckets.Length;
                for (int i = 0; i < _Buckets.Length; i++)
                {
                    if (value <= _Buckets[i])
                    {
                        index = i;
                        break;
                    }
                }

                lock (_Lock)
                {
                    _Counts[index] = _Counts[index] + 1L;
                    _SampleCount = _SampleCount + 1L;
                    _Sum = _Sum + value;
                }
            }

            /// <summary>Append this series to the exposition output.</summary>
            /// <param name="sb">Target builder.</param>
            /// <param name="name">Metric family name.</param>
            /// <param name="innerLabels">Escaped label content without surrounding braces.</param>
            public void AppendTo(StringBuilder sb, string name, string innerLabels)
            {
                long[] snapshot = new long[_Counts.Length];
                long sampleCount;
                double sum;
                lock (_Lock)
                {
                    Array.Copy(_Counts, snapshot, _Counts.Length);
                    sampleCount = _SampleCount;
                    sum = _Sum;
                }

                long cumulative = 0;
                for (int i = 0; i < _Buckets.Length; i++)
                {
                    cumulative = cumulative + snapshot[i];
                    sb.Append(name).Append("_bucket{").Append(innerLabels).Append(",le=\"").Append(_BucketLabels[i]).Append("\"} ")
                      .Append(cumulative.ToString(CultureInfo.InvariantCulture)).Append('\n');
                }

                cumulative = cumulative + snapshot[_Buckets.Length];
                sb.Append(name).Append("_bucket{").Append(innerLabels).Append(",le=\"+Inf\"} ")
                  .Append(cumulative.ToString(CultureInfo.InvariantCulture)).Append('\n');

                sb.Append(name).Append("_sum{").Append(innerLabels).Append("} ").Append(FormatDouble(sum)).Append('\n');
                sb.Append(name).Append("_count{").Append(innerLabels).Append("} ").Append(sampleCount.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
        }

        #endregion
    }
}
