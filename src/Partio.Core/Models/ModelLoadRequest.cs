namespace Partio.Core.Models
{
    using Partio.Core.Enums;

    /// <summary>
    /// Request payload for loading or warming a configured model endpoint.
    /// </summary>
    public class ModelLoadRequest
    {
        private ModelLoadStrategyEnum _Strategy = ModelLoadStrategyEnum.Auto;
        private int _TimeoutMs = 0;
        private string? _KeepAlive = "30m";
        private string _SampleInput = "Partio model load probe";
        private int _MaxTokens = 1;
        private bool _RecordRequestHistory = true;
        private bool _RequireNativeLoad = false;

        /// <summary>
        /// Requested loading strategy.
        /// </summary>
        public ModelLoadStrategyEnum Strategy
        {
            get => _Strategy;
            set => _Strategy = value;
        }

        /// <summary>
        /// Requested timeout in milliseconds. Zero uses the endpoint maximum.
        /// </summary>
        public int TimeoutMs
        {
            get => _TimeoutMs;
            set => _TimeoutMs = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Provider keep-alive value. Applies to Ollama native load paths.
        /// </summary>
        public string? KeepAlive
        {
            get => _KeepAlive;
            set => _KeepAlive = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>
        /// Short non-sensitive input used for warm requests.
        /// </summary>
        public string SampleInput
        {
            get => _SampleInput;
            set
            {
                string normalized = string.IsNullOrWhiteSpace(value) ? "Partio model load probe" : value.Trim();
                _SampleInput = normalized.Length > 4096 ? normalized.Substring(0, 4096) : normalized;
            }
        }

        /// <summary>
        /// Maximum completion tokens for warm completion requests.
        /// </summary>
        public int MaxTokens
        {
            get => _MaxTokens;
            set => _MaxTokens = value < 1 ? 1 : value > 16 ? 16 : value;
        }

        /// <summary>
        /// Whether the route should write detailed request history when request history is enabled.
        /// </summary>
        public bool RecordRequestHistory
        {
            get => _RecordRequestHistory;
            set => _RecordRequestHistory = value;
        }

        /// <summary>
        /// Whether the caller requires a provider-native load operation.
        /// </summary>
        public bool RequireNativeLoad
        {
            get => _RequireNativeLoad;
            set => _RequireNativeLoad = value;
        }

        /// <summary>
        /// Resolve the effective timeout against an endpoint maximum.
        /// </summary>
        /// <param name="endpointMaximumTimeoutMs">Endpoint maximum timeout in milliseconds.</param>
        /// <returns>Positive timeout value in milliseconds.</returns>
        public int ResolveTimeoutMs(int endpointMaximumTimeoutMs)
        {
            int maximum = endpointMaximumTimeoutMs <= 0 ? 1 : endpointMaximumTimeoutMs;
            if (TimeoutMs <= 0) return maximum;
            return Math.Min(TimeoutMs, maximum);
        }
    }
}
