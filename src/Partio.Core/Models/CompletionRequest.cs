namespace Partio.Core.Models
{
    /// <summary>
    /// Request body for generating a completion through a configured completion endpoint.
    /// This is the production inference path (single upstream call); use it to validate an
    /// endpoint or to run a one-off completion without the extra diagnostics of the explorer route.
    /// </summary>
    public class CompletionRequest
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
