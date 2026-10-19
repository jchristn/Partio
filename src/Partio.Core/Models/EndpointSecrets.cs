namespace Partio.Core.Models
{
    /// <summary>
    /// Masks endpoint secrets (ApiKey and secret provider settings) in API responses, and restores the stored
    /// value when a client echoes a masked value back on update. A masked value is <c>****</c> followed by the
    /// last four characters of a secret of twelve or more characters, or <c>****</c> alone for shorter secrets and
    /// for service-account JSON. The reveal APIs return secrets unmasked.
    /// </summary>
    public static class EndpointSecrets
    {
        /// <summary>
        /// Prefix of every masked secret.
        /// </summary>
        public const string MaskPrefix = "****";

        private const int _MinimumLengthForTail = 12;
        private const int _TailLength = 4;

        /// <summary>
        /// Mask a secret for display.
        /// </summary>
        /// <param name="value">Secret value.</param>
        /// <returns>The masked value, or the input when it is null or empty.</returns>
        public static string? Mask(string? value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (value.Length < _MinimumLengthForTail) return MaskPrefix;
            return MaskPrefix + value.Substring(value.Length - _TailLength);
        }

        /// <summary>
        /// Mask a secret without revealing any of its characters.
        /// </summary>
        /// <param name="value">Secret value.</param>
        /// <returns><c>****</c>, or the input when it is null or empty.</returns>
        public static string? MaskFully(string? value)
        {
            return string.IsNullOrEmpty(value) ? value : MaskPrefix;
        }

        /// <summary>
        /// Resolve a secret supplied on update: when the client sent back the masked form of the stored value,
        /// keep the stored value; otherwise use the supplied value (including null, which clears the secret).
        /// </summary>
        /// <param name="supplied">Value supplied by the client.</param>
        /// <param name="stored">Value currently stored.</param>
        /// <returns>The value to store.</returns>
        public static string? Resolve(string? supplied, string? stored)
        {
            if (supplied == null || string.IsNullOrEmpty(stored)) return supplied;
            if (supplied == Mask(stored) || supplied == MaskFully(stored)) return stored;
            return supplied;
        }

        /// <summary>
        /// Return a copy of provider settings with secret fields masked.
        /// </summary>
        /// <param name="settings">Provider settings.</param>
        /// <returns>A masked copy, or null.</returns>
        public static ProviderSettings? Redact(ProviderSettings? settings)
        {
            if (settings == null) return null;

            SerializationHelper.Serializer serializer = new SerializationHelper.Serializer();
            ProviderSettings copy = serializer.DeserializeJson<ProviderSettings>(serializer.SerializeJson(settings, false)) ?? new ProviderSettings();

            if (copy.Bedrock != null)
            {
                copy.Bedrock.SecretAccessKey = Mask(copy.Bedrock.SecretAccessKey);
                copy.Bedrock.SessionToken = Mask(copy.Bedrock.SessionToken);
            }

            if (copy.VertexAI != null)
                copy.VertexAI.ServiceAccountJson = MaskFully(copy.VertexAI.ServiceAccountJson);

            return copy;
        }

        /// <summary>
        /// Restore secret provider settings that a client echoed back masked on update.
        /// </summary>
        /// <param name="supplied">Provider settings supplied by the client (modified in place).</param>
        /// <param name="stored">Provider settings currently stored.</param>
        public static void Restore(ProviderSettings? supplied, ProviderSettings? stored)
        {
            if (supplied == null || stored == null) return;

            if (supplied.Bedrock != null && stored.Bedrock != null)
            {
                supplied.Bedrock.SecretAccessKey = Resolve(supplied.Bedrock.SecretAccessKey, stored.Bedrock.SecretAccessKey);
                supplied.Bedrock.SessionToken = Resolve(supplied.Bedrock.SessionToken, stored.Bedrock.SessionToken);
            }

            if (supplied.VertexAI != null && stored.VertexAI != null)
                supplied.VertexAI.ServiceAccountJson = Resolve(supplied.VertexAI.ServiceAccountJson, stored.VertexAI.ServiceAccountJson);
        }
    }
}
