namespace Partio.Core.Enums
{
    /// <summary>
    /// Strategy requested for model loading.
    /// </summary>
    public enum ModelLoadStrategyEnum
    {
        /// <summary>
        /// Let Partio choose the safest provider-specific behavior.
        /// </summary>
        Auto,

        /// <summary>
        /// Require a provider-native load or preload operation.
        /// </summary>
        NativeProviderLoad,

        /// <summary>
        /// Send a minimal inference or embedding request to warm the provider path.
        /// </summary>
        WarmRequest
    }
}
