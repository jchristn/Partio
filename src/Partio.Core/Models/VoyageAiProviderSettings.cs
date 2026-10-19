namespace Partio.Core.Models
{
    /// <summary>
    /// VoyageAI endpoint settings.
    /// </summary>
    public class VoyageAiProviderSettings
    {
        private int? _OutputDimension = null;

        /// <summary>
        /// Embedding input_type (document or query). Null lets VoyageAI embed the raw input.
        /// </summary>
        public string? InputType { get; set; }

        /// <summary>
        /// Optional output dimension for models that support Matryoshka embeddings (256, 512, 1024, 2048).
        /// </summary>
        public int? OutputDimension
        {
            get => _OutputDimension;
            set => _OutputDimension = !value.HasValue || value.Value >= 1
                ? value
                : throw new ArgumentOutOfRangeException(nameof(OutputDimension), "OutputDimension must be at least 1 when supplied.");
        }
    }
}
