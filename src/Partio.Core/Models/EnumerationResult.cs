namespace Partio.Core.Models
{
    /// <summary>
    /// Paginated result of an enumeration query.
    /// </summary>
    /// <typeparam name="T">Type of entity being enumerated.</typeparam>
    public class EnumerationResult<T>
    {
        private List<T> _Data = new List<T>();
        private string? _ContinuationToken = null;
        private long? _TotalCount = null;
        private bool _HasMore = false;

        /// <summary>
        /// List of results.
        /// </summary>
        public List<T> Data
        {
            get => _Data;
            set => _Data = value ?? new List<T>();
        }

        /// <summary>
        /// Continuation token for the next page.
        /// </summary>
        public string? ContinuationToken
        {
            get => _ContinuationToken;
            set => _ContinuationToken = value;
        }

        /// <summary>
        /// Total number of matching records.
        /// </summary>
        public long? TotalCount
        {
            get => _TotalCount;
            set => _TotalCount = value;
        }

        /// <summary>
        /// Whether more results are available.
        /// </summary>
        public bool HasMore
        {
            get => _HasMore;
            set => _HasMore = value;
        }
    }
}
