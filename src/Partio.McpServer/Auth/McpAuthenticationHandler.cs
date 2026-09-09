namespace Partio.McpServer.Auth
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using Partio.McpServer.Settings;
    using SyslogLogging;
    using Voltaic.Core;

    /// <summary>
    /// Authenticates inbound MCP requests by delegating to Partio's own authentication, so the MCP surface
    /// accepts exactly the same bearer tokens the REST API accepts — both admin API keys and per-tenant
    /// credential tokens. The presented token is validated against Partio (GET /v1.0/whoami) and, on success,
    /// carried forward to the tool handlers via <see cref="AuthenticationResult.Claims"/> so each tool call
    /// runs as the caller's own identity and tenant.
    /// </summary>
    public class McpAuthenticationHandler
    {
        /// <summary>Claim key under which the caller's bearer token is carried to tool handlers.</summary>
        public const string TokenClaim = "token";

        private readonly McpServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly HttpClient _Http;
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
            _Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        /// <summary>
        /// Authenticate an inbound request. CORS preflight (OPTIONS), the health endpoint, and the ping
        /// method bypass this handler in Voltaic.
        /// </summary>
        /// <param name="request">The inbound HTTP request.</param>
        /// <returns>An authentication result; rejected with 401 when the bearer is missing or Partio rejects it.</returns>
        public async Task<AuthenticationResult> AuthenticateAsync(HttpListenerRequest request)
        {
            if (!_Settings.RequireAuthentication)
            {
                // Authentication disabled: allow the request and forward the configured fallback identity.
                return Allow("anonymous", _Settings.PartioApiKey);
            }

            string? presented = ExtractBearer(request);
            if (string.IsNullOrEmpty(presented))
            {
                _Logging.Warn(_Header + "rejected request with no bearer token");
                return Deny("A bearer token is required.");
            }

            // Delegate the accept decision to Partio itself: whatever token Partio's REST auth accepts
            // (admin key or tenant credential) is accepted here; anything it rejects is rejected here.
            try
            {
                string url = _Settings.PartioEndpoint.TrimEnd('/') + "/v1.0/whoami";
                using HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, url);
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", presented);

                using HttpResponseMessage response = await _Http.SendAsync(message).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return Allow("authenticated", presented);

                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    _Logging.Warn(_Header + "Partio rejected the presented bearer token (" + (int)response.StatusCode + ")");
                    return Deny("Invalid or unauthorized bearer token.");
                }

                _Logging.Warn(_Header + "unexpected status validating bearer token: " + (int)response.StatusCode);
                return Deny("Unable to validate bearer token.");
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "authentication probe to Partio failed: " + ex.Message);
                return Deny("Unable to validate bearer token.");
            }
        }

        private static AuthenticationResult Allow(string principal, string? token)
        {
            return new AuthenticationResult
            {
                IsAuthenticated = true,
                Principal = principal,
                Claims = new Dictionary<string, string> { [TokenClaim] = token ?? string.Empty }
            };
        }

        private static AuthenticationResult Deny(string message)
        {
            return new AuthenticationResult
            {
                IsAuthenticated = false,
                StatusCode = 401,
                ErrorMessage = message
            };
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
