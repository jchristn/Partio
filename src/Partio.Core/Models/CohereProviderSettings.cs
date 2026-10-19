namespace Partio.Core.Models
{
    /// <summary>
    /// Cohere endpoint settings.
    /// </summary>
    public class CohereProviderSettings
    {
        /// <summary>
        /// Embedding input_type (search_document, search_query, classification, or clustering).
        /// Null sends search_document.
        /// </summary>
        public string? InputType { get; set; }
    }
}
