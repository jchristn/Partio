namespace Partio.Core.Enums
{
    /// <summary>
    /// How a self-hosted or OpenAI-compatible endpoint (Ollama, vLLM, TEI, OpenAI) authenticates. The secret
    /// is the endpoint ApiKey in every mode except None.
    /// </summary>
    public enum EndpointAuthModeEnum
    {
        /// <summary>No credential is sent.</summary>
        None,
        /// <summary>The ApiKey is sent as <c>Authorization: Bearer {ApiKey}</c>.</summary>
        Bearer,
        /// <summary>The ApiKey is sent in a custom header named by HeaderName.</summary>
        Header,
        /// <summary>The ApiKey is appended to every request URL as the query parameter named by QueryParameterName.</summary>
        QueryParameter,
        /// <summary>HTTP basic authentication with Username and the ApiKey as the password.</summary>
        Basic
    }
}
