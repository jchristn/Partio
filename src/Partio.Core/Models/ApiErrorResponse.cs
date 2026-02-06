namespace Partio.Core.Models
{
    /// <summary>
    /// Standard API error response body.
    /// </summary>
    public class ApiErrorResponse
    {
        private string _Error = string.Empty;
        private string _Message = string.Empty;
        private int _StatusCode = 500;
        private DateTime _TimestampUtc = DateTime.UtcNow;

        /// <summary>
        /// Error type or code.
        /// </summary>
        public string Error
        {
            get => _Error;
            set => _Error = value ?? string.Empty;
        }

        /// <summary>
        /// Human-readable error message.
        /// </summary>
        public string Message
        {
            get => _Message;
            set => _Message = value ?? string.Empty;
        }

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int StatusCode
        {
            get => _StatusCode;
            set => _StatusCode = value;
        }

        /// <summary>
        /// UTC timestamp of the error.
        /// </summary>
        public DateTime TimestampUtc
        {
            get => _TimestampUtc;
            set => _TimestampUtc = value;
        }
    }
}
