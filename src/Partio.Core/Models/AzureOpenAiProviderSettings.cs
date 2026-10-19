namespace Partio.Core.Models
{
    using Partio.Core.Enums;

    /// <summary>
    /// Azure OpenAI endpoint settings. The endpoint Model is the Azure deployment name and Endpoint is the
    /// resource URL (for example https://my-resource.openai.azure.com).
    /// </summary>
    public class AzureOpenAiProviderSettings
    {
        /// <summary>
        /// Azure OpenAI api-version query value. Null uses the PolyPrompt default (2024-10-21).
        /// </summary>
        public string? ApiVersion { get; set; }

        /// <summary>
        /// How the endpoint ApiKey is presented: an api-key header (default) or an Entra ID bearer token.
        /// </summary>
        public AzureAuthModeEnum AuthMode { get; set; } = AzureAuthModeEnum.ApiKey;
    }
}
