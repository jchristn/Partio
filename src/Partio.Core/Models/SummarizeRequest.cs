namespace Partio.Core.Models
{
    /// <summary>
    /// Request body for summarizing a piece of text through a configured completion endpoint, without
    /// chunking or embedding. Uses the same summarization engine as <c>/v1.0/process</c>.
    /// </summary>
    public class SummarizeRequest
    {
        /// <summary>
        /// The text to summarize.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Summarization configuration, including the completion endpoint id, prompt, and token budget.
        /// </summary>
        public SummarizationConfiguration SummarizationConfiguration { get; set; } = new SummarizationConfiguration();
    }
}
