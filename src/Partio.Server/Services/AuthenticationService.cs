namespace Partio.Server.Services
{
    using Partio.Core;
    using Partio.Core.Database;
    using Partio.Core.Models;
    using Partio.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// Authentication context returned after successful authentication.
    /// </summary>
    public class AuthContext
    {
        /// <summary>Whether authentication succeeded.</summary>
        public bool IsAuthenticated { get; set; } = false;

        /// <summary>Whether the authenticated user is a global admin (via AdminApiKeys).</summary>
        public bool IsGlobalAdmin { get; set; } = false;

        /// <summary>Tenant ID (null for global admin).</summary>
        public string? TenantId { get; set; } = null;

        /// <summary>User ID (null for global admin).</summary>
        public string? UserId { get; set; } = null;

        /// <summary>Credential ID (null for global admin).</summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>The bearer token used.</summary>
        public string? Token { get; set; } = null;
    }

    /// <summary>
    /// Service for authenticating bearer tokens.
    /// </summary>
    public class AuthenticationService
    {
        private readonly ServerSettings _Settings;
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[Auth] ";

        /// <summary>
        /// Initialize a new AuthenticationService.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        public AuthenticationService(ServerSettings settings, DatabaseDriverBase database, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Authenticate a bearer token.
        /// </summary>
        /// <param name="token">Bearer token string.</param>
        /// <returns>Authentication context.</returns>
        public async Task<AuthContext> AuthenticateBearerAsync(string token)
        {
            AuthContext ctx = new AuthContext();
            ctx.Token = token;

            if (string.IsNullOrEmpty(token))
            {
                if (_Settings.Debug.Authentication)
                    _Logging.Warn(_Header + "empty bearer token");
                return ctx;
            }

            // Check admin API keys first
            if (_Settings.AdminApiKeys != null && _Settings.AdminApiKeys.Contains(token))
            {
                ctx.IsAuthenticated = true;
                ctx.IsGlobalAdmin = true;
                if (_Settings.Debug.Authentication)
                    _Logging.Info(_Header + "authenticated as global admin");
                return ctx;
            }

            // Look up credential by bearer token
            Credential? credential = await _Database.Credential.ReadByBearerTokenAsync(token).ConfigureAwait(false);
            if (credential == null)
            {
                if (_Settings.Debug.Authentication)
                    _Logging.Warn(_Header + "credential not found for token");
                return ctx;
            }

            if (!credential.Active)
            {
                if (_Settings.Debug.Authentication)
                    _Logging.Warn(_Header + "credential " + credential.Id + " is inactive");
                return ctx;
            }

            // Validate user
            UserMaster? user = await _Database.User.ReadByIdAsync(credential.UserId).ConfigureAwait(false);
            if (user == null || !user.Active)
            {
                if (_Settings.Debug.Authentication)
                    _Logging.Warn(_Header + "user " + credential.UserId + " not found or inactive");
                return ctx;
            }

            // Validate tenant
            TenantMetadata? tenant = await _Database.Tenant.ReadByIdAsync(credential.TenantId).ConfigureAwait(false);
            if (tenant == null || !tenant.Active)
            {
                if (_Settings.Debug.Authentication)
                    _Logging.Warn(_Header + "tenant " + credential.TenantId + " not found or inactive");
                return ctx;
            }

            ctx.IsAuthenticated = true;
            ctx.IsGlobalAdmin = false;
            ctx.TenantId = credential.TenantId;
            ctx.UserId = credential.UserId;
            ctx.CredentialId = credential.Id;

            if (_Settings.Debug.Authentication)
                _Logging.Info(_Header + "authenticated user " + user.Email + " tenant " + tenant.Name);

            return ctx;
        }
    }
}
