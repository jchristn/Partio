namespace Partio.Core.Models
{
    /// <summary>
    /// In-memory runtime health state for an embedding endpoint.
    /// </summary>
    public class EndpointHealthState
    {
        /// <summary>The embedding endpoint ID.</summary>
        public string EndpointId { get; set; } = string.Empty;

        /// <summary>Model name (for display/logging).</summary>
        public string EndpointName { get; set; } = string.Empty;

        /// <summary>Tenant scope.</summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>Current health state. Starts false; must prove healthy.</summary>
        public bool IsHealthy { get; set; } = false;

        /// <summary>When monitoring began.</summary>
        public DateTime FirstCheckUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Most recent check time.</summary>
        public DateTime? LastCheckUtc { get; set; }

        /// <summary>Last transition to healthy.</summary>
        public DateTime? LastHealthyUtc { get; set; }

        /// <summary>Last transition to unhealthy.</summary>
        public DateTime? LastUnhealthyUtc { get; set; }

        /// <summary>Last transition in either direction.</summary>
        public DateTime? LastStateChangeUtc { get; set; }

        /// <summary>Cumulative healthy milliseconds.</summary>
        public long TotalUptimeMs { get; set; } = 0;

        /// <summary>Cumulative unhealthy milliseconds.</summary>
        public long TotalDowntimeMs { get; set; } = 0;

        /// <summary>Running counter of consecutive successes; resets on failure.</summary>
        public int ConsecutiveSuccesses { get; set; } = 0;

        /// <summary>Running counter of consecutive failures; resets on success.</summary>
        public int ConsecutiveFailures { get; set; } = 0;

        /// <summary>Error message from most recent failed check.</summary>
        public string? LastError { get; set; }

        /// <summary>Rolling window of individual check results (max 24 hours).</summary>
        public List<HealthCheckRecord> CheckHistory { get; } = new List<HealthCheckRecord>();

        /// <summary>Per-state lock for thread safety.</summary>
        public object Lock { get; } = new object();

        /// <summary>Separate lock for history list access.</summary>
        public object HistoryLock { get; } = new object();
    }
}
