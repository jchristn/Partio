namespace Partio.Core.Enums
{
    /// <summary>
    /// Partio endpoint types that can be targeted by control-plane actions.
    /// </summary>
    public enum EndpointTypeEnum
    {
        /// <summary>
        /// Embedding endpoint.
        /// </summary>
        Embedding,

        /// <summary>
        /// Completion or inference endpoint.
        /// </summary>
        Completion
    }
}
