namespace Partio.Core.Settings
{
    /// <summary>
    /// Telemetry settings: Watson built-in telemetry, Radiant/OTLP trace export, and the Prometheus scrape
    /// surface. Watson emits the HTTP layer (metrics and per-request server spans); the application layer
    /// adds <c>partio_*</c> metrics and spans. A collector (Radiant) subscribes to the <c>Watson</c> and
    /// <c>Partio</c> sources and exports traces to Tempo; Prometheus scrapes the metrics endpoints.
    /// </summary>
    public class TelemetrySettings
    {
        #region Public-Members

        /// <summary>
        /// Master switch. When true, Watson's built-in telemetry is enabled and the Radiant trace host
        /// starts; when false, all emission is skipped.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Service name reported in traces and metrics.</summary>
        public string ServiceName { get; set; } = "partio-server";

        /// <summary>
        /// OTLP collector endpoint (Tempo). Use the gRPC port (4317) with the "grpc" protocol, or the HTTP
        /// port (4318) with the "httpprotobuf" protocol (in which case include the /v1/traces path). Defaults
        /// to the IPv4 loopback (127.0.0.1) rather than "localhost", which on Windows resolves ::1 first and
        /// stalls before falling back.
        /// </summary>
        public string OtlpEndpoint { get; set; } = "http://127.0.0.1:4317";

        /// <summary>OTLP export protocol: "grpc" (default) or "httpprotobuf".</summary>
        public string OtlpProtocol { get; set; } = "grpc";

        /// <summary>
        /// Whether the Prometheus scrape surface is exposed: Watson's in-process <c>/metrics</c> endpoint for
        /// the HTTP/<c>watson.*</c> metrics, and the application's <c>/v1.0/metrics</c> endpoint for the
        /// <c>partio_*</c> families. Keep these off any public interface.
        /// </summary>
        public bool PrometheusEnabled { get; set; } = true;

        #endregion
    }
}
