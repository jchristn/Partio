namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Health status for a monitored embedding endpoint.
    /// </summary>
    public class EndpointHealthStatus
    {
        [JsonPropertyName("EndpointId")]
        public string EndpointId { get; set; } = string.Empty;

        [JsonPropertyName("EndpointName")]
        public string EndpointName { get; set; } = string.Empty;

        [JsonPropertyName("TenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonPropertyName("IsHealthy")]
        public bool IsHealthy { get; set; }

        [JsonPropertyName("FirstCheckUtc")]
        public DateTime FirstCheckUtc { get; set; }

        [JsonPropertyName("LastCheckUtc")]
        public DateTime? LastCheckUtc { get; set; }

        [JsonPropertyName("LastHealthyUtc")]
        public DateTime? LastHealthyUtc { get; set; }

        [JsonPropertyName("LastUnhealthyUtc")]
        public DateTime? LastUnhealthyUtc { get; set; }

        [JsonPropertyName("LastStateChangeUtc")]
        public DateTime? LastStateChangeUtc { get; set; }

        [JsonPropertyName("TotalUptimeMs")]
        public long TotalUptimeMs { get; set; }

        [JsonPropertyName("TotalDowntimeMs")]
        public long TotalDowntimeMs { get; set; }

        [JsonPropertyName("UptimePercentage")]
        public double UptimePercentage { get; set; }

        [JsonPropertyName("ConsecutiveSuccesses")]
        public int ConsecutiveSuccesses { get; set; }

        [JsonPropertyName("ConsecutiveFailures")]
        public int ConsecutiveFailures { get; set; }

        [JsonPropertyName("LastError")]
        public string? LastError { get; set; }

        [JsonPropertyName("History")]
        public List<HealthCheckRecord> History { get; set; } = new List<HealthCheckRecord>();
    }
}
