namespace Partio.Core.Models
{
    using Partio.Core.Enums;

    /// <summary>
    /// Provider-level result for a model load or warm request.
    /// </summary>
    public class ModelLoadProviderResult
    {
        /// <summary>
        /// Whether the provider action succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Partio-mapped status code for the provider action.
        /// </summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// Provider action outcome.
        /// </summary>
        public ModelLoadOutcomeEnum Outcome { get; set; } = ModelLoadOutcomeEnum.Warmed;

        /// <summary>
        /// Effective strategy used by the provider client.
        /// </summary>
        public ModelLoadStrategyEnum Strategy { get; set; } = ModelLoadStrategyEnum.WarmRequest;

        /// <summary>
        /// Operator-safe message describing the result.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
