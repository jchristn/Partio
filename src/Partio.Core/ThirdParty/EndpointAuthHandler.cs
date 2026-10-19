namespace Partio.Core.ThirdParty
{
    using Partio.Core.Enums;
    using Partio.Core.Models;

    /// <summary>
    /// Outbound HTTP handler that attaches a custom-header, query-parameter, or basic credential to every request
    /// a provider client sends, for endpoints whose <see cref="ProviderSettings.Auth"/> selects one of those modes.
    /// PolyPrompt clients accept an injected <see cref="HttpClient"/> and never touch its default headers, so
    /// Partio wraps its own client's transport with this handler rather than changing PolyPrompt. Every instance
    /// shares one pooled inner handler, so creating a handler per request does not open new connection pools.
    /// </summary>
    public sealed class EndpointAuthHandler : DelegatingHandler
    {
        private static readonly SocketsHttpHandler _SharedInner = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };

        private readonly EndpointAuthModeEnum _Mode;
        private readonly string _Secret;
        private readonly string? _Name;
        private readonly string? _Username;

        /// <summary>
        /// Initialize a new EndpointAuthHandler.
        /// </summary>
        /// <param name="auth">Authentication settings (Header, QueryParameter, or Basic).</param>
        /// <param name="secret">Credential value: the header value, query value, or basic password.</param>
        /// <param name="innerHandler">Inner handler. Default: a shared pooled handler.</param>
        /// <exception cref="ArgumentException">The mode is not Header, QueryParameter, or Basic, or a required name is missing.</exception>
        public EndpointAuthHandler(EndpointAuthSettings auth, string secret, HttpMessageHandler? innerHandler = null)
            : base(innerHandler ?? _SharedInner)
        {
            if (auth == null) throw new ArgumentNullException(nameof(auth));
            _Mode = auth.Mode;
            _Secret = secret ?? throw new ArgumentNullException(nameof(secret));
            _Username = auth.Username;
            switch (_Mode)
            {
                case EndpointAuthModeEnum.Header:
                    _Name = !string.IsNullOrWhiteSpace(auth.HeaderName) ? auth.HeaderName : throw new ArgumentException("A header name is required.", nameof(auth));
                    break;
                case EndpointAuthModeEnum.QueryParameter:
                    _Name = !string.IsNullOrWhiteSpace(auth.QueryParameterName) ? auth.QueryParameterName : throw new ArgumentException("A query parameter name is required.", nameof(auth));
                    break;
                case EndpointAuthModeEnum.Basic:
                    break;
                default:
                    throw new ArgumentException("EndpointAuthHandler handles Header, QueryParameter, and Basic modes only.", nameof(auth));
            }
        }

        /// <summary>
        /// Resolve how a provider client authenticates. Bearer tokens stay on PolyPrompt's native path (the API key
        /// is passed to the client); custom-header, query-parameter, and basic credentials are applied by an
        /// <see cref="EndpointAuthHandler"/> transport and the client receives no API key; None sends nothing.
        /// Providers with a fixed scheme always use the API key as PolyPrompt expects.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <param name="apiKey">Configured API key.</param>
        /// <param name="settings">Provider settings.</param>
        /// <param name="innerHandler">Optional inner handler for the transport (tests).</param>
        /// <returns>The API key to give the PolyPrompt-backed client and an optional transport.</returns>
        public static ClientCredentials ResolveClientCredentials(
            ApiFormatEnum format,
            string? apiKey,
            ProviderSettings? settings,
            HttpMessageHandler? innerHandler = null)
        {
            EndpointAuthModeEnum? mode = ProviderDefaults.EffectiveAuthMode(format, apiKey, settings);
            if (!mode.HasValue) return new ClientCredentials(apiKey, innerHandler);

            switch (mode.Value)
            {
                case EndpointAuthModeEnum.None:
                    return new ClientCredentials(null, innerHandler);
                case EndpointAuthModeEnum.Bearer:
                    return new ClientCredentials(apiKey, innerHandler);
                default:
                    if (string.IsNullOrEmpty(apiKey) || settings?.Auth == null) return new ClientCredentials(null, innerHandler);
                    return new ClientCredentials(null, new EndpointAuthHandler(settings.Auth, apiKey, innerHandler));
            }
        }

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            switch (_Mode)
            {
                case EndpointAuthModeEnum.Header:
                    request.Headers.Remove(_Name!);
                    request.Headers.TryAddWithoutValidation(_Name!, _Secret);
                    break;
                case EndpointAuthModeEnum.Basic:
                    string pair = (_Username ?? string.Empty) + ":" + _Secret;
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(pair)));
                    break;
                case EndpointAuthModeEnum.QueryParameter:
                    if (request.RequestUri != null)
                    {
                        UriBuilder builder = new UriBuilder(request.RequestUri);
                        string pairText = Uri.EscapeDataString(_Name!) + "=" + Uri.EscapeDataString(_Secret);
                        string existing = builder.Query.TrimStart('?');
                        builder.Query = string.IsNullOrEmpty(existing) ? pairText : existing + "&" + pairText;
                        request.RequestUri = builder.Uri;
                    }
                    break;
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
