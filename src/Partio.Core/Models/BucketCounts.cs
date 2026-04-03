namespace Partio.Core.Models
{
    /// <summary>
    /// Holds success and failure counts for a single time bucket in request statistics.
    /// </summary>
    internal class BucketCounts
    {
        /// <summary>
        /// Number of successful requests in this bucket.
        /// </summary>
        internal long Success { get; set; }

        /// <summary>
        /// Number of failed requests in this bucket.
        /// </summary>
        internal long Failure { get; set; }
    }
}
