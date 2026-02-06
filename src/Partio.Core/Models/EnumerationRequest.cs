namespace Partio.Core.Models
{
    using Partio.Core.Enums;

    /// <summary>
    /// Request object for paginated enumeration of entities.
    /// </summary>
    public class EnumerationRequest
    {
        private int _MaxResults = 100;
        private string? _ContinuationToken = null;
        private EnumerationOrderEnum _Order = EnumerationOrderEnum.CreatedDescending;
        private string? _NameFilter = null;
        private string? _LabelFilter = null;
        private string? _TagKeyFilter = null;
        private string? _TagValueFilter = null;
        private bool? _ActiveFilter = null;

        /// <summary>
        /// Maximum number of results to return.
        /// </summary>
        /// <remarks>Default: 100. Range: 1–1000.</remarks>
        public int MaxResults
        {
            get => _MaxResults;
            set => _MaxResults = (value >= 1 && value <= 1000)
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MaxResults), "MaxResults must be between 1 and 1000.");
        }

        /// <summary>
        /// Continuation token for pagination.
        /// </summary>
        public string? ContinuationToken
        {
            get => _ContinuationToken;
            set => _ContinuationToken = value;
        }

        /// <summary>
        /// Sort order for results.
        /// </summary>
        /// <remarks>Default: CreatedDescending.</remarks>
        public EnumerationOrderEnum Order
        {
            get => _Order;
            set => _Order = value;
        }

        /// <summary>
        /// Partial match filter on name.
        /// </summary>
        public string? NameFilter
        {
            get => _NameFilter;
            set => _NameFilter = value;
        }

        /// <summary>
        /// Exact match filter on labels.
        /// </summary>
        public string? LabelFilter
        {
            get => _LabelFilter;
            set => _LabelFilter = value;
        }

        /// <summary>
        /// Filter on tag key.
        /// </summary>
        public string? TagKeyFilter
        {
            get => _TagKeyFilter;
            set => _TagKeyFilter = value;
        }

        /// <summary>
        /// Filter on tag value.
        /// </summary>
        public string? TagValueFilter
        {
            get => _TagValueFilter;
            set => _TagValueFilter = value;
        }

        /// <summary>
        /// Filter on active status.
        /// </summary>
        public bool? ActiveFilter
        {
            get => _ActiveFilter;
            set => _ActiveFilter = value;
        }
    }
}
