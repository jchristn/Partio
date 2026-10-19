namespace Partio.Core.Models
{
    using Partio.Core.Enums;

    /// <summary>
    /// AWS Bedrock endpoint settings. The endpoint Model is the Bedrock model ID and Endpoint is the
    /// runtime URL (default https://bedrock-runtime.{Region}.amazonaws.com). ApiKey is not used.
    /// </summary>
    public class BedrockProviderSettings
    {
        /// <summary>
        /// AWS region, for example us-east-1. Selects the runtime host and the signing region.
        /// </summary>
        public string? Region { get; set; }

        /// <summary>
        /// Where signing credentials come from.
        /// </summary>
        public AwsCredentialSourceEnum CredentialSource { get; set; } = AwsCredentialSourceEnum.Static;

        /// <summary>
        /// AWS access key ID (CredentialSource Static).
        /// </summary>
        public string? AccessKeyId { get; set; }

        /// <summary>
        /// AWS secret access key (CredentialSource Static). Redacted on read.
        /// </summary>
        public string? SecretAccessKey { get; set; }

        /// <summary>
        /// Optional session token for temporary credentials (CredentialSource Static). Redacted on read.
        /// </summary>
        public string? SessionToken { get; set; }
    }
}
