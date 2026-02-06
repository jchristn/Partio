namespace Partio.Core.Settings
{
    /// <summary>
    /// Request history recording and cleanup settings.
    /// </summary>
    public class RequestHistorySettings
    {
        private bool _Enabled = true;
        private string _Directory = "./request-history/";
        private int _RetentionDays = 7;
        private int _CleanupIntervalMinutes = 60;
        private int _MaxRequestBodyBytes = 65536;
        private int _MaxResponseBodyBytes = 65536;

        /// <summary>
        /// Enable request history recording.
        /// </summary>
        /// <remarks>Default: true.</remarks>
        public bool Enabled
        {
            get => _Enabled;
            set => _Enabled = value;
        }

        /// <summary>
        /// Directory for storing request/response body files.
        /// </summary>
        /// <remarks>Default: ./request-history/.</remarks>
        public string Directory
        {
            get => _Directory;
            set => _Directory = value ?? throw new ArgumentNullException(nameof(Directory));
        }

        /// <summary>
        /// Number of days to retain request history entries.
        /// </summary>
        /// <remarks>Default: 7. Range: 1–365.</remarks>
        public int RetentionDays
        {
            get => _RetentionDays;
            set => _RetentionDays = (value >= 1 && value <= 365)
                ? value
                : throw new ArgumentOutOfRangeException(nameof(RetentionDays), "RetentionDays must be between 1 and 365.");
        }

        /// <summary>
        /// Interval in minutes between cleanup cycles.
        /// </summary>
        /// <remarks>Default: 60. Range: 1–1440.</remarks>
        public int CleanupIntervalMinutes
        {
            get => _CleanupIntervalMinutes;
            set => _CleanupIntervalMinutes = (value >= 1 && value <= 1440)
                ? value
                : throw new ArgumentOutOfRangeException(nameof(CleanupIntervalMinutes), "CleanupIntervalMinutes must be between 1 and 1440.");
        }

        /// <summary>
        /// Maximum request body size to store in bytes.
        /// </summary>
        /// <remarks>Default: 65536. Range: 1024–1048576.</remarks>
        public int MaxRequestBodyBytes
        {
            get => _MaxRequestBodyBytes;
            set => _MaxRequestBodyBytes = (value >= 1024 && value <= 1048576)
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MaxRequestBodyBytes), "MaxRequestBodyBytes must be between 1024 and 1048576.");
        }

        /// <summary>
        /// Maximum response body size to store in bytes.
        /// </summary>
        /// <remarks>Default: 65536. Range: 1024–1048576.</remarks>
        public int MaxResponseBodyBytes
        {
            get => _MaxResponseBodyBytes;
            set => _MaxResponseBodyBytes = (value >= 1024 && value <= 1048576)
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MaxResponseBodyBytes), "MaxResponseBodyBytes must be between 1024 and 1048576.");
        }
    }
}
