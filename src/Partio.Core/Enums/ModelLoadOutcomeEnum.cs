namespace Partio.Core.Enums
{
    /// <summary>
    /// Outcome reported by a model load or warm request.
    /// </summary>
    public enum ModelLoadOutcomeEnum
    {
        /// <summary>
        /// The provider accepted a native model load or preload operation.
        /// </summary>
        Loaded,

        /// <summary>
        /// The provider accepted a small warm request, but native residency is not guaranteed.
        /// </summary>
        Warmed,

        /// <summary>
        /// The provider or endpoint was validated without a native load guarantee.
        /// </summary>
        Validated,

        /// <summary>
        /// The requested strategy is not supported by the configured provider.
        /// </summary>
        Unsupported,

        /// <summary>
        /// The provider load or warm request failed.
        /// </summary>
        Failed
    }
}
