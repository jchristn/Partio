namespace Partio.Core.Enums
{
    /// <summary>
    /// Supported embedding API formats.
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
        vLLM
    }
}
