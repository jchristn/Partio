namespace Partio.Core.Settings
{
    /// <summary>
    /// Debug logging settings.
    /// </summary>
    public class DebugSettings
    {
        private bool _Authentication = false;
        private bool _Exceptions = true;
        private bool _Requests = false;
        private bool _DatabaseQueries = false;

        /// <summary>
        /// Log authentication debug information.
        /// </summary>
        /// <remarks>Default: false.</remarks>
        public bool Authentication
        {
            get => _Authentication;
            set => _Authentication = value;
        }

        /// <summary>
        /// Log exception details.
        /// </summary>
        /// <remarks>Default: true.</remarks>
        public bool Exceptions
        {
            get => _Exceptions;
            set => _Exceptions = value;
        }

        /// <summary>
        /// Log request debug information.
        /// </summary>
        /// <remarks>Default: false.</remarks>
        public bool Requests
        {
            get => _Requests;
            set => _Requests = value;
        }

        /// <summary>
        /// Log database query debug information.
        /// </summary>
        /// <remarks>Default: false.</remarks>
        public bool DatabaseQueries
        {
            get => _DatabaseQueries;
            set => _DatabaseQueries = value;
        }
    }
}
