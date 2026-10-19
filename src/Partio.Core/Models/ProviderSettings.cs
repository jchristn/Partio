namespace Partio.Core.Models
{
    /// <summary>
    /// Optional provider-specific settings for an embedding or completion endpoint. Only the section that
    /// matches the endpoint's ApiFormat, plus Auth for providers with selectable authentication, is read and
    /// validated; the others are ignored and dropped on save. Providers configured entirely by Endpoint, ApiKey,
    /// and Model need no section.
    /// </summary>
    public class ProviderSettings
    {
        /// <summary>
        /// Selectable authentication for Ollama, vLLM, TEI, and OpenAI endpoints. When absent, a configured
        /// ApiKey is sent as a bearer token. Ignored for providers with a fixed authentication scheme.
        /// </summary>
        public EndpointAuthSettings? Auth { get; set; }

        /// <summary>
        /// Azure OpenAI settings (ApiFormat AzureOpenAI).
        /// </summary>
        public AzureOpenAiProviderSettings? AzureOpenAI { get; set; }

        /// <summary>
        /// Anthropic settings (ApiFormat Anthropic).
        /// </summary>
        public AnthropicProviderSettings? Anthropic { get; set; }

        /// <summary>
        /// AWS Bedrock settings (ApiFormat Bedrock). Required for Bedrock endpoints.
        /// </summary>
        public BedrockProviderSettings? Bedrock { get; set; }

        /// <summary>
        /// Google Vertex AI settings (ApiFormat VertexAI). Required for Vertex AI endpoints.
        /// </summary>
        public VertexAiProviderSettings? VertexAI { get; set; }

        /// <summary>
        /// Cohere settings (ApiFormat Cohere).
        /// </summary>
        public CohereProviderSettings? Cohere { get; set; }

        /// <summary>
        /// VoyageAI settings (ApiFormat VoyageAI).
        /// </summary>
        public VoyageAiProviderSettings? VoyageAI { get; set; }
    }
}
