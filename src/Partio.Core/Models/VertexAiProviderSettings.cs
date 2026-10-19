namespace Partio.Core.Models
{
    using Partio.Core.Enums;

    /// <summary>
    /// Google Vertex AI endpoint settings. Endpoint is the regional API root
    /// (default https://{Region}-aiplatform.googleapis.com).
    /// </summary>
    public class VertexAiProviderSettings
    {
        /// <summary>
        /// Google Cloud project ID.
        /// </summary>
        public string? ProjectId { get; set; }

        /// <summary>
        /// Vertex AI region, for example us-central1.
        /// </summary>
        public string? Region { get; set; }

        /// <summary>
        /// Where OAuth bearer tokens come from. AccessToken uses the endpoint ApiKey as a static token.
        /// </summary>
        public GcpCredentialSourceEnum CredentialSource { get; set; } = GcpCredentialSourceEnum.ServiceAccountJson;

        /// <summary>
        /// Full service-account key JSON (CredentialSource ServiceAccountJson). Redacted on read.
        /// </summary>
        public string? ServiceAccountJson { get; set; }
    }
}
