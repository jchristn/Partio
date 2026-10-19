namespace Partio.Core.ThirdParty
{
    /// <summary>
    /// How a provider client authenticates: the API key handed to the PolyPrompt-backed client (sent the way the
    /// provider expects, for example as a bearer token) and an optional transport that applies a custom-header,
    /// query-parameter, or basic credential instead.
    /// </summary>
    public sealed class ClientCredentials
    {
        /// <summary>
        /// API key for the provider client, or null when the transport carries the credential or none is sent.
        /// </summary>
        public string? ApiKey { get; }

        /// <summary>
        /// Optional HTTP message handler for the provider client's requests.
        /// </summary>
        public HttpMessageHandler? Transport { get; }

        /// <summary>
        /// Initialize a new ClientCredentials.
        /// </summary>
        /// <param name="apiKey">API key for the provider client.</param>
        /// <param name="transport">Optional transport.</param>
        public ClientCredentials(string? apiKey, HttpMessageHandler? transport)
        {
            ApiKey = apiKey;
            Transport = transport;
        }
    }
}
