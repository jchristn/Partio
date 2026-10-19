namespace Partio.Core.ThirdParty
{
    using Partio.Core.Enums;

    /// <summary>
    /// Provider-specific constants and rules shared by endpoint normalization, client construction,
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
        /// Whether the provider dialect is the native Ollama API (local Ollama or Ollama Cloud).
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <returns>True for Ollama and OllamaCloud.</returns>
        public static bool IsOllamaDialect(ApiFormatEnum format)
        {
            return format == ApiFormatEnum.Ollama || format == ApiFormatEnum.OllamaCloud;
        }

        /// <summary>
        /// Whether the provider rejects requests without an API key.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <returns>True for OllamaCloud and xAI.</returns>
        public static bool RequiresApiKey(ApiFormatEnum format)
        {
            return format == ApiFormatEnum.OllamaCloud || format == ApiFormatEnum.xAI;
        }

        /// <summary>
        /// Resolve the endpoint URL Partio stores for a provider. OllamaCloud always resolves to
        /// <see cref="OllamaCloudEndpoint"/>. xAI defaults to <see cref="XAiEndpoint"/> and drops a trailing
        /// <c>/v1</c> so health-check and proxy paths are built from the API root. Other formats are unchanged.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <param name="endpoint">Supplied endpoint URL (may be null or empty).</param>
        /// <returns>The endpoint URL to store.</returns>
        public static string NormalizeEndpoint(ApiFormatEnum format, string? endpoint)
        {
            switch (format)
            {
                case ApiFormatEnum.OllamaCloud:
                    return OllamaCloudEndpoint;

                case ApiFormatEnum.xAI:
                    if (string.IsNullOrWhiteSpace(endpoint)) return XAiEndpoint;
                    string trimmed = endpoint.Trim().TrimEnd('/');
                    if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                        trimmed = trimmed.Substring(0, trimmed.Length - 3);
                    return trimmed;

                default:
                    return endpoint ?? string.Empty;
            }
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
        /// Human-readable provider name.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <returns>Display name such as "Ollama Cloud" or "xAI".</returns>
        public static string DisplayName(ApiFormatEnum format)
        {
            switch (format)
            {
                case ApiFormatEnum.OllamaCloud: return "Ollama Cloud";
                case ApiFormatEnum.xAI: return "xAI";
                default: return format.ToString();
            }
        }

        /// <summary>
        /// Low-cardinality provider service label for telemetry.
        /// </summary>
        /// <param name="format">Provider dialect.</param>
        /// <returns>ollama, ollamacloud, openai, vllm, gemini, xai, or (unknown).</returns>
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
                default: return "(unknown)";
            }
        }
    }
}
