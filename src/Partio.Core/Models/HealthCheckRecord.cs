namespace Partio.Core.Models
{
    /// <summary>
    /// A single health check result record.
    /// </summary>
    public class HealthCheckRecord
    {
        /// <summary>
        /// UTC timestamp of the check.
        /// </summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// Whether the check was successful.
        /// </summary>
        public bool Success { get; set; }
    }
}
