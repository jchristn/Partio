namespace Partio.Core.Models
{
    using Partio.Core.Enums;

    /// <summary>
    /// Selectable authentication for providers whose authentication depends on how they are deployed: Ollama,
    /// vLLM, TEI, and OpenAI-compatible services, which are often placed behind gateways or proxies. The secret
    /// (token, header value, query value, or password) is the endpoint ApiKey, which is masked on read.
    /// Providers with a documented scheme (Anthropic, Gemini, Azure OpenAI, and the other hosted providers)
    /// ignore this section.
    /// </summary>
    public class EndpointAuthSettings
    {
        /// <summary>
        /// Authentication mode.
        /// </summary>
        public EndpointAuthModeEnum Mode { get; set; } = EndpointAuthModeEnum.Bearer;

        /// <summary>
        /// Header that carries the ApiKey when Mode is Header, for example X-API-Key.
        /// </summary>
        public string? HeaderName { get; set; }

        /// <summary>
        /// Query parameter that carries the ApiKey when Mode is QueryParameter, for example api_key.
        /// </summary>
        public string? QueryParameterName { get; set; }

        /// <summary>
        /// User name when Mode is Basic. The ApiKey is the password.
        /// </summary>
        public string? Username { get; set; }
    }
}
