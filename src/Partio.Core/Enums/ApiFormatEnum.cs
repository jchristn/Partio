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
        xAI
    }
}
