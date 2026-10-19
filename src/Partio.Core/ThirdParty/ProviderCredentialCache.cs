namespace Partio.Core.ThirdParty
{
    using System.Collections.Concurrent;
    using System.Security.Cryptography;
    using System.Text;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using PolyPrompt.Auth;

    /// <summary>
    /// Builds PolyPrompt credential providers from endpoint provider settings. Google credentials that mint
    /// OAuth tokens (service-account JSON and Application Default Credentials) are cached and shared, so a token
    /// is exchanged once and refreshed ahead of expiry rather than on every request.
    /// </summary>
    public static class ProviderCredentialCache
    {
        private static readonly ConcurrentDictionary<string, Lazy<ICredentialProvider>> _GoogleCredentials =
            new ConcurrentDictionary<string, Lazy<ICredentialProvider>>(StringComparer.Ordinal);

        /// <summary>
        /// Resolve the Google credential provider for a Vertex AI endpoint.
        /// </summary>
        /// <param name="settings">Vertex AI settings.</param>
        /// <param name="apiKey">Endpoint ApiKey, used as the static token when CredentialSource is AccessToken.</param>
        /// <returns>The credential provider.</returns>
        /// <exception cref="ArgumentException">The selected credential source is missing its value.</exception>
        public static ICredentialProvider GetVertexCredential(VertexAiProviderSettings settings, string? apiKey)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            switch (settings.CredentialSource)
            {
                case GcpCredentialSourceEnum.AccessToken:
                    if (string.IsNullOrWhiteSpace(apiKey))
                        throw new ArgumentException("Vertex AI endpoints using an access token require the token in ApiKey.", nameof(apiKey));
                    return new StaticTokenCredential(apiKey);

                case GcpCredentialSourceEnum.ApplicationDefault:
                    return _GoogleCredentials.GetOrAdd("adc", _ => new Lazy<ICredentialProvider>(() => new AdcCredential())).Value;

                default:
                    string json = settings.ServiceAccountJson
                        ?? throw new ArgumentException("Vertex AI endpoints using a service account require ProviderSettings.VertexAI.ServiceAccountJson.", nameof(settings));
                    return _GoogleCredentials.GetOrAdd(
                        "sa:" + Sha256Hex(json),
                        _ => new Lazy<ICredentialProvider>(() => ServiceAccountCredential.FromJson(json))).Value;
            }
        }

        /// <summary>
        /// Resolve the AWS credential provider for a Bedrock endpoint.
        /// </summary>
        /// <param name="settings">Bedrock settings.</param>
        /// <returns>The credential provider.</returns>
        /// <exception cref="ArgumentException">Static credentials are incomplete.</exception>
        public static IAwsCredentialProvider GetAwsCredential(BedrockProviderSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            if (settings.CredentialSource == AwsCredentialSourceEnum.Environment)
                return new EnvironmentAwsCredential(settings.Region);

            if (string.IsNullOrWhiteSpace(settings.AccessKeyId) || string.IsNullOrWhiteSpace(settings.SecretAccessKey))
                throw new ArgumentException("Bedrock endpoints with static credentials require an AccessKeyId and SecretAccessKey.", nameof(settings));

            return new StaticAwsCredential(settings.AccessKeyId, settings.SecretAccessKey, settings.Region ?? string.Empty, settings.SessionToken);
        }

        /// <summary>
        /// Dispose and forget every cached Google credential (for example after rotating service-account keys).
        /// </summary>
        public static void Clear()
        {
            foreach (KeyValuePair<string, Lazy<ICredentialProvider>> entry in _GoogleCredentials)
            {
                if (_GoogleCredentials.TryRemove(entry.Key, out Lazy<ICredentialProvider>? removed)
                    && removed.IsValueCreated
                    && removed.Value is IDisposable disposable)
                    disposable.Dispose();
            }
        }

        private static string Sha256Hex(string value)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        }
    }
}
