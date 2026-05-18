namespace Partio.Core.Models
{
    using Partio.Core.Enums;

    /// <summary>
    /// Captures details of a single HTTP call made to an upstream embedding endpoint.
    /// </summary>
    public class EmbeddingCallDetail
    {
        /// <summary>
        /// High-level purpose for this upstream call (embedding request, capability probe, calibration probe).
        /// </summary>
        public string? Purpose { get; set; }

        /// <summary>
        /// Full URL called.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// HTTP method (e.g. POST).
        /// </summary>
        public string? Method { get; set; }

        /// <summary>
        /// Headers sent upstream.
        /// </summary>
        public Dictionary<string, string>? RequestHeaders { get; set; }

        /// <summary>
        /// Body sent upstream.
        /// </summary>
        public string? RequestBody { get; set; }

        /// <summary>
        /// Structured input diagnostics for batched embedding requests, when known.
        /// </summary>
        public List<EmbeddingCallInputDetail>? Inputs { get; set; }

        /// <summary>
        /// Number of content tokens across all inputs in the upstream request, when known.
        /// </summary>
        public int? BatchTokenCount { get; set; }

        /// <summary>
        /// Effective per-input token budget used by Partio when preparing this call.
        /// </summary>
        public int? EffectiveInputBudget { get; set; }

        /// <summary>
        /// Raw upstream maximum input tokens reported or resolved for the active model.
        /// </summary>
        public int? MaxInputTokens { get; set; }

        /// <summary>
        /// Batch limit mode Partio assumed or discovered for this call.
        /// </summary>
        public BatchLimitModeEnum? BatchLimitMode { get; set; }

        /// <summary>
        /// Best-effort indices of inputs that likely triggered the failure, when determinable.
        /// </summary>
        public List<int>? FailedInputIndices { get; set; }

        /// <summary>
        /// Human-readable hint describing why the call failed, when Partio can infer it.
        /// </summary>
        public string? FailureReasonHint { get; set; }

        /// <summary>
        /// HTTP status code returned.
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// Response headers.
        /// </summary>
        public Dictionary<string, string>? ResponseHeaders { get; set; }

        /// <summary>
        /// Response body.
        /// </summary>
        public string? ResponseBody { get; set; }

        /// <summary>
        /// Timing for this call in milliseconds.
        /// </summary>
        public double? ResponseTimeMs { get; set; }

        /// <summary>
        /// Whether the call succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the call failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// UTC timestamp when the call was initiated.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
