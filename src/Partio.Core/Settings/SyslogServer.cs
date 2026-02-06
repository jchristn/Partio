namespace Partio.Core.Settings
{
    /// <summary>
    /// Syslog server configuration.
    /// </summary>
    public class SyslogServer
    {
        private string _Hostname = "localhost";
        private int _Port = 514;

        /// <summary>
        /// Syslog server hostname.
        /// </summary>
        public string Hostname
        {
            get => _Hostname;
            set => _Hostname = value ?? throw new ArgumentNullException(nameof(Hostname));
        }

        /// <summary>
        /// Syslog server port.
        /// </summary>
        public int Port
        {
            get => _Port;
            set => _Port = (value >= 0 && value <= 65535)
                ? value
                : throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 0 and 65535.");
        }
    }
}
