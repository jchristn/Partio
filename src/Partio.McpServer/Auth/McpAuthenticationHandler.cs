namespace Partio.McpServer.Auth
{
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;
    using Partio.McpServer.Settings;
    using SyslogLogging;
    using Voltaic.Core;

    /// <summary>
    /// Validates the inbound bearer token on MCP requests against the configured accepted tokens.
    /// This is a single-key gateway: a caller presents a bearer that matches a configured admin key,
    /// and the MCP server then calls Partio using its own configured API key.
    /// </summary>
    public class McpAuthenticationHandler
    {
        private readonly McpServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[McpAuth] ";

        /// <summary>
        /// Initialize a new McpAuthenticationHandler.
        /// </summary>
        /// <param name="settings">MCP server settings.</param>
        /// <param name="logging">Logging module.</param>
        public McpAuthenticationHandler(McpServerSettings settings, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Authenticate an inbound request by inspecting its Authorization bearer header.
        /// CORS preflight (OPTIONS), the health endpoint, and the ping method bypass this handler in Voltaic.
        /// </summary>
        /// <param name="request">The inbound HTTP request.</param>
        /// <returns>An authentication result; rejected with 401 when the bearer is missing or unknown.</returns>
        public Task<AuthenticationResult> AuthenticateAsync(HttpListenerRequest request)
        {
            if (!_Settings.RequireAuthentication)
                return Task.FromResult(new AuthenticationResult { IsAuthenticated = true });

            string? presented = ExtractBearer(request);
            if (string.IsNullOrEmpty(presented))
            {
                _Logging.Warn(_Header + "rejected request with no bearer token");
                return Task.FromResult(new AuthenticationResult
                {
                    IsAuthenticated = false,
                    StatusCode = 401,
                    ErrorMessage = "A bearer token is required."
                });
            }

            List<string> accepted = _Settings.ResolveAcceptedTokens();
            bool matched = false;
            foreach (string token in accepted)
            {
                // Accumulate without short-circuiting; each comparison is fixed-time to avoid a character-by-character timing oracle.
                if (FixedTimeEquals(token, presented)) matched = true;
            }

            if (matched)
                return Task.FromResult(new AuthenticationResult { IsAuthenticated = true });

            _Logging.Warn(_Header + "rejected request with invalid bearer token");
            return Task.FromResult(new AuthenticationResult
            {
                IsAuthenticated = false,
                StatusCode = 401,
                ErrorMessage = "Invalid bearer token."
            });
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            byte[] left = Encoding.UTF8.GetBytes(a);
            byte[] right = Encoding.UTF8.GetBytes(b);
            return CryptographicOperations.FixedTimeEquals(left, right);
        }

        private static string? ExtractBearer(HttpListenerRequest request)
        {
            if (request == null) return null;
            string? header = request.Headers["Authorization"];
            if (string.IsNullOrWhiteSpace(header)) return null;

            const string prefix = "Bearer ";
            if (header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return header.Substring(prefix.Length).Trim();

            return header.Trim();
        }
    }
}
