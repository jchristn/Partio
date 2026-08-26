namespace Partio.Core.Observability
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Times a single outbound provider call, opens a client span named <c>&lt;service&gt; &lt;operation&gt;</c>,
    /// and records <c>partio_integration_requests_total</c> / <c>partio_integration_request_duration_seconds</c>
    /// on dispose. The outcome defaults to <c>error</c>; the caller sets it (<c>ok</c>, <c>error</c>,
    /// <c>cancelled</c>, or <c>rejected</c>) as the call resolves. The span nests under the current request
    /// span, so a slow answer resolves to the exact provider call that caused it.
    /// </summary>
    public sealed class IntegrationScope : IDisposable
    {
        private readonly string _Service;
        private readonly string _Operation;
        private readonly Activity? _Activity;
        private readonly long _StartTs;
        private bool _Disposed;

        private IntegrationScope(string service, string operation, string? endpointId)
        {
            _Service = String.IsNullOrEmpty(service) ? "(unknown)" : service;
            _Operation = String.IsNullOrEmpty(operation) ? "(unknown)" : operation;
            _Activity = PartioTelemetry.ActivitySource.StartActivity(_Service + " " + _Operation, ActivityKind.Client);
            _Activity?.SetTag(PartioTelemetry.TagService, _Service);
            _Activity?.SetTag(PartioTelemetry.TagOperation, _Operation);
            if (!String.IsNullOrEmpty(endpointId)) _Activity?.SetTag(PartioTelemetry.TagEndpointId, endpointId);
            _StartTs = Stopwatch.GetTimestamp();
        }

        /// <summary>The recorded outcome. Defaults to <c>error</c> until set by the caller.</summary>
        public string Outcome { get; set; } = "error";

        /// <summary>Begin an integration scope.</summary>
        /// <param name="service">Provider service (ollama, openai, gemini, vllm).</param>
        /// <param name="operation">Operation (embedding, completion, probe).</param>
        /// <param name="endpointId">Endpoint id for the span (high-cardinality — span only).</param>
        /// <returns>The scope.</returns>
        public static IntegrationScope Begin(string service, string operation, string? endpointId = null)
            => new IntegrationScope(service, operation, endpointId);

        /// <summary>Mark the call successful.</summary>
        public void Ok() => Outcome = "ok";

        /// <summary>Record the metric and close the span.</summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            double seconds = Stopwatch.GetElapsedTime(_StartTs).TotalSeconds;
            if (!String.Equals(Outcome, "ok", StringComparison.Ordinal)) _Activity?.SetStatus(ActivityStatusCode.Error, Outcome);
            PartioMetrics.RecordIntegration(_Service, _Operation, Outcome, seconds);
            _Activity?.Dispose();
        }
    }
}
