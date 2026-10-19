namespace Partio.Core.ThirdParty
{
    using System.Text.Json;
    using Partio.Core.Enums;
    using Partio.Core.Models;

    /// <summary>
    /// Provider-specific constants and rules shared by endpoint normalization and validation, client construction,
    /// health checks, and the completion proxy.
    /// </summary>
    public static class ProviderDefaults
    {
        /// <summary>
        /// Fixed Ollama Cloud API root. PolyPrompt pins Ollama Cloud clients to this host, so Partio
        /// stores it on every OllamaCloud endpoint regardless of the supplied value.
        /// </summary>
        public const string OllamaCloudEndpoint = "https://ollama.com";

        /// <summary>
        /// Default xAI API root, used when an xAI endpoint is created without an endpoint URL.
        /// </summary>
        public const string XAiEndpoint = "https://api.x.ai";

        /// <summary>
        /// Default OpenAI API root.
        /// </summary>
        public const string OpenAiEndpoint = "https://api.openai.com";

        /// <summary>
        /// Default Gemini API root.
        /// </summary>
        public const string GeminiEndpoint = "https://generativelanguage.googleapis.com";

        /// <summary>
        /// Default Anthropic API root.
        /// </summary>
        public const string AnthropicEndpoint = "https://api.anthropic.com";

        /// <summary>
        /// Default Cohere API root.
        /// </summary>
        public const string CohereEndpoint = "https://api.cohere.com";

        /// <summary>
        /// Default VoyageAI API root.
        /// </summary>
        public const string VoyageAiEndpoint = "https://api.voyageai.com";

        /// <summary>
        /// Azure OpenAI api-version used when an endpoint does not set one (matches PolyPrompt's default).
        /// </summary>
        public const string AzureOpenAiApiVersion = PolyPrompt.Clients.AzureOpenAiDefaults.ApiVersion;

        /// <summary>
        /// anthropic-version header value used when an endpoint does not set one (matches PolyPrompt's default).
        /// </summary>
        public const string AnthropicVersion = "2023-06-01";

        /// <summary>
        /// Health check interval applied to providers checked with a PolyPrompt connectivity probe, which
        /// makes an authenticated (and for some providers billable) request.
        /// </summary>
        public const int ProbeHealthCheckIntervalMs = 60000;

        /// <summary>
        /// Health check timeout applied to providers checked with a PolyPrompt connectivity probe.
        /// </summary>
        public const int ProbeHealthCheckTimeoutMs = 10000;

        /// <summary>
        /// Whether the provider dialect is the native Ollama API (local Ollama or Ollama Cloud).
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <returns>True for Ollama and OllamaCloud.</returns>
        public static bool IsOllamaDialect(ApiFormatEnum format)
        {
            return format == ApiFormatEnum.Ollama || format == ApiFormatEnum.OllamaCloud;
        }

        /// <summary>
        /// Whether the provider can serve completion (inference) endpoints.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <returns>False for the embedding-only providers TEI and VoyageAI.</returns>
        public static bool SupportsCompletion(ApiFormatEnum format)
        {
            return format != ApiFormatEnum.TEI && format != ApiFormatEnum.VoyageAI;
        }

        /// <summary>
        /// Whether the provider can serve embedding endpoints.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <returns>False for the completion-only provider Anthropic.</returns>
        public static bool SupportsEmbedding(ApiFormatEnum format)
        {
            return format != ApiFormatEnum.Anthropic;
        }

        /// <summary>
        /// Whether the provider rejects requests without an API key.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <returns>True for OllamaCloud, xAI, AzureOpenAI, Anthropic, Cohere, and VoyageAI.</returns>
        public static bool RequiresApiKey(ApiFormatEnum format)
        {
            switch (format)
            {
                case ApiFormatEnum.OllamaCloud:
                case ApiFormatEnum.xAI:
                case ApiFormatEnum.AzureOpenAI:
                case ApiFormatEnum.Anthropic:
                case ApiFormatEnum.Cohere:
                case ApiFormatEnum.VoyageAI:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether every request must be signed or carry a short-lived token minted from provider settings
        /// (AWS Signature V4 for Bedrock, OAuth for Vertex AI). Such providers cannot be health-checked with a
        /// plain HTTP probe and cannot be relayed by the transparent proxy.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <returns>True for Bedrock and VertexAI.</returns>
        public static bool UsesSignedRequests(ApiFormatEnum format)
        {
            return format == ApiFormatEnum.Bedrock || format == ApiFormatEnum.VertexAI;
        }

        /// <summary>
        /// Whether health checks call the PolyPrompt client's connectivity probe instead of an HTTP request to
        /// HealthCheckUrl. Applies to signed providers and to VoyageAI, which has no model-listing API.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <returns>True for Bedrock, VertexAI, and VoyageAI.</returns>
        public static bool UsesConnectivityProbe(ApiFormatEnum format)
        {
            return UsesSignedRequests(format) || format == ApiFormatEnum.VoyageAI;
        }

        /// <summary>
        /// Whether the transparent completion proxy can relay requests for the provider.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <returns>False for Bedrock and VertexAI.</returns>
        public static bool SupportsProxy(ApiFormatEnum format)
        {
            return !UsesSignedRequests(format);
        }

        /// <summary>
        /// Default endpoint URL for a provider when none is supplied, or an empty string when the provider has
        /// no well-known endpoint (Ollama, vLLM, TEI, and Azure OpenAI must be given a URL).
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <param name="settings">Provider settings (supplies the region for Bedrock and Vertex AI).</param>
        /// <returns>The default endpoint URL, or an empty string.</returns>
        public static string DefaultEndpoint(ApiFormatEnum format, ProviderSettings? settings = null)
        {
            switch (format)
            {
                case ApiFormatEnum.OllamaCloud: return OllamaCloudEndpoint;
                case ApiFormatEnum.xAI: return XAiEndpoint;
                case ApiFormatEnum.OpenAI: return OpenAiEndpoint;
                case ApiFormatEnum.Gemini: return GeminiEndpoint;
                case ApiFormatEnum.Anthropic: return AnthropicEndpoint;
                case ApiFormatEnum.Cohere: return CohereEndpoint;
                case ApiFormatEnum.VoyageAI: return VoyageAiEndpoint;
                case ApiFormatEnum.Bedrock:
                    string? awsRegion = settings?.Bedrock?.Region?.Trim();
                    return string.IsNullOrEmpty(awsRegion) ? string.Empty : "https://bedrock-runtime." + awsRegion + ".amazonaws.com";
                case ApiFormatEnum.VertexAI:
                    string? gcpRegion = settings?.VertexAI?.Region?.Trim();
                    return string.IsNullOrEmpty(gcpRegion) ? string.Empty : "https://" + gcpRegion + "-aiplatform.googleapis.com";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Resolve the endpoint URL Partio stores for a provider. OllamaCloud always resolves to
        /// <see cref="OllamaCloudEndpoint"/>. An empty endpoint resolves to <see cref="DefaultEndpoint"/>.
        /// Providers whose proxy and health paths include the API version drop a trailing version segment
        /// (<c>/v1</c> for xAI, Anthropic, and VoyageAI; <c>/v1</c> or <c>/v2</c> for Cohere; <c>/openai</c> for
        /// Azure OpenAI) so paths are built from the API root. Ollama, OpenAI, vLLM, and Gemini URLs are unchanged.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <param name="endpoint">Supplied endpoint URL (may be null or empty).</param>
        /// <param name="settings">Provider settings (supplies the region for Bedrock and Vertex AI defaults).</param>
        /// <returns>The endpoint URL to store.</returns>
        public static string NormalizeEndpoint(ApiFormatEnum format, string? endpoint, ProviderSettings? settings = null)
        {
            if (format == ApiFormatEnum.OllamaCloud) return OllamaCloudEndpoint;
            if (string.IsNullOrWhiteSpace(endpoint)) return DefaultEndpoint(format, settings);

            switch (format)
            {
                case ApiFormatEnum.xAI:
                case ApiFormatEnum.Anthropic:
                case ApiFormatEnum.VoyageAI:
                    return TrimSuffix(endpoint, "/v1");
                case ApiFormatEnum.Cohere:
                    return TrimSuffix(TrimSuffix(endpoint, "/v2"), "/v1");
                case ApiFormatEnum.AzureOpenAI:
                    return TrimSuffix(endpoint, "/openai");
                case ApiFormatEnum.Bedrock:
                case ApiFormatEnum.VertexAI:
                case ApiFormatEnum.TEI:
                    return endpoint.Trim().TrimEnd('/');
                default:
                    return endpoint;
            }
        }

        /// <summary>
        /// Return provider settings reduced to the section that matches the endpoint's ApiFormat, so unrelated
        /// sections are never stored. Returns null when the matching section is absent.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <param name="settings">Supplied provider settings.</param>
        /// <returns>Provider settings containing only the matching section, or null.</returns>
        public static ProviderSettings? NormalizeProviderSettings(ApiFormatEnum format, ProviderSettings? settings)
        {
            if (settings == null) return null;

            ProviderSettings normalized = new ProviderSettings();
            if (SupportsSelectableAuth(format) && settings.Auth != null)
            {
                normalized.Auth = settings.Auth;
                normalized.Auth.HeaderName = normalized.Auth.Mode == EndpointAuthModeEnum.Header ? NullIfBlank(normalized.Auth.HeaderName) : null;
                normalized.Auth.QueryParameterName = normalized.Auth.Mode == EndpointAuthModeEnum.QueryParameter ? NullIfBlank(normalized.Auth.QueryParameterName) : null;
                normalized.Auth.Username = normalized.Auth.Mode == EndpointAuthModeEnum.Basic ? NullIfBlank(normalized.Auth.Username) : null;
            }

            switch (format)
            {
                case ApiFormatEnum.AzureOpenAI:
                    if (settings.AzureOpenAI == null) return normalized.Auth != null ? normalized : null;
                    normalized.AzureOpenAI = settings.AzureOpenAI;
                    normalized.AzureOpenAI.ApiVersion = NullIfBlank(normalized.AzureOpenAI.ApiVersion);
                    break;
                case ApiFormatEnum.Anthropic:
                    if (settings.Anthropic == null) return normalized.Auth != null ? normalized : null;
                    normalized.Anthropic = settings.Anthropic;
                    normalized.Anthropic.AnthropicVersion = NullIfBlank(normalized.Anthropic.AnthropicVersion);
                    normalized.Anthropic.WorkspaceId = NullIfBlank(normalized.Anthropic.WorkspaceId);
                    break;
                case ApiFormatEnum.Bedrock:
                    if (settings.Bedrock == null) return normalized.Auth != null ? normalized : null;
                    normalized.Bedrock = settings.Bedrock;
                    normalized.Bedrock.Region = NullIfBlank(normalized.Bedrock.Region);
                    normalized.Bedrock.AccessKeyId = NullIfBlank(normalized.Bedrock.AccessKeyId);
                    normalized.Bedrock.SecretAccessKey = NullIfBlank(normalized.Bedrock.SecretAccessKey);
                    normalized.Bedrock.SessionToken = NullIfBlank(normalized.Bedrock.SessionToken);
                    break;
                case ApiFormatEnum.VertexAI:
                    if (settings.VertexAI == null) return normalized.Auth != null ? normalized : null;
                    normalized.VertexAI = settings.VertexAI;
                    normalized.VertexAI.ProjectId = NullIfBlank(normalized.VertexAI.ProjectId);
                    normalized.VertexAI.Region = NullIfBlank(normalized.VertexAI.Region);
                    normalized.VertexAI.ServiceAccountJson = NullIfBlank(normalized.VertexAI.ServiceAccountJson);
                    break;
                case ApiFormatEnum.Cohere:
                    if (settings.Cohere == null) return normalized.Auth != null ? normalized : null;
                    normalized.Cohere = settings.Cohere;
                    normalized.Cohere.InputType = NullIfBlank(normalized.Cohere.InputType);
                    break;
                case ApiFormatEnum.VoyageAI:
                    if (settings.VoyageAI == null) return normalized.Auth != null ? normalized : null;
                    normalized.VoyageAI = settings.VoyageAI;
                    normalized.VoyageAI.InputType = NullIfBlank(normalized.VoyageAI.InputType);
                    break;
                default:
                    return normalized.Auth != null ? normalized : null;
            }

            return normalized;
        }

        /// <summary>
        /// Validate that an endpoint definition carries everything its provider needs.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <param name="kind">Whether the endpoint is an embedding or completion endpoint.</param>
        /// <param name="endpoint">Normalized endpoint URL.</param>
        /// <param name="apiKey">Configured API key.</param>
        /// <param name="settings">Provider settings.</param>
        /// <exception cref="ArgumentException">A required value is missing or the provider does not support the endpoint kind.</exception>
        public static void Validate(ApiFormatEnum format, EndpointTypeEnum kind, string? endpoint, string? apiKey, ProviderSettings? settings)
        {
            if (kind == EndpointTypeEnum.Completion && !SupportsCompletion(format))
                throw new ArgumentException(DisplayName(format) + " does not support completion endpoints; create an embedding endpoint instead.", "ApiFormat");
            if (kind == EndpointTypeEnum.Embedding && !SupportsEmbedding(format))
                throw new ArgumentException(DisplayName(format) + " does not support embedding endpoints; create a completion endpoint instead.", "ApiFormat");

            ValidateApiKey(format, apiKey);
            ValidateAuth(format, apiKey, settings?.Auth);

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                if (format == ApiFormatEnum.AzureOpenAI)
                    throw new ArgumentException("Azure OpenAI endpoints require an Endpoint (the resource URL, for example https://my-resource.openai.azure.com).", "Endpoint");
                if (format != ApiFormatEnum.Bedrock && format != ApiFormatEnum.VertexAI)
                    throw new ArgumentException(DisplayName(format) + " endpoints require an Endpoint URL.", "Endpoint");
            }

            switch (format)
            {

                case ApiFormatEnum.Bedrock:
                    BedrockProviderSettings? aws = settings?.Bedrock;
                    if (aws == null)
                        throw new ArgumentException("Bedrock endpoints require ProviderSettings.Bedrock.", "ProviderSettings.Bedrock");
                    if (string.IsNullOrWhiteSpace(aws.Region))
                        throw new ArgumentException("Bedrock endpoints require ProviderSettings.Bedrock.Region.", "ProviderSettings.Bedrock.Region");
                    if (aws.CredentialSource == AwsCredentialSourceEnum.Static)
                    {
                        if (string.IsNullOrWhiteSpace(aws.AccessKeyId))
                            throw new ArgumentException("Bedrock endpoints with static credentials require ProviderSettings.Bedrock.AccessKeyId.", "ProviderSettings.Bedrock.AccessKeyId");
                        if (string.IsNullOrWhiteSpace(aws.SecretAccessKey))
                            throw new ArgumentException("Bedrock endpoints with static credentials require ProviderSettings.Bedrock.SecretAccessKey.", "ProviderSettings.Bedrock.SecretAccessKey");
                    }
                    break;

                case ApiFormatEnum.VertexAI:
                    VertexAiProviderSettings? gcp = settings?.VertexAI;
                    if (gcp == null)
                        throw new ArgumentException("Vertex AI endpoints require ProviderSettings.VertexAI.", "ProviderSettings.VertexAI");
                    if (string.IsNullOrWhiteSpace(gcp.ProjectId))
                        throw new ArgumentException("Vertex AI endpoints require ProviderSettings.VertexAI.ProjectId.", "ProviderSettings.VertexAI.ProjectId");
                    if (string.IsNullOrWhiteSpace(gcp.Region))
                        throw new ArgumentException("Vertex AI endpoints require ProviderSettings.VertexAI.Region.", "ProviderSettings.VertexAI.Region");
                    if (gcp.CredentialSource == GcpCredentialSourceEnum.ServiceAccountJson)
                        ValidateServiceAccountJson(gcp.ServiceAccountJson);
                    else if (gcp.CredentialSource == GcpCredentialSourceEnum.AccessToken && string.IsNullOrWhiteSpace(apiKey))
                        throw new ArgumentException("Vertex AI endpoints using an access token require the token in ApiKey.", "ApiKey");
                    break;
            }
        }

        /// <summary>
        /// Whether the provider's authentication depends on its deployment and can be selected through
        /// <see cref="ProviderSettings.Auth"/> (none, bearer token, custom header, query parameter, or basic).
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <returns>True for Ollama, vLLM, TEI, and OpenAI (including OpenAI-compatible gateways).</returns>
        public static bool SupportsSelectableAuth(ApiFormatEnum format)
        {
            return format == ApiFormatEnum.Ollama
                || format == ApiFormatEnum.vLLM
                || format == ApiFormatEnum.TEI
                || format == ApiFormatEnum.OpenAI;
        }

        /// <summary>
        /// Effective authentication mode for a provider with selectable authentication. Without an Auth section a
        /// configured ApiKey is sent as a bearer token (the behavior before Auth existed) and no key means None.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <param name="apiKey">Configured API key.</param>
        /// <param name="settings">Provider settings.</param>
        /// <returns>The mode, or null when the provider has a fixed authentication scheme.</returns>
        public static EndpointAuthModeEnum? EffectiveAuthMode(ApiFormatEnum format, string? apiKey, ProviderSettings? settings)
        {
            if (!SupportsSelectableAuth(format)) return null;
            if (settings?.Auth != null) return settings.Auth.Mode;
            return string.IsNullOrEmpty(apiKey) ? EndpointAuthModeEnum.None : EndpointAuthModeEnum.Bearer;
        }

        /// <summary>
        /// Append the endpoint's query-parameter credential to a URL when its authentication mode is
        /// QueryParameter; otherwise return the URL unchanged.
        /// </summary>
        /// <param name="url">Absolute or relative URL, with or without a query string.</param>
        /// <param name="format">Provider dialect.</param>
        /// <param name="apiKey">Configured API key (the parameter value).</param>
        /// <param name="settings">Provider settings.</param>
        /// <returns>The URL to request.</returns>
        public static string ApplyAuthQuery(string url, ApiFormatEnum format, string? apiKey, ProviderSettings? settings)
        {
            if (EffectiveAuthMode(format, apiKey, settings) != EndpointAuthModeEnum.QueryParameter) return url;
            string? name = settings?.Auth?.QueryParameterName;
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(apiKey)) return url;
            string separator = url.IndexOf('?') >= 0 ? "&" : "?";
            return url + separator + Uri.EscapeDataString(name) + "=" + Uri.EscapeDataString(apiKey);
        }

        private static readonly HashSet<string> _ReservedAuthHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Host", "Content-Length", "Content-Type", "Connection", "Transfer-Encoding", "Keep-Alive",
            "Proxy-Connection", "TE", "Trailer", "Upgrade", "Expect", "Accept", "Accept-Encoding", "User-Agent"
        };

        private static void ValidateAuth(ApiFormatEnum format, string? apiKey, EndpointAuthSettings? auth)
        {
            if (auth == null || !SupportsSelectableAuth(format)) return;

            if (auth.Mode != EndpointAuthModeEnum.None && string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("ProviderSettings.Auth.Mode " + auth.Mode + " requires the credential in ApiKey.", "ApiKey");

            switch (auth.Mode)
            {
                case EndpointAuthModeEnum.Header:
                    if (string.IsNullOrWhiteSpace(auth.HeaderName))
                        throw new ArgumentException("ProviderSettings.Auth.HeaderName is required when Mode is Header.", "ProviderSettings.Auth.HeaderName");
                    if (!IsHttpToken(auth.HeaderName))
                        throw new ArgumentException("ProviderSettings.Auth.HeaderName '" + auth.HeaderName + "' is not a valid HTTP header name.", "ProviderSettings.Auth.HeaderName");
                    if (_ReservedAuthHeaders.Contains(auth.HeaderName))
                        throw new ArgumentException("ProviderSettings.Auth.HeaderName '" + auth.HeaderName + "' is managed by the HTTP client and cannot carry a credential.", "ProviderSettings.Auth.HeaderName");
                    break;
                case EndpointAuthModeEnum.QueryParameter:
                    if (string.IsNullOrWhiteSpace(auth.QueryParameterName))
                        throw new ArgumentException("ProviderSettings.Auth.QueryParameterName is required when Mode is QueryParameter.", "ProviderSettings.Auth.QueryParameterName");
                    if (auth.QueryParameterName.IndexOfAny(new[] { '&', '=', '?', '#', ' ' }) >= 0)
                        throw new ArgumentException("ProviderSettings.Auth.QueryParameterName must not contain &, =, ?, #, or spaces.", "ProviderSettings.Auth.QueryParameterName");
                    break;
                case EndpointAuthModeEnum.Basic:
                    if (string.IsNullOrWhiteSpace(auth.Username))
                        throw new ArgumentException("ProviderSettings.Auth.Username is required when Mode is Basic.", "ProviderSettings.Auth.Username");
                    if (auth.Username.Contains(':'))
                        throw new ArgumentException("ProviderSettings.Auth.Username must not contain ':'.", "ProviderSettings.Auth.Username");
                    break;
            }
        }

        private static bool IsHttpToken(string value)
        {
            foreach (char c in value)
            {
                if (c <= 32 || c >= 127 || "()<>@,;:\\\"/[]?={}".IndexOf(c) >= 0) return false;
            }
            return value.Length > 0;
        }

        /// <summary>
        /// Throw when a provider that requires an API key has none configured.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <param name="apiKey">Configured API key.</param>
        /// <exception cref="ArgumentException">The provider requires an API key and none was supplied.</exception>
        public static void ValidateApiKey(ApiFormatEnum format, string? apiKey)
        {
            if (RequiresApiKey(format) && string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException(DisplayName(format) + " endpoints require an ApiKey.", "ApiKey");
        }

        /// <summary>
        /// Default health check URL for a provider, or null when the provider is checked with a connectivity
        /// probe instead of an HTTP request.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <param name="endpoint">Normalized endpoint URL.</param>
        /// <param name="settings">Provider settings (supplies the Azure api-version).</param>
        /// <returns>The health check URL, or null.</returns>
        public static string? DefaultHealthCheckUrl(ApiFormatEnum format, string endpoint, ProviderSettings? settings = null)
        {
            if (UsesConnectivityProbe(format)) return null;

            string baseUrl = (endpoint ?? string.Empty).TrimEnd('/');
            switch (format)
            {
                case ApiFormatEnum.Ollama:
                case ApiFormatEnum.OllamaCloud:
                    return baseUrl + "/api/tags";
                case ApiFormatEnum.Gemini:
                    return baseUrl + "/v1beta/models";
                case ApiFormatEnum.AzureOpenAI:
                    return baseUrl + "/openai/models?api-version=" + (settings?.AzureOpenAI?.ApiVersion ?? AzureOpenAiApiVersion);
                case ApiFormatEnum.TEI:
                    return baseUrl + "/health";
                default:
                    return baseUrl + "/v1/models";
            }
        }

        /// <summary>
        /// Whether health checks should send the endpoint credential by default.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <returns>True for every provider except local Ollama.</returns>
        public static bool DefaultHealthCheckUseAuth(ApiFormatEnum format)
        {
            return format != ApiFormatEnum.Ollama;
        }

        /// <summary>
        /// Resolve the authentication headers Partio sends upstream for a provider. Ollama, vLLM, TEI, and OpenAI
        /// follow <see cref="ProviderSettings.Auth"/> (bearer, custom header, or basic; a query-parameter credential
        /// is applied to the URL by <see cref="ApplyAuthQuery"/>). Otherwise: <c>x-goog-api-key</c> for
        /// Gemini; <c>x-api-key</c> plus <c>anthropic-version</c> (and <c>anthropic-workspace-id</c> when set) for
        /// Anthropic; <c>api-key</c> for Azure OpenAI with key auth; and <c>Authorization: Bearer</c> otherwise.
        /// Bedrock and Vertex AI with minted credentials return no headers because each request is signed by
        /// the PolyPrompt client.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <param name="apiKey">Configured API key.</param>
        /// <param name="settings">Provider settings.</param>
        /// <returns>The headers to send; empty when no credential applies.</returns>
        public static List<KeyValuePair<string, string>> ResolveAuthHeaders(ApiFormatEnum format, string? apiKey, ProviderSettings? settings = null)
        {
            List<KeyValuePair<string, string>> headers = new List<KeyValuePair<string, string>>();

            if (format == ApiFormatEnum.Bedrock) return headers;

            EndpointAuthModeEnum? selectable = EffectiveAuthMode(format, apiKey, settings);
            if (selectable.HasValue)
            {
                if (string.IsNullOrEmpty(apiKey)) return headers;
                switch (selectable.Value)
                {
                    case EndpointAuthModeEnum.Bearer:
                        headers.Add(new KeyValuePair<string, string>("Authorization", "Bearer " + apiKey));
                        break;
                    case EndpointAuthModeEnum.Header:
                        if (!string.IsNullOrEmpty(settings?.Auth?.HeaderName))
                            headers.Add(new KeyValuePair<string, string>(settings!.Auth!.HeaderName!, apiKey));
                        break;
                    case EndpointAuthModeEnum.Basic:
                        string pair = (settings?.Auth?.Username ?? string.Empty) + ":" + apiKey;
                        headers.Add(new KeyValuePair<string, string>("Authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(pair))));
                        break;
                }
                return headers;
            }
            if (format == ApiFormatEnum.VertexAI
                && (settings?.VertexAI?.CredentialSource ?? GcpCredentialSourceEnum.ServiceAccountJson) != GcpCredentialSourceEnum.AccessToken)
                return headers;
            if (string.IsNullOrEmpty(apiKey)) return headers;

            switch (format)
            {
                case ApiFormatEnum.Gemini:
                    headers.Add(new KeyValuePair<string, string>("x-goog-api-key", apiKey));
                    break;
                case ApiFormatEnum.Anthropic:
                    headers.Add(new KeyValuePair<string, string>("x-api-key", apiKey));
                    headers.Add(new KeyValuePair<string, string>("anthropic-version", settings?.Anthropic?.AnthropicVersion ?? AnthropicVersion));
                    if (!string.IsNullOrEmpty(settings?.Anthropic?.WorkspaceId))
                        headers.Add(new KeyValuePair<string, string>("anthropic-workspace-id", settings!.Anthropic!.WorkspaceId!));
                    break;
                case ApiFormatEnum.AzureOpenAI:
                    if ((settings?.AzureOpenAI?.AuthMode ?? AzureAuthModeEnum.ApiKey) == AzureAuthModeEnum.ApiKey)
                        headers.Add(new KeyValuePair<string, string>("api-key", apiKey));
                    else
                        headers.Add(new KeyValuePair<string, string>("Authorization", "Bearer " + apiKey));
                    break;
                default:
                    headers.Add(new KeyValuePair<string, string>("Authorization", "Bearer " + apiKey));
                    break;
            }

            return headers;
        }

        /// <summary>
        /// Human-readable provider name.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <returns>Display name such as "Ollama Cloud" or "AWS Bedrock".</returns>
        public static string DisplayName(ApiFormatEnum format)
        {
            switch (format)
            {
                case ApiFormatEnum.OllamaCloud: return "Ollama Cloud";
                case ApiFormatEnum.xAI: return "xAI";
                case ApiFormatEnum.AzureOpenAI: return "Azure OpenAI";
                case ApiFormatEnum.Bedrock: return "AWS Bedrock";
                case ApiFormatEnum.VertexAI: return "Google Vertex AI";
                case ApiFormatEnum.TEI: return "Hugging Face TEI";
                case ApiFormatEnum.VoyageAI: return "VoyageAI";
                default: return format.ToString();
            }
        }

        /// <summary>
        /// Low-cardinality provider service label for telemetry.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <returns>The lowercase provider label, or (unknown).</returns>
        public static string ServiceLabel(ApiFormatEnum format)
        {
            switch (format)
            {
                case ApiFormatEnum.Ollama: return "ollama";
                case ApiFormatEnum.OllamaCloud: return "ollamacloud";
                case ApiFormatEnum.OpenAI: return "openai";
                case ApiFormatEnum.vLLM: return "vllm";
                case ApiFormatEnum.Gemini: return "gemini";
                case ApiFormatEnum.xAI: return "xai";
                case ApiFormatEnum.AzureOpenAI: return "azureopenai";
                case ApiFormatEnum.Anthropic: return "anthropic";
                case ApiFormatEnum.Cohere: return "cohere";
                case ApiFormatEnum.Bedrock: return "bedrock";
                case ApiFormatEnum.VertexAI: return "vertexai";
                case ApiFormatEnum.TEI: return "tei";
                case ApiFormatEnum.VoyageAI: return "voyageai";
                default: return "(unknown)";
            }
        }

        private static void ValidateServiceAccountJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Vertex AI endpoints using a service account require ProviderSettings.VertexAI.ServiceAccountJson.", "ProviderSettings.VertexAI.ServiceAccountJson");

            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object
                    || !HasString(doc.RootElement, "client_email")
                    || !HasString(doc.RootElement, "private_key"))
                    throw new ArgumentException("ProviderSettings.VertexAI.ServiceAccountJson must be a service-account key JSON with client_email and private_key.", "ProviderSettings.VertexAI.ServiceAccountJson");
            }
            catch (JsonException)
            {
                throw new ArgumentException("ProviderSettings.VertexAI.ServiceAccountJson is not valid JSON.", "ProviderSettings.VertexAI.ServiceAccountJson");
            }
        }

        private static bool HasString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString());
        }

        private static string TrimSuffix(string endpoint, string suffix)
        {
            string trimmed = endpoint.Trim().TrimEnd('/');
            if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(0, trimmed.Length - suffix.Length);
            return trimmed;
        }

        private static string? NullIfBlank(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
