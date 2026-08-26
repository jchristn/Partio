namespace Partio.Core.Observability
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Times a top-level processing operation (chunk, embed, summarize, process, process_batch), opens a
    /// <c>process</c> span, and records <c>partio_process_total</c> / <c>partio_process_duration_seconds</c>
    /// on dispose. The outcome defaults to <c>error</c> so a thrown exception is recorded as a failure with
    /// no catch block at the call site; call <see cref="Complete"/> on the success path, or set
    /// <see cref="Outcome"/> to <c>cancelled</c> when a cancellation is observed.
    /// </summary>
    public sealed class ProcessOperationScope : IDisposable
    {
        private readonly string _Operation;
        private readonly Activity? _Activity;
        private readonly long _StartTs;
        private bool _Disposed;

        private ProcessOperationScope(string operation)
        {
            _Operation = String.IsNullOrEmpty(operation) ? "(unknown)" : operation;
            _Activity = PartioTelemetry.ActivitySource.StartActivity("process", ActivityKind.Internal);
            _Activity?.SetTag(PartioTelemetry.TagOperation, _Operation);
            _StartTs = Stopwatch.GetTimestamp();
        }

        /// <summary>The recorded outcome. Defaults to <c>error</c> until <see cref="Complete"/> is called.</summary>
        public string Outcome { get; set; } = "error";

        /// <summary>Begin an operation scope.</summary>
        /// <param name="operation">Operation name.</param>
        /// <returns>The scope.</returns>
        public static ProcessOperationScope Begin(string operation) => new ProcessOperationScope(operation);

        /// <summary>Mark the operation successful.</summary>
        public void Complete() => Outcome = "ok";

        /// <summary>Record the metric and close the span.</summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            double seconds = Stopwatch.GetElapsedTime(_StartTs).TotalSeconds;
            if (!String.Equals(Outcome, "ok", StringComparison.Ordinal)) _Activity?.SetStatus(ActivityStatusCode.Error, Outcome);
            PartioMetrics.RecordProcess(_Operation, Outcome, seconds);
            _Activity?.Dispose();
        }
    }

    /// <summary>
    /// Times a single processing-pipeline stage (tokenize, chunk, summarize, embed), opens a
    /// <c>stage:&lt;name&gt;</c> span, and records <c>partio_process_stage_total</c> /
    /// <c>partio_process_stage_duration_seconds</c> on dispose. Same default-to-error semantics as
    /// <see cref="ProcessOperationScope"/>.
    /// </summary>
    public sealed class ProcessStageScope : IDisposable
    {
        private readonly string _Stage;
        private readonly Activity? _Activity;
        private readonly long _StartTs;
        private bool _Disposed;

        private ProcessStageScope(string stage)
        {
            _Stage = String.IsNullOrEmpty(stage) ? "(unknown)" : stage;
            _Activity = PartioTelemetry.ActivitySource.StartActivity("stage:" + _Stage, ActivityKind.Internal);
            _Activity?.SetTag(PartioTelemetry.TagStage, _Stage);
            _StartTs = Stopwatch.GetTimestamp();
        }

        /// <summary>The recorded outcome. Defaults to <c>error</c> until <see cref="Complete"/> is called.</summary>
        public string Outcome { get; set; } = "error";

        /// <summary>Begin a stage scope.</summary>
        /// <param name="stage">Stage name.</param>
        /// <returns>The scope.</returns>
        public static ProcessStageScope Begin(string stage) => new ProcessStageScope(stage);

        /// <summary>Mark the stage successful.</summary>
        public void Complete() => Outcome = "ok";

        /// <summary>Record the metric and close the span.</summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            double seconds = Stopwatch.GetElapsedTime(_StartTs).TotalSeconds;
            if (!String.Equals(Outcome, "ok", StringComparison.Ordinal)) _Activity?.SetStatus(ActivityStatusCode.Error, Outcome);
            PartioMetrics.RecordProcessStage(_Stage, Outcome, seconds);
            _Activity?.Dispose();
        }
    }
}
