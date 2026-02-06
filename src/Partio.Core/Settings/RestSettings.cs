namespace Partio.Core.Settings
{
    /// <summary>
    /// REST server settings.
    /// </summary>
    public class RestSettings
    {
        private string _Hostname = "localhost";
        private int _Port = 8400;
        private bool _Ssl = false;

        /// <summary>
        /// Hostname to bind the REST server to.
        /// </summary>
        /// <remarks>Default: localhost.</remarks>
        public string Hostname
        {
            get => _Hostname;
            set => _Hostname = value ?? throw new ArgumentNullException(nameof(Hostname));
        }

        /// <summary>
        /// Port to bind the REST server to.
        /// </summary>
        /// <remarks>Default: 8400. Range: 0–65535.</remarks>
        public int Port
        {
            get => _Port;
            set => _Port = (value >= 0 && value <= 65535)
                ? value
                : throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 0 and 65535.");
        }

        /// <summary>
        /// Whether to enable SSL.
        /// </summary>
        /// <remarks>Default: false.</remarks>
        public bool Ssl
        {
            get => _Ssl;
            set => _Ssl = value;
        }
    }
}
