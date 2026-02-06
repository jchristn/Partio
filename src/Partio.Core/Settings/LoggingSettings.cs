namespace Partio.Core.Settings
{
    /// <summary>
    /// Logging configuration settings.
    /// </summary>
    public class LoggingSettings
    {
        private List<SyslogServer> _SyslogServers = new List<SyslogServer>();
        private bool _ConsoleLogging = true;
        private bool _EnableColors = false;
        private string _LogDirectory = "./logs/";
        private string _LogFilename = "partio.log";
        private bool _FileLogging = true;
        private bool _IncludeDateInFilename = true;
        private int _MinimumSeverity = 0;

        /// <summary>
        /// List of syslog servers to forward log entries to.
        /// </summary>
        public List<SyslogServer> SyslogServers
        {
            get => _SyslogServers;
            set => _SyslogServers = value ?? new List<SyslogServer>();
        }

        /// <summary>
        /// Enable console logging output.
        /// </summary>
        /// <remarks>Default: true.</remarks>
        public bool ConsoleLogging
        {
            get => _ConsoleLogging;
            set => _ConsoleLogging = value;
        }

        /// <summary>
        /// Enable colored console output.
        /// </summary>
        /// <remarks>Default: false.</remarks>
        public bool EnableColors
        {
            get => _EnableColors;
            set => _EnableColors = value;
        }

        /// <summary>
        /// Directory for log files.
        /// </summary>
        /// <remarks>Default: ./logs/.</remarks>
        public string LogDirectory
        {
            get => _LogDirectory;
            set => _LogDirectory = value ?? throw new ArgumentNullException(nameof(LogDirectory));
        }

        /// <summary>
        /// Log filename.
        /// </summary>
        /// <remarks>Default: partio.log.</remarks>
        public string LogFilename
        {
            get => _LogFilename;
            set => _LogFilename = value ?? throw new ArgumentNullException(nameof(LogFilename));
        }

        /// <summary>
        /// Enable file logging.
        /// </summary>
        /// <remarks>Default: true.</remarks>
        public bool FileLogging
        {
            get => _FileLogging;
            set => _FileLogging = value;
        }

        /// <summary>
        /// Include date in log filename.
        /// </summary>
        /// <remarks>Default: true.</remarks>
        public bool IncludeDateInFilename
        {
            get => _IncludeDateInFilename;
            set => _IncludeDateInFilename = value;
        }

        /// <summary>
        /// Minimum severity level for logging.
        /// </summary>
        /// <remarks>Default: 0. Range: 0–7.</remarks>
        public int MinimumSeverity
        {
            get => _MinimumSeverity;
            set => _MinimumSeverity = (value >= 0 && value <= 7)
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MinimumSeverity), "MinimumSeverity must be between 0 and 7.");
        }
    }
}
