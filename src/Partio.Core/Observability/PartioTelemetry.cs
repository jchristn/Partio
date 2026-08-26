namespace Partio.Core.Observability
{
    using System.Diagnostics;

    /// <summary>
    /// Dependency-free application trace source for Partio. Wraps a single
    /// <see cref="System.Diagnostics.ActivitySource"/> named <see cref="SourceName"/> that engines,
    /// third-party clients, and route handlers use to open spans for work Watson cannot see (processing
    /// stages, outbound integrations, background jobs). A collector (Radiant, in the server) subscribes to
    /// this source name alongside <c>"Watson"</c> and exports the spans over OTLP.
    ///
    /// Instrumentation is best-effort by construction: when no collector is listening,
    /// <see cref="ActivitySource.StartActivity(string, ActivityKind)"/> returns <c>null</c> in a few
    /// nanoseconds and allocates nothing, so callers guard with the null-conditional operator and carry on.
    /// The source name and tag keys are stable public contract consumed by dashboards and trace queries.
    /// </summary>
    public static class PartioTelemetry
    {
        #region Public-Members

        /// <summary>The activity-source name a collector subscribes to. Do not rename once dashboards depend on it.</summary>
        public const string SourceName = "Partio";

        /// <summary>Span tag: the high-level operation (chunk, embed, summarize, process, process_batch).</summary>
        public const string TagOperation = "partio.operation";

        /// <summary>Span tag: a processing-pipeline stage name (tokenize, chunk, summarize, embed).</summary>
        public const string TagStage = "partio.stage";

        /// <summary>Span tag: the downstream service for an integration span (ollama, openai, gemini, vllm).</summary>
        public const string TagService = "partio.service";

        /// <summary>Span tag: the tenant id (high-cardinality — spans only, never a metric label).</summary>
        public const string TagTenantId = "partio.tenant.id";

        /// <summary>Span tag: the endpoint id (high-cardinality — spans only, never a metric label).</summary>
        public const string TagEndpointId = "partio.endpoint.id";

        /// <summary>Span tag: the endpoint kind (embedding or completion).</summary>
        public const string TagKind = "partio.kind";

        /// <summary>
        /// The shared application activity source. Spans opened on it nest under Watson's per-request server
        /// span automatically because Watson sets <see cref="Activity.Current"/> for the life of a handler.
        /// </summary>
        public static readonly ActivitySource ActivitySource = new ActivitySource(SourceName);

        #endregion
    }
}
