namespace Partio.Core.Enums
{
    /// <summary>
    /// Supported provider API formats for embedding and completion endpoints.
    /// </summary>
    public enum ApiFormatEnum
    {
        /// <summary>Ollama embedding API format.</summary>
        Ollama,
        /// <summary>OpenAI embedding API format.</summary>
        OpenAI,
        /// <summary>Google Gemini API format.</summary>
        Gemini,
        /// <summary>vLLM API format (OpenAI-compatible).</summary>
        vLLM,
        /// <summary>Ollama Cloud (native Ollama API at https://ollama.com, bearer token required).</summary>
        OllamaCloud,
        /// <summary>xAI Grok API format (native Responses API for completions, bearer token required).</summary>
        xAI,
        /// <summary>Azure OpenAI (resource endpoint plus deployment name, api-key or Entra ID bearer token).</summary>
        AzureOpenAI,
        /// <summary>Anthropic Claude Messages API (x-api-key header). Completion only.</summary>
        Anthropic,
        /// <summary>Cohere v2 API (chat and embeddings, bearer token).</summary>
        Cohere,
        /// <summary>AWS Bedrock (Converse and InvokeModel APIs, AWS Signature V4).</summary>
        Bedrock,
        /// <summary>Google Vertex AI (Gemini models under a project and region, OAuth bearer token).</summary>
        VertexAI,
        /// <summary>Hugging Face Text Embeddings Inference server (self-hosted). Embedding only.</summary>
        TEI,
        /// <summary>VoyageAI embeddings API (bearer token). Embedding only.</summary>
        VoyageAI
    }
}
