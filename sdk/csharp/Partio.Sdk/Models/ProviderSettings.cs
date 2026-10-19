namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Optional provider-specific endpoint settings. Only the section matching the endpoint's ApiFormat is kept by
    /// the server; Bedrock and VertexAI endpoints require their section. Secret fields are masked on read.
    /// </summary>
    public class ProviderSettings
    {
        [JsonPropertyName("AzureOpenAI")]
        public AzureOpenAiProviderSettings? AzureOpenAI { get; set; }

        [JsonPropertyName("Anthropic")]
        public AnthropicProviderSettings? Anthropic { get; set; }

        [JsonPropertyName("Bedrock")]
        public BedrockProviderSettings? Bedrock { get; set; }

        [JsonPropertyName("VertexAI")]
        public VertexAiProviderSettings? VertexAI { get; set; }

        [JsonPropertyName("Cohere")]
        public CohereProviderSettings? Cohere { get; set; }

        [JsonPropertyName("VoyageAI")]
        public VoyageAiProviderSettings? VoyageAI { get; set; }
    }

    /// <summary>Azure OpenAI settings. The endpoint Model is the deployment name.</summary>
    public class AzureOpenAiProviderSettings
    {
        /// <summary>api-version query value. Null uses the server default (2024-10-21).</summary>
        [JsonPropertyName("ApiVersion")]
        public string? ApiVersion { get; set; }

        /// <summary>ApiKey (api-key header) or BearerToken (Entra ID token in ApiKey).</summary>
        [JsonPropertyName("AuthMode")]
        public string AuthMode { get; set; } = AzureAuthModes.ApiKey;
    }

    /// <summary>Anthropic settings.</summary>
    public class AnthropicProviderSettings
    {
        [JsonPropertyName("AnthropicVersion")]
        public string? AnthropicVersion { get; set; }

        [JsonPropertyName("WorkspaceId")]
        public string? WorkspaceId { get; set; }
    }

    /// <summary>AWS Bedrock settings. ApiKey is not used.</summary>
    public class BedrockProviderSettings
    {
        [JsonPropertyName("Region")]
        public string? Region { get; set; }

        /// <summary>Static or Environment.</summary>
        [JsonPropertyName("CredentialSource")]
        public string CredentialSource { get; set; } = AwsCredentialSources.Static;

        [JsonPropertyName("AccessKeyId")]
        public string? AccessKeyId { get; set; }

        /// <summary>Masked on read.</summary>
        [JsonPropertyName("SecretAccessKey")]
        public string? SecretAccessKey { get; set; }

        /// <summary>Masked on read.</summary>
        [JsonPropertyName("SessionToken")]
        public string? SessionToken { get; set; }
    }

    /// <summary>Google Vertex AI settings.</summary>
    public class VertexAiProviderSettings
    {
        [JsonPropertyName("ProjectId")]
        public string? ProjectId { get; set; }

        [JsonPropertyName("Region")]
        public string? Region { get; set; }

        /// <summary>ServiceAccountJson, ApplicationDefault, or AccessToken (token in ApiKey).</summary>
        [JsonPropertyName("CredentialSource")]
        public string CredentialSource { get; set; } = GcpCredentialSources.ServiceAccountJson;

        /// <summary>Full service-account key JSON. Masked on read.</summary>
        [JsonPropertyName("ServiceAccountJson")]
        public string? ServiceAccountJson { get; set; }
    }

    /// <summary>Cohere settings.</summary>
    public class CohereProviderSettings
    {
        [JsonPropertyName("InputType")]
        public string? InputType { get; set; }
    }

    /// <summary>VoyageAI settings.</summary>
    public class VoyageAiProviderSettings
    {
        [JsonPropertyName("InputType")]
        public string? InputType { get; set; }

        [JsonPropertyName("OutputDimension")]
        public int? OutputDimension { get; set; }
    }

    /// <summary>ApiFormat values accepted by the server.</summary>
    public static class ApiFormats
    {
        public const string Ollama = "Ollama";
        public const string OllamaCloud = "OllamaCloud";
        public const string OpenAI = "OpenAI";
        public const string vLLM = "vLLM";
        public const string Gemini = "Gemini";
        public const string xAI = "xAI";
        public const string AzureOpenAI = "AzureOpenAI";
        public const string Anthropic = "Anthropic";
        public const string Cohere = "Cohere";
        public const string Bedrock = "Bedrock";
        public const string VertexAI = "VertexAI";
        public const string TEI = "TEI";
        public const string VoyageAI = "VoyageAI";
    }

    /// <summary>AzureOpenAiProviderSettings.AuthMode values.</summary>
    public static class AzureAuthModes
    {
        public const string ApiKey = "ApiKey";
        public const string BearerToken = "BearerToken";
    }

    /// <summary>BedrockProviderSettings.CredentialSource values.</summary>
    public static class AwsCredentialSources
    {
        public const string Static = "Static";
        public const string Environment = "Environment";
    }

    /// <summary>VertexAiProviderSettings.CredentialSource values.</summary>
    public static class GcpCredentialSources
    {
        public const string ServiceAccountJson = "ServiceAccountJson";
        public const string ApplicationDefault = "ApplicationDefault";
        public const string AccessToken = "AccessToken";
    }
}
