namespace Partio.Server.Services
{
    using System;
    using Partio.Core.Observability;
    using Partio.Core.Settings;
    using Radiant;
    using SyslogLogging;

    /// <summary>
    /// Owns the Radiant host that exports OTLP traces to Tempo. It subscribes to both the <c>Watson</c>
    /// activity source (per-request server spans, with inbound <c>traceparent</c> adoption) and the
    /// <c>Partio</c> application activity source (<see cref="PartioTelemetry"/> — processing, integration,
    /// and background spans), so an HTTP request and the work it drives land in one trace.
    ///
    /// Metrics are not exported here — Watson serves its HTTP/<c>watson.*</c> metrics on its in-process
    /// Prometheus endpoint and the application serves <c>partio_*</c> via <see cref="PartioMetrics"/>, both
    /// scraped directly by Prometheus. All operations are best-effort: a Radiant init failure logs a warning
    /// and leaves telemetry inert rather than affecting request handling.
    /// </summary>
    public class TelemetryService : IDisposable
    {
        #region Public-Members

        /// <summary>True when the Radiant trace host started and is exporting spans.</summary>
        public bool Enabled => _Host != null;

        #endregion

        #region Private-Members

        private readonly LoggingModule _Logging;
        private readonly RadiantHost? _Host;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>Instantiate the telemetry service from settings.</summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="logging">Logging module.</param>
        public TelemetryService(TelemetrySettings settings, LoggingModule logging)
        {
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            if (settings == null || !settings.Enabled) return;

            try
            {
                RadiantSettings radiant = new RadiantSettings(settings.ServiceName) { Enable = true };

                // Traces only: subscribe to Watson's server spans and Partio's application spans.
                radiant.Sources.AddActivitySource("Watson");
                radiant.Sources.AddActivitySource(PartioTelemetry.SourceName);
                radiant.Traces.Enable = true;
                radiant.Traces.PropagateContext = true;

                // Metrics and logs are collected out of band (Prometheus scrape / file tailing), not pushed
                // through Radiant — so keep the metrics pillar off to avoid exporting to a traces endpoint.
                radiant.Metrics.Enable = false;

                radiant.Otlp.Enable = true;
                radiant.Otlp.Endpoint = settings.OtlpEndpoint;
                // Match the exporter protocol to the endpoint's port. gRPC (4317) is the default and avoids the
                // OTLP/HTTP "you must append /v1/traces to an explicit endpoint" gotcha.
                radiant.Otlp.Protocol = String.Equals(settings.OtlpProtocol, "httpprotobuf", StringComparison.OrdinalIgnoreCase)
                    ? OtlpProtocolEnum.HttpProtobuf
                    : OtlpProtocolEnum.Grpc;

                _Host = RadiantHost.Start(radiant);
                _Logging.Info("[TelemetryService] OTLP traces enabled (" + radiant.Otlp.Protocol + ") -> " + settings.OtlpEndpoint);
            }
            catch (Exception e)
            {
                _Host = null;
                _Logging.Warn("[TelemetryService] telemetry disabled (init failed): " + e.Message);
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>Dispose the telemetry host.</summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            try { _Host?.Dispose(); } catch (Exception) { }
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
