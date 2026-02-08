namespace Partio.Core.Models
{
    /// <summary>
    /// API response DTO combining EndpointHealthState fields with computed values.
    /// </summary>
    public class EndpointHealthStatus
    {
        /// <summary>The embedding endpoint ID.</summary>
        public string EndpointId { get; set; } = string.Empty;

        /// <summary>Model name.</summary>
        public string EndpointName { get; set; } = string.Empty;

        /// <summary>Tenant scope.</summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>Current health state.</summary>
        public bool IsHealthy { get; set; }

        /// <summary>When monitoring began.</summary>
        public DateTime FirstCheckUtc { get; set; }

        /// <summary>Most recent check time.</summary>
        public DateTime? LastCheckUtc { get; set; }

        /// <summary>Last transition to healthy.</summary>
        public DateTime? LastHealthyUtc { get; set; }

        /// <summary>Last transition to unhealthy.</summary>
        public DateTime? LastUnhealthyUtc { get; set; }

        /// <summary>Last transition in either direction.</summary>
        public DateTime? LastStateChangeUtc { get; set; }

        /// <summary>Cumulative healthy milliseconds.</summary>
        public long TotalUptimeMs { get; set; }

        /// <summary>Cumulative unhealthy milliseconds.</summary>
        public long TotalDowntimeMs { get; set; }

        /// <summary>Uptime percentage (0-100).</summary>
        public double UptimePercentage { get; set; }

        /// <summary>Consecutive successes.</summary>
        public int ConsecutiveSuccesses { get; set; }

        /// <summary>Consecutive failures.</summary>
        public int ConsecutiveFailures { get; set; }

        /// <summary>Error message from most recent failed check.</summary>
        public string? LastError { get; set; }

        /// <summary>Rolling window of check history records.</summary>
        public List<HealthCheckRecord> History { get; set; } = new List<HealthCheckRecord>();

        /// <summary>
        /// Create an EndpointHealthStatus from an EndpointHealthState.
        /// Snapshots the state and computes current-period uptime/downtime.
        /// </summary>
        public static EndpointHealthStatus FromState(EndpointHealthState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            EndpointHealthStatus status = new EndpointHealthStatus();

            lock (state.Lock)
            {
                status.EndpointId = state.EndpointId;
                status.EndpointName = state.EndpointName;
                status.TenantId = state.TenantId;
                status.IsHealthy = state.IsHealthy;
                status.FirstCheckUtc = state.FirstCheckUtc;
                status.LastCheckUtc = state.LastCheckUtc;
                status.LastHealthyUtc = state.LastHealthyUtc;
                status.LastUnhealthyUtc = state.LastUnhealthyUtc;
                status.LastStateChangeUtc = state.LastStateChangeUtc;
                status.ConsecutiveSuccesses = state.ConsecutiveSuccesses;
                status.ConsecutiveFailures = state.ConsecutiveFailures;
                status.LastError = state.LastError;

                // Compute current-period uptime/downtime
                long uptimeMs = state.TotalUptimeMs;
                long downtimeMs = state.TotalDowntimeMs;

                if (state.LastStateChangeUtc.HasValue)
                {
                    long currentPeriodMs = (long)(DateTime.UtcNow - state.LastStateChangeUtc.Value).TotalMilliseconds;
                    if (currentPeriodMs < 0) currentPeriodMs = 0;

                    if (state.IsHealthy)
                        uptimeMs += currentPeriodMs;
                    else
                        downtimeMs += currentPeriodMs;
                }

                status.TotalUptimeMs = uptimeMs;
                status.TotalDowntimeMs = downtimeMs;

                long totalMs = uptimeMs + downtimeMs;
                status.UptimePercentage = totalMs > 0 ? (double)uptimeMs / totalMs * 100.0 : 0.0;
            }

            lock (state.HistoryLock)
            {
                status.History = new List<HealthCheckRecord>(state.CheckHistory);
            }

            return status;
        }
    }
}
