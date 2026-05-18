namespace Partio.Core.Models
{
    /// <summary>
    /// Structured diagnostics for one logical input within an upstream embedding request.
    /// </summary>
    public class EmbeddingCallInputDetail
    {
        /// <summary>
        /// Zero-based input index within the upstream request body.
        /// </summary>
        public int Index { get; set; } = 0;

        /// <summary>
        /// Number of characters in this input.
        /// </summary>
        public int CharacterCount { get; set; } = 0;

        /// <summary>
        /// Number of tokens in this input according to the active Partio tokenizer.
        /// </summary>
        public int TokenCount { get; set; } = 0;

        /// <summary>
        /// True when this input exceeds the active per-input content budget.
        /// </summary>
        public bool ExceedsEffectiveInputBudget { get; set; } = false;

        /// <summary>
        /// Short preview used for request-history diagnostics.
        /// </summary>
        public string? Preview { get; set; }
    }
}
