namespace Partio.Core.Models
{
    /// <summary>
    /// Response containing aggregated request history statistics.
    /// </summary>
    public class RequestStatisticsResponse
    {
        /// <summary>
        /// Time-bucketed request counts.
        /// </summary>
        public List<RequestStatisticsBucket> Buckets { get; set; } = new List<RequestStatisticsBucket>();

        /// <summary>
        /// Total number of successful requests (HTTP status 100-399) in the time range.
        /// </summary>
        public long TotalSuccess { get; set; } = 0;

        /// <summary>
        /// Total number of failed requests (HTTP status 400+ or null) in the time range.
        /// </summary>
        public long TotalFailure { get; set; } = 0;
    }

    /// <summary>
    /// A single time bucket within the statistics response.
    /// </summary>
    public class RequestStatisticsBucket
    {
        /// <summary>
        /// ISO 8601 timestamp prefix representing the start of this time bucket.
        /// Always "yyyy-MM-ddTHH:mm" format, e.g. "2026-03-20T14:30".
        /// </summary>
        public string TimeBucket { get; set; } = string.Empty;

        /// <summary>
        /// Number of successful requests (HTTP status 100-399) in this bucket.
        /// </summary>
        public long SuccessCount { get; set; } = 0;

        /// <summary>
        /// Number of failed requests (HTTP status 400+ or null) in this bucket.
        /// </summary>
        public long FailureCount { get; set; } = 0;
    }
}
