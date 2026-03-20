namespace Partio.Core.Models
{
    /// <summary>
    /// Request body for exercising a configured inference endpoint through Partio.
    /// </summary>
    public class EndpointExplorerCompletionRequest
    {
        /// <summary>
        /// Target completion endpoint ID.
        /// </summary>
        public string EndpointId { get; set; } = string.Empty;

        /// <summary>
        /// User prompt to send.
        /// </summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Optional system prompt.
        /// </summary>
        public string? SystemPrompt { get; set; }

        /// <summary>
        /// Maximum number of tokens to generate.
        /// </summary>
        public int MaxTokens { get; set; } = 512;

        /// <summary>
        /// Timeout in milliseconds.
        /// </summary>
        public int TimeoutMs { get; set; } = 60000;
    }
}
