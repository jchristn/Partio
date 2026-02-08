namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// A single health check result record.
    /// </summary>
    public class HealthCheckRecord
    {
        [JsonPropertyName("TimestampUtc")]
        public DateTime TimestampUtc { get; set; }

        [JsonPropertyName("Success")]
        public bool Success { get; set; }
    }
}
