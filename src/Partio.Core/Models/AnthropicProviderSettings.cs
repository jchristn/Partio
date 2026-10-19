namespace Partio.Core.Models
{
    /// <summary>
    /// Anthropic endpoint settings.
    /// </summary>
    public class AnthropicProviderSettings
    {
        /// <summary>
        /// anthropic-version header value. Null uses the PolyPrompt default (2023-06-01).
        /// </summary>
        public string? AnthropicVersion { get; set; }

        /// <summary>
        /// Optional anthropic-workspace-id header value, required for identity-linked API keys.
        /// </summary>
        public string? WorkspaceId { get; set; }
    }
}
